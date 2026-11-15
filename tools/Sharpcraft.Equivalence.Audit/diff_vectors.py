#!/usr/bin/env python3
# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

VEC_RE = re.compile(r'^\(([^)]+)\)$')

def parse_tsv(path: Path) -> dict[tuple[str, str], dict[str, str]]:
    cases = {}
    for line in path.read_text(encoding='utf-8').splitlines():
        if not line or line.startswith('#'):
            continue
        parts = line.split('\t')
        suite, case_id = parts[0], parts[1]
        fields = {}
        for kv in parts[2:]:
            k, v = kv.split('=', 1)
            fields[k] = v
        cases[(suite, case_id)] = fields
    return cases

def values_equal(a: str, b: str, eps: float) -> bool:
    if a == b:
        return True
    ma, mb = VEC_RE.match(a), VEC_RE.match(b)
    if ma and mb:
        va = [float(x) for x in ma.group(1).split(',')]
        vb = [float(x) for x in mb.group(1).split(',')]
        return len(va) == len(vb) and all(abs(x - y) <= eps for x, y in zip(va, vb))
    try:
        return abs(float(a) - float(b)) <= eps
    except ValueError:
        return False

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--vanilla', required=True, type=Path)
    ap.add_argument('--ours', required=True, type=Path)
    ap.add_argument('--eps', type=float, default=1e-4)
    ap.add_argument('--json-out', type=Path, default=None)
    args = ap.parse_args()

    van = parse_tsv(args.vanilla)
    ours = parse_tsv(args.ours)
    suite_stats = defaultdict(lambda: {'total': 0, 'passed': 0, 'mismatches': []})

    for key in sorted(set(van) | set(ours)):
        suite, cid = key
        suite_stats[suite]['total'] += 1
        if key not in van:
            suite_stats[suite]['mismatches'].append({'case': cid, 'field': 'missing_in_vanilla'})
            continue
        if key not in ours:
            suite_stats[suite]['mismatches'].append({'case': cid, 'field': 'missing_in_ours'})
            continue
        vf, of = van[key], ours[key]
        bad = False
        for k in sorted(set(vf) | set(of)):
            va, oa = vf.get(k, '<missing>'), of.get(k, '<missing>')
            if not values_equal(va, oa, args.eps):
                suite_stats[suite]['mismatches'].append({'case': cid, 'field': k, 'vanilla': va, 'ours': oa})
                bad = True
        if not bad:
            suite_stats[suite]['passed'] += 1

    total_mismatches = sum(len(s['mismatches']) for s in suite_stats.values())
    status = 'FAIL' if total_mismatches > 0 else 'PASS'
    print(f"{status} suites={len(suite_stats)} cases={len(van)} mismatches={total_mismatches} eps={args.eps}")
    for suite, st in sorted(suite_stats.items()):
        print(f"  suite={suite} passed={st['passed']}/{st['total']} mismatches={len(st['mismatches'])}")
        for m in st['mismatches'][:5]:
            print(f"    [{m['case']}] {m['field']}: vanilla={m.get('vanilla','')} ours={m.get('ours','')}")

    if args.json_out is not None:
        args.json_out.write_text(json.dumps({'status': status, 'suites': suite_stats}, indent=2), encoding='utf-8')
    return 1 if total_mismatches > 0 else 0

if __name__ == '__main__':
    sys.exit(main())
