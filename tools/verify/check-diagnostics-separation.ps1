# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Diagnostics separation check (docs/specs/quality/vanilla-equivalence-matrix.md row 16).
#
# Rule: the game itself must not read environment variables. Everything tunable for debugging lives in the launcher, which
# turns it into options and hands them to the game. Then "remove the debug code" is one folder deletion, not an audit.
param(
    [switch]$Report
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$gameAssemblies = 'Sharpcraft.Framework', 'Sharpcraft.Client', 'Sharpcraft.Common'

$violations = 0
foreach ($assembly in $gameAssemblies) {
    $directory = Join-Path $root "src/$assembly"
    if (-not (Test-Path $directory)) { continue }
    $hits = Get-ChildItem $directory -Recurse -Include '*.cs' | Select-String -Pattern 'GetEnvironmentVariable' -ErrorAction SilentlyContinue
    foreach ($hit in $hits) {
        $violations++
        $relative = $hit.Path.Replace("$root\", '')
        $trimmed = $hit.Line.Trim()
        "FAIL separation $relative`:$($hit.LineNumber) $trimmed"
    }
}

"INFO violations=$violations rule=game-assemblies-read-no-environment"
if ($violations -gt 0 -and -not $Report) { exit 1 }
if ($violations -eq 0) { 'PASS separation violations=0' }
