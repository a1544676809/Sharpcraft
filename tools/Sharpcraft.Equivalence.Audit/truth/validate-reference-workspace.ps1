# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Validates the local Minecraft 26.3 analysis workspace used by the truth probes.

.DESCRIPTION
    The analysis repository is not always reachable. This script checks only the local workspace
    that the probes consume: the version jar, its SHA-1, the cached classpath, and the decompiled
    source tree. It never copies anything into the repository.

.PARAMETER AnalysisRoot
    Analysis workspace root. Required: pass this argument or set MC_ANALYSIS_ROOT. No implicit
    machine-specific path is assumed; ref://mc-26.3-analysis resolves to this value.

.PARAMETER ExpectedJarSha1
    Expected SHA-1 of the Minecraft 26.3 client jar (baseline identity). Explicit parameter so a
    baseline upgrade cannot silently reuse a stale hash. Pass an empty string to skip the check.
#>
param(
    [string]$AnalysisRoot = '',
    [string]$ExpectedJarSha1 = 'e877b6a07acd633fb3bb475002175cec036e7b87'
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
$required = @(
    $jar,
    (Join-Path $AnalysisRoot 'vectors\make-classpath.ps1'),
    (Join-Path $AnalysisRoot 'vectors\run-probe.ps1')
)

foreach ($path in $required) {
    if (-not (Test-Path $path)) {
        throw "analysis workspace is missing: $path"
    }
}

if (-not (Test-Path $classpath)) {
    throw "classpath cache is missing: $classpath (run tools/setup_workspace.py)"
}

if (-not (Test-Path $sources)) {
    throw "decompiled source tree is missing: $sources"
}

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    throw 'java is not on PATH'
}
if (-not (Get-Command javac -ErrorAction SilentlyContinue)) {
    throw 'javac is not on PATH'
}

if ($ExpectedJarSha1) {
    $actual = (Get-FileHash -Algorithm SHA1 -Path $jar).Hash.ToLowerInvariant()
    if ($actual -ne $ExpectedJarSha1.ToLowerInvariant()) {
        throw "client jar SHA-1 mismatch: expected $ExpectedJarSha1, got $actual"
    }
    Write-Host "jar SHA-1: ok ($actual)"
}

$cp = (Get-Content $classpath -Raw).Trim()
if (-not $cp) {
    throw "classpath cache is empty: $classpath"
}
Write-Host "classpath: $($cp.Split(';').Count) entries"

$javaCount = (Get-ChildItem $sources -Recurse -Filter *.java -File -ErrorAction SilentlyContinue | Measure-Object).Count
if ($javaCount -eq 0) {
    throw "no decompiled java sources under $sources"
}
Write-Host "decompiled sources: $javaCount java files"

Write-Host "analysis workspace: ok ($AnalysisRoot)"
