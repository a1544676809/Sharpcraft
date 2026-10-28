#!/usr/bin/env python3
# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 MinecraftCSharp contributors
"""Package-level dependency scan over decompiled Minecraft sources.

Purpose: collect evidence for side attribution (Client / Server / Common /
peripheral host layer). It reads .java files, resolves each file's package from
its path, collects referenced packages from import statements and fully
qualified names, and aggregates a package-to-package reference graph.

Limitations (recorded in the generated docs):
  - Import/qualified-name based only; method-local coupling through interfaces
    is invisible.
  - Decompiler may inline fully qualified names, inflating some edges.

Usage:
  python package_deps.py --src <decompiled root> --out <report dir>
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
from collections import Counter, defaultdict
from pathlib import Path

PKG_DECL = re.compile(r"^\s*package\s+([\w.]+)\s*;", re.M)
IMPORT_DECL = re.compile(r"^\s*import\s+(?:static\s+)?([\w.]+)\s*;", re.M)
FULLY_QUALIFIED = re.compile(r"\b((?:net\.minecraft|com\.mojang)\.[\w.]+)")

CLIENT_ROOT = "net.minecraft.client"
SERVER_ROOT = "net.minecraft.server"
ENGINE_PREFIX = "com.mojang."


def known_packages(root: Path) -> tuple[set[str], dict[str, int], dict[str, str]]:
    """Return owning packages, per-package file counts, and a type-name index."""
    packages: Counter[str] = Counter()
    type_index: dict[str, str] = {}
    for path in sorted(root.rglob("*.java")):
        rel = path.relative_to(root)
        if len(rel.parts) < 2:
            continue
        package = ".".join(rel.parts[:-1])
        packages[package] += 1
        type_index.setdefault(package + "." + path.stem, package)
    return set(packages), dict(packages), type_index


def resolve_package(reference: str, type_index: dict[str, str]) -> str | None:
    """Resolve a reference to the package that owns it.

    Resolution is anchored on known *type* names (package + file stem) instead of
    dotted prefixes: prefix tables depend on iteration order and made the whole
    report non-reproducible between runs.
    """
    parts = reference.split(".")
    for i in range(len(parts), 2, -1):
        owner = type_index.get(".".join(parts[:i]))
        if owner is not None:
            return owner
    return None


def scan(root: Path) -> tuple[Counter[tuple[str, str]], dict[str, int]]:
    packages, file_counts, type_index = known_packages(root)
    edges: Counter[tuple[str, str]] = Counter()

    for path in sorted(root.rglob("*.java")):
        rel = path.relative_to(root)
        if len(rel.parts) < 2:
            continue
        src_pkg = ".".join(rel.parts[:-1])
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue

        targets: set[str] = set()
        for match in IMPORT_DECL.finditer(text):
            resolved = resolve_package(match.group(1), type_index)
            if resolved:
                targets.add(resolved)
        for match in FULLY_QUALIFIED.finditer(text):
            resolved = resolve_package(match.group(1), type_index)
            if resolved:
                targets.add(resolved)

        for target in targets:
            if target != src_pkg:
                edges[(src_pkg, target)] += 1

    return edges, file_counts


def is_client(pkg: str) -> bool:
    return pkg == CLIENT_ROOT or pkg.startswith(CLIENT_ROOT + ".")


def is_server(pkg: str) -> bool:
    return pkg == SERVER_ROOT or pkg.startswith(SERVER_ROOT + ".")


def is_engine(pkg: str) -> bool:
    return pkg.startswith(ENGINE_PREFIX)


def summarize(edges: Counter[tuple[str, str]], file_counts: dict[str, int]) -> dict:
    dep_on_client: Counter[str] = Counter()
    dep_on_server: Counter[str] = Counter()
    dep_on_engine: Counter[str] = Counter()
    used_by_client: Counter[str] = Counter()
    used_by_server: Counter[str] = Counter()
    total_out: Counter[str] = Counter()

    for (src, dst), weight in edges.items():
        total_out[src] += weight
        if is_client(dst):
            dep_on_client[src] += weight
        if is_server(dst):
            dep_on_server[src] += weight
        if is_engine(dst):
            dep_on_engine[src] += weight
        if is_client(src):
            used_by_client[dst] += weight
        if is_server(src):
            used_by_server[dst] += weight

    return {
        "dep_on_client": dep_on_client,
        "dep_on_server": dep_on_server,
        "dep_on_engine": dep_on_engine,
        "used_by_client": used_by_client,
        "used_by_server": used_by_server,
        "total_out": total_out,
    }


def classify(pkg: str, stats: dict) -> str:
    """Heuristic side attribution from reference direction asymmetry.

    The decisive signal is whether a package references ``net.minecraft.client``
    at all: vanilla keeps every server-bound and shared package free of client
    references, so a non-zero client dependency is the thing that forces a
    package onto the client side (or requires splitting it).
    """
    out_c = stats["dep_on_client"][pkg]
    out_s = stats["dep_on_server"][pkg]
    in_c = stats["used_by_client"][pkg]
    in_s = stats["used_by_server"][pkg]

    if is_engine(pkg):
        return "Peripheral(engine)"
    if is_client(pkg):
        return "Client"
    if is_server(pkg):
        return "Server"
    if out_c:
        return "Client-coupled(must split)"
    if in_c and in_s:
        return "Common(shared)"
    if in_s:
        return "Common(server-consumed)"
    if in_c:
        return "Common(client-consumed)"
    return "Common(isolated)"


def default_analysis_root() -> Path:
    """Resolve the external analysis workspace (AGENTS.md 3.6).

    ``MC_ANALYSIS_ROOT`` wins; otherwise the sibling directory of the repository
    checkout is used, i.e. ``ref://mc-26.3-analysis`` -> ``<repo>/../mc-26.3-analysis``.
    """
    configured = os.environ.get("MC_ANALYSIS_ROOT")
    if configured:
        return Path(configured)
    repository_root = Path(__file__).resolve().parents[2]
    return repository_root.parent / "mc-26.3-analysis"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=default_analysis_root(),
                        help="analysis workspace root (default: $MC_ANALYSIS_ROOT or ../mc-26.3-analysis)")
    parser.add_argument("--src", type=Path, default=None, help="decompiled sources (default: <root>/src)")
    parser.add_argument("--out", type=Path, default=None, help="report directory (default: <root>/reports)")
    parser.add_argument("--quiet", action="store_true", help="suppress the markdown tables (CI friendly)")
    args = parser.parse_args()

    root_arg: Path = args.root
    src_dir: Path = args.src or (root_arg / "src")
    out_dir: Path = args.out or (root_arg / "reports")

    root: Path = src_dir
    if not root.is_dir():
        print(f"source directory not found: {root}")
        return 2

    edges, file_counts = scan(root)
    stats = summarize(edges, file_counts)
    out_dir.mkdir(parents=True, exist_ok=True)

    with (out_dir / "edges.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["source_package", "target_package", "weight"])
        for (src, dst), weight in sorted(edges.items(), key=lambda kv: (-kv[1], kv[0])):
            writer.writerow([src, dst, weight])

    # Aggregate to the depth used by the architecture docs.
    def key_of(pkg: str) -> str:
        parts = pkg.split(".")
        if len(parts) >= 3 and parts[0] == "net" and parts[1] == "minecraft":
            if parts[2] == "world" and len(parts) >= 4:
                return ".".join(parts[:4])
            return ".".join(parts[:3])
        if len(parts) >= 3 and parts[0] == "com" and parts[1] == "mojang":
            return ".".join(parts[:3])
        if len(parts) >= 2:
            return ".".join(parts[:2])
        return pkg

    groups: dict[str, dict[str, int]] = defaultdict(
        lambda: {"classes": 0, "out_client": 0, "out_server": 0, "out_engine": 0,
                 "in_client": 0, "in_server": 0, "out_total": 0}
    )
    for pkg, count in file_counts.items():
        groups[key_of(pkg)]["classes"] += count
    for (src, dst), weight in edges.items():
        bucket = groups[key_of(src)]
        bucket["out_total"] += weight
        if is_client(dst):
            bucket["out_client"] += weight
        if is_server(dst):
            bucket["out_server"] += weight
        if is_engine(dst):
            bucket["out_engine"] += weight
        inverse = groups[key_of(dst)]
        if is_client(src):
            inverse["in_client"] += weight
        if is_server(src):
            inverse["in_server"] += weight

    rows = []
    for key, bucket in groups.items():
        synthetic = {
            "dep_on_client": Counter({key: bucket["out_client"]}),
            "dep_on_server": Counter({key: bucket["out_server"]}),
            "used_by_client": Counter({key: bucket["in_client"]}),
            "used_by_server": Counter({key: bucket["in_server"]}),
        }
        rows.append({"package": key, **bucket, "side": classify(key, synthetic)})
    rows.sort(key=lambda row: -row["classes"])

    with (out_dir / "summary.json").open("w", encoding="utf-8") as handle:
        json.dump({"groups": rows, "edge_count": len(edges),
                   "package_count": len(file_counts)}, handle, indent=2, ensure_ascii=False)

    def md_table(items: list[dict]) -> str:
        header = ("| 包 | 类 | 依赖 Client | 依赖 Server | 依赖引擎 | 被 Client 用 | 被 Server 用 | 判定 |\n"
                  "| --- | --- | --- | --- | --- | --- | --- | --- |\n")
        body = "".join(
            f"| `{r['package']}` | {r['classes']} | {r['out_client']} | {r['out_server']} | "
            f"{r['out_engine']} | {r['in_client']} | {r['in_server']} | {r['side']} |\n"
            for r in items
        )
        return header + body

    top = [r for r in rows if r["classes"] >= 8]
    (out_dir / "summary.md").write_text(md_table(top), encoding="utf-8")

    print(f"packages={len(file_counts)} edges={len(edges)} groups={len(rows)}")

    # Full-depth packages that reference client code while not being client code
    # themselves: the concrete list of packages we must split or keep client-side.
    coupled: Counter[str] = Counter()
    for (src, dst), weight in edges.items():
        if is_client(dst) and not is_client(src) and not is_engine(src):
            coupled[src] += weight
    with (out_dir / "client_coupled.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["package", "client_reference_weight"])
        for pkg, weight in coupled.most_common():
            writer.writerow([pkg, weight])
    print(f"client_coupled_packages={len(coupled)}")
    if not args.quiet:
        for pkg, weight in coupled.most_common(20):
            print(f"  {pkg} {weight}")
        print("\n" + md_table(rows[:45]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
