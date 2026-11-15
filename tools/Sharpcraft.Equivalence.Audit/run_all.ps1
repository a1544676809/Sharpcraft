# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
param(
    [Parameter(Mandatory = $true)][string]$Jar,
    [string]$Suite = 'entity',
    [string]$Work = (Join-Path ([System.IO.Path]::GetTempPath()) 'sharpcraft-equiv-v2')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Work | Out-Null
$vanilla = Join-Path $Work 'vanilla.tsv'
$ours = Join-Path $Work 'ours.tsv'
$report = Join-Path $Work 'report.json'

& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'truth/run.ps1') -Jar $Jar -Out $vanilla
if ($LASTEXITCODE -ne 0) { throw "vanilla probe failed: $LASTEXITCODE" }

& dotnet run --project $PSScriptRoot -- --suite $Suite --out $ours
if ($LASTEXITCODE -ne 0) { throw "sut audit failed: $LASTEXITCODE" }

& python3 (Join-Path $PSScriptRoot 'diff_vectors.py') --vanilla $vanilla --ours $ours --json-out $report
exit $LASTEXITCODE
