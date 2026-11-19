<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (C) 2026 Sharpcraft contributors -->

# Local reference-model probe workflow

This directory is the **local truth-bridge toolkit**: hand-written probes and scripts that sample
reference vectors from the local Minecraft 26.3 jar. It produces numbers only; it does not copy
Minecraft source, assets, or a game jar into this repository.

The canonical evidence workspace is the external `mc-26.3-analysis` repository (`ref://mc-26.3-analysis/...`).
These tools consume that workspace through the `MC_ANALYSIS_ROOT` environment variable so sampling is
reproducible without requiring the analysis repository to be network-reachable.

## Why this exists

`AGENTS.md` principle 14 requires golden vectors from the real 26.3 jar. The normal path is the
external analysis workspace (`ref://mc-26.3-analysis/...`). The scripts here are the small,
hand-written sampling bridge used when that workspace is available locally but its probe binaries
are not otherwise wired into the build:

- `validate-reference-workspace.ps1` — checks the jar SHA-1, classpath and decompressed source root.
- `ModelProbe.java` — hand-written Java probe; bakes a vanilla `ModelLayers` entry, applies an
  optional source part pose and PoseStack transform chain, and dumps every cube vertex + UV.
- `run-model-probe.ps1` — compiles and runs the probe with a temporary class directory.
- `compare-model-probe.ps1` — compares two probe captures by vertex key with a numeric tolerance.
- `write-reference-manifest.ps1` — snapshots jar identity and probe hashes for synchronization.

The probe is intentionally a **truth generator**, not a renderer reimplementation. It is compiled
from source under `tools/<project>/truth/`, which the repository's compliance guard explicitly
allows for evidence generators.

## Preflight

Set the analysis workspace root before running any script. It must be the directory that resolves
`ref://mc-26.3-analysis` (contains `artifacts/26.3/client.jar`, `vectors/cp.txt`, `src/net/minecraft`):

```powershell
$env:MC_ANALYSIS_ROOT = '<analysis-workspace-root>'   # or pass -AnalysisRoot to each script
```

Every script requires either `MC_ANALYSIS_ROOT` or an explicit `-AnalysisRoot`; none assumes a
machine-specific default. If neither is set, the script fails with an actionable message. No
absolute machine path is stored in the repository, and the manifest records only the logical path
`ref://mc-26.3-analysis`.

```powershell
pwsh -NoProfile -File tools/Sharpcraft.Equivalence.Audit/truth/validate-reference-workspace.ps1 `
    -ExpectedJarSha1 'e877b6a07acd633fb3bb475002175cec036e7b87'
```

It reports `jar SHA-1`, classpath entry count and decompressed-source count. `ExpectedJarSha1` is an
explicit parameter (baseline identity); pass an empty string to skip the hash check.

To snapshot the local workspace identity and probe hashes:

```powershell
pwsh -NoProfile -File tools/Sharpcraft.Equivalence.Audit/truth/write-reference-manifest.ps1 `
    -Out "$env:TEMP/reference-manifest.json"
```

## Raw model-space sample

```powershell
pwsh -NoProfile -File tools/Sharpcraft.Equivalence.Audit/truth/run-model-probe.ps1 `
    -Layer CONDUIT_SHELL `
    -Out "$env:TEMP/conduit-raw.tsv"
```

The default output is the baked layer in **model pixels, y-down**, exactly as the source layer
declares its cubes. Use this form when checking that a bake refactor did not change the raw
`EntityModelPart` geometry.

## Final block-space sample

The source renderer applies its pose first, then `ModelPart.render` applies the 1/16 model scale.
Pass `-ModelScale 0.0625` and the renderer's exact translation/rotation/scale chain:

```powershell
pwsh -NoProfile -File tools/Sharpcraft.Equivalence.Audit/truth/run-model-probe.ps1 `
    -Layer CONDUIT_SHELL `
    -ModelScale 0.0625 `
    -Transform translate:0.5,0.5,0.5 `
    -Out "$env:TEMP/conduit-world.tsv"
```

For the inactive conduit shell this produces the first DOWN vertex
`(0.687500, 0.312500, 0.687500)` with UV `(0.375000, 0.0)`, matching the reference shell sample
already committed under `tests/Sharpcraft.Client.Tests/Repro/Data/conduit-vectors.golden.txt`.

## Pose and transform grammar

Options are applied in command-line order:

| Option | Meaning |
| --- | --- |
| `-Pose "path=rx,ry,rz"` | Set a `ModelPart` pose in **radians**, before sampling. `path` is dot-separated (`body.head`) or `root`. Repeating `--pose` is allowed. |
| `-Transform translate:x,y,z` | PoseStack translation, in current units (block units after model scale is applied). |
| `-Transform scale:x,y,z` | PoseStack scale. |
| `-Transform rotX:deg` / `rotY:deg` / `rotZ:deg` | PoseStack rotation in degrees. |
| `-ModelScale factor` | Model-pixel scale applied last; `0.0625` gives block-space vertices, default `1` gives raw pixels. |

The resulting order is: source part pose → renderer transform chain → model-pixel scale → vertices.
That order matters: applying `-ModelScale` before `translate:0.5,0.5,0.5` scales the translation
too and produces the wrong absolute position.

## Output contract

The first line is a stable header:

```text
# layer  part  cube  polygon  vertex  x  y  z  u  v
```

Rows are tab-separated with six-decimal values. `part` is `/` for the root or the dot-separated
part path. Outputs belong in the system temp directory or another path **outside the repository**
unless a reviewed, small golden vector is being added under `tests/`.

Compare a before/after capture by vertex key, with numeric tolerance:

```powershell
pwsh -NoProfile -File tools/Sharpcraft.Equivalence.Audit/truth/compare-model-probe.ps1 `
    -Reference "$env:TEMP/conduit-before.tsv" `
    -Actual "$env:TEMP/conduit-after.tsv" `
    -Tolerance 0.000001
```

## Using it for issue #102

The #102 refactor moves the y-down/y-up flip from the bake layer to each renderer. For every
candidate renderer, capture both sides of the move:

1. **Raw before/after**: `-Layer <LAYER>` with no transforms. The raw model-pixel vertices must be
   byte-for-byte unchanged by the bake refactor.
2. **Final before/after**: add the renderer's source transform chain and `-ModelScale 0.0625`. The
   final world-space vertices and UVs must be unchanged after the flip moves.
3. Only then delete renderer-frame compensation constants (`24`, `-1f`, ad-hoc `scale`).

Suggested first targets for the current tree:

- `CONDUIT_SHELL` — positions already have a 24/24 golden sample; use it as the first
  “flip moved, final vertices unchanged” check.
- `STANDING_BANNER` / `STANDING_BANNER_FLAG` — verifies the tilt-sign case; remember #111 is
  currently a pinned known defect, not a green reference.
- `COPPER_GOLEM` — verifies the baseline/root-pose case.

For a model whose source renderer has no extra transform chain, the raw model-space dump is already
the final geometry; do not add a fake compensation.

## Synchronization note

These tools are the committed, reviewable bridge; the sample vectors they produce are the contract
and are stored under `tests/` or in the external analysis workspace, never here. The hand-written
Java probe and its runners may also be mirrored into `mc-26.3-analysis` when that repository is
reachable. Do not commit the jar, decompiled sources, classpath cache, compiled classes, or probe
outputs to either repository.
