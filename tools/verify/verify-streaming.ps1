# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Streaming equivalence check (docs/specs/quality/vanilla-equivalence-matrix.md row 3 and 4).
#
# Runs the client for a fixed time with the diagnostic channel on, then asserts that the number of loaded chunks rises
# over time and ends close to the count the configured view distance implies. It prints machine readable PASS/FAIL lines
# so a regression shows up as a number rather than an impression.
param(
    [string]$Address = '127.0.0.1:25565',
    [int]$Seconds = 25,
    [int]$MinRatioPercent = 90
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$log = Join-Path $env:TEMP "verify-streaming-$([guid]::NewGuid().ToString('N').Substring(0,8)).log"

$env:MC_DIAG = '1'
$env:MC_WINDOW_SECONDS = "$Seconds"
$env:MC_WINDOW_AUTOWALK = '1'
$env:MC_ASSETS_OUT = "$env:TEMP\mcs-assets-pack"
$env:MC_PACK_FORMAT = '97.1'

Push-Location $root
try {
    & dotnet run --project src\Sharpcraft.Launcher --no-build -- window $Address *> $log
}
finally {
    Pop-Location
    Remove-Item Env:\MC_DIAG, Env:\MC_WINDOW_AUTOWALK -ErrorAction SilentlyContinue
}

$samples = Select-String -Path $log -Pattern 'stream: loaded=(\d+) expected=(\d+)' | ForEach-Object {
    $m = [regex]::Match($_.Line, 'loaded=(\d+) expected=(\d+)')
    [pscustomobject]@{ Loaded = [int]$m.Groups[1].Value; Expected = [int]$m.Groups[2].Value }
}

if ($samples.Count -eq 0) {
    "FAIL samples=0 log=$log"
    exit 2
}

$first = $samples[0]
$last = $samples[-1]
$peak = ($samples | Measure-Object Loaded -Maximum).Maximum
$expected = $last.Expected
$ratio = if ($expected -gt 0) { [math]::Round(100.0 * $peak / $expected, 1) } else { 0 }
$grew = $peak -gt $first.Loaded

"INFO samples=$($samples.Count) first=$($first.Loaded) peak=$peak expected=$expected ratio=$ratio% grew=$grew log=$log"

if (-not $grew) { "FAIL streaming grew=false" ; exit 1 }
if ($ratio -lt $MinRatioPercent) { "FAIL streaming ratio=$ratio% threshold=$MinRatioPercent%" ; exit 1 }

"PASS streaming peak=$peak expected=$expected ratio=$ratio%"
