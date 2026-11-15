# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Proves this project decodes block states exactly as the running game does.

.DESCRIPTION
    Dumps every block state twice from the same source of truth:
      * vanilla.tsv  - taken from the game itself through truth/run.ps1
      * ours.tsv     - taken from this project's decoder
    and compares them line by line. The state ids are the input; the property values are the
    output. An empty diff means for the same ids we produce the same properties.

.PARAMETER Jar
    Path to the Minecraft version jar.

.PARAMETER Work
    Directory for the two dumps; defaults to a folder under the temp directory.
#>
param(
    [Parameter(Mandatory = $true)][string]$Jar,
    [string]$Work = (Join-Path ([System.IO.Path]::GetTempPath()) 'block-state-compare')
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $Work | Out-Null
$vanilla = Join-Path $Work 'vanilla.tsv'
$ours = Join-Path $Work 'ours.tsv'

& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'truth/run.ps1') -Jar $Jar -Out $vanilla
if ($LASTEXITCODE -ne 0) {
    throw "vanilla dump failed with exit code $LASTEXITCODE"
}

& dotnet run --project $PSScriptRoot -- $ours
if ($LASTEXITCODE -ne 0) {
    throw "project dump failed with exit code $LASTEXITCODE"
}

function Read-Data($path) {
    [System.IO.File]::ReadLines($path) | Where-Object { $_.Length -gt 0 -and $_[0] -ne '#' }
}

$vanillaLines = @(Read-Data $vanilla)
$ourLines = @(Read-Data $ours)

Write-Host "vanilla states: $($vanillaLines.Count)"
Write-Host "ours    states: $($ourLines.Count)"

$difference = Compare-Object -ReferenceObject $vanillaLines -DifferenceObject $ourLines
if ($difference) {
    Write-Host "MISMATCH: $($difference.Count) differing lines"
    $difference | Select-Object -First 10 | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

Write-Host "OK: every block state decodes identically"
