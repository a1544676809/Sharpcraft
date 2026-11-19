# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Writes a small JSON manifest of the local analysis workspace and truth probes.

.DESCRIPTION
    The manifest records the jar identity, classpath/source counts and hashes of the hand-written
    probe files. Tomorrow's synchronization can compare manifests before accepting either side as
    canonical. This script writes only to the requested output path (default: temp), never into the
    analysis workspace or the repository. The manifest stores only the logical reference path
    (ref://mc-26.3-analysis), never the absolute machine path, so it is safe to commit or share.
.PARAMETER AnalysisRoot
    Analysis workspace root. Required: pass this argument or set MC_ANALYSIS_ROOT. No implicit
    machine-specific path is assumed; ref://mc-26.3-analysis resolves to this value.
.PARAMETER Out
    Manifest output path. Defaults to the system temp directory.
#>
param(
    [string]$AnalysisRoot = '',
    [string]$Out = (Join-Path ([System.IO.Path]::GetTempPath()) 'sharpcraft-reference-manifest.json')
)

$ErrorActionPreference = 'Stop'

if (-not $AnalysisRoot) {
    $AnalysisRoot = $env:MC_ANALYSIS_ROOT
}
if (-not $AnalysisRoot) {
    throw 'MC_ANALYSIS_ROOT is not set and -AnalysisRoot was not supplied. Set MC_ANALYSIS_ROOT to the mc-26.3-analysis workspace root (the directory that resolves ref://mc-26.3-analysis).'
}
$AnalysisRoot = (Resolve-Path $AnalysisRoot).Path

$jar = Join-Path $AnalysisRoot 'artifacts\26.3\client.jar'
$classpath = Join-Path $AnalysisRoot 'vectors\cp.txt'
$sources = Join-Path $AnalysisRoot 'src\net\minecraft'

$probeFiles = @(
    'ModelProbe.java',
    'run-model-probe.ps1',
    'compare-model-probe.ps1',
    'validate-reference-workspace.ps1',
    'write-reference-manifest.ps1',
    'README.md'
)

$files = foreach ($name in $probeFiles) {
    $path = Join-Path $PSScriptRoot $name
    if (Test-Path $path) {
        [pscustomobject]@{
            path = $name
            sha256 = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
        }
    }
}

$manifest = [pscustomobject]@{
    kind = 'sharpcraft-reference-manifest'
    version = 1
    generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
    analysisRoot = 'ref://mc-26.3-analysis'
    jarSha1 = (Get-FileHash -Algorithm SHA1 -Path $jar).Hash.ToLowerInvariant()
    classpathEntries = if (Test-Path $classpath) { ((Get-Content $classpath -Raw).Trim().Split(';')).Count } else { 0 }
    decompiledJavaFiles = (Get-ChildItem $sources -Recurse -Filter *.java -File -ErrorAction SilentlyContinue | Measure-Object).Count
    probeFiles = @($files)
}

$outPath = [System.IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outPath) | Out-Null
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $outPath
Write-Host "manifest: $outPath"
