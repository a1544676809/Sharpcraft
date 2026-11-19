# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Runs the hand-written ModelProbe against a local Minecraft 26.3 jar.

.DESCRIPTION
    This is the local reference-sampling path used when the external analysis repository is not
    available. The probe is compiled into a temporary directory and its output goes outside the
    repository by default. Nothing from the game (source or assets) is written into the repository.

    At least one ModelLayers field is required, for example CONDUIT_SHELL, STANDING_BANNER,
    STANDING_BANNER_FLAG or COPPER_GOLEM. Options are passed to the Java probe in order.

.PARAMETER Layer
    One or more ModelLayers field names, comma-separated.

.PARAMETER Out
    Output TSV path. Defaults to a file under the system temp directory.

.PARAMETER AnalysisRoot
    Analysis workspace root. Required: pass this argument or set MC_ANALYSIS_ROOT. No implicit
    machine-specific path is assumed; ref://mc-26.3-analysis resolves to this value.

.PARAMETER Pose
    Zero or more part poses: partPath=rx,ry,rz (radians). Use "root=..." for the model root.

.PARAMETER ModelScale
    Scale applied to the model vertices before the transform chain. ModelPart coordinates are in
    model pixels; use 0.0625 to obtain block-space vertices. Defaults to 1 (raw model pixels).

.PARAMETER Transform
    Zero or more transform-chain operations: translate:x,y,z, scale:x,y,z, rotX:deg, rotY:deg,
    rotZ:deg. They are applied in the order supplied.

.EXAMPLE
    ./run-model-probe.ps1 -Layer CONDUIT_SHELL -Out $env:TEMP/conduit-raw.tsv

.EXAMPLE
    ./run-model-probe.ps1 -Layer CONDUIT_SHELL -Transform translate:0.5,0.5,0.5 `
        -Transform scale:1,1,1 -Out $env:TEMP/conduit-world.tsv
#>
param(
    [Parameter(Mandatory = $true)][string]$Layer,
    [string]$Out = (Join-Path ([System.IO.Path]::GetTempPath()) 'sharpcraft-model-probe.tsv'),
    [string]$AnalysisRoot = '',
    [string[]]$Pose = @(),
    [double]$ModelScale = 1.0,
    [string[]]$Transform = @()
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
if (-not (Test-Path $jar)) {
    throw "client jar not found: $jar"
}

$classpathFile = Join-Path $AnalysisRoot 'vectors\cp.txt'
if (-not (Test-Path $classpathFile)) {
    throw "classpath file not found: $classpathFile (run tools/setup_workspace.py in the analysis workspace)"
}
$classpath = (Get-Content $classpathFile -Raw).Trim()
if (-not $classpath) {
    throw "classpath file is empty: $classpathFile"
}

$outPath = [System.IO.Path]::GetFullPath($Out)
$outDirectory = Split-Path -Parent $outPath
if ($outDirectory) {
    New-Item -ItemType Directory -Force -Path $outDirectory | Out-Null
}

$build = Join-Path ([System.IO.Path]::GetTempPath()) ('sharpcraft-model-probe-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $build | Out-Null
try {
    $source = Join-Path $PSScriptRoot 'ModelProbe.java'
    & javac -cp $classpath -d $build $source
    if ($LASTEXITCODE -ne 0) {
        throw "javac failed with exit code $LASTEXITCODE"
    }

    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add($outPath)
    $arguments.Add($Layer)
    foreach ($poseValue in $Pose) {
        $arguments.Add('--pose')
        $arguments.Add($poseValue)
    }
    if ($ModelScale -ne 1.0) {
        $arguments.Add('--model-scale')
        $arguments.Add($ModelScale.ToString([System.Globalization.CultureInfo]::InvariantCulture))
    }
    foreach ($transformValue in $Transform) {
        $arguments.Add('--transform')
        $arguments.Add($transformValue)
    }

    & java -Xmx2G -cp ($build + ';' + $classpath) ModelProbe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "ModelProbe failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item -Recurse -Force $build -ErrorAction SilentlyContinue
}

Write-Host "model probe: $outPath"
