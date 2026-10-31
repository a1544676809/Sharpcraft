# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Frame time percentiles (docs/specs/quality/vanilla-equivalence-matrix.md row 15).
#
# Runs the window for a fixed time and asserts the ninety ninth percentile frame time against the target. The
# percentiles come from the renderer itself, printed at exit because the host asked for them.
param(
    [string]$Address = '127.0.0.1:25565',
    [int]$Seconds = 30,
    [double]$MaxP99Ms = 16.7,
    [switch]$Walk
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$log = Join-Path $env:TEMP "verify-perf-$([guid]::NewGuid().ToString('N').Substring(0,8)).log"

$env:MC_ASSETS_OUT = "$env:TEMP\mcs-assets-pack"
$env:MC_PACK_FORMAT = '97.1'
$env:MC_WINDOW_SECONDS = "$Seconds"
$env:MC_PERF_SUMMARY = '1'

if ($Walk) { $env:MC_WINDOW_AUTOWALK = '1' }

Push-Location $root
try {
    & dotnet run --project src\Sharpcraft.Launcher -c Release --no-build -- window $Address *> $log
}
finally {
    Pop-Location
    Remove-Item Env:\MC_WINDOW_AUTOWALK, Env:\MC_PERF_SUMMARY -ErrorAction SilentlyContinue
}

$perf = Select-String -Path $log -Pattern 'perf: frames=(\d+) mean=([\d.]+) p50=([\d.]+) p95=([\d.]+) p99=([\d.]+) max=([\d.]+) over33=(\d+)'
if (-not $perf) { "FAIL perf reason=no-summary log=$log"; exit 2 }

$m = $perf[-1].Matches[0]
$frames = [int]$m.Groups[1].Value
$mean = [double]$m.Groups[2].Value
$p50 = [double]$m.Groups[3].Value
$p95 = [double]$m.Groups[4].Value
$p99 = [double]$m.Groups[5].Value
$max = [double]$m.Groups[6].Value
$over33 = [int]$m.Groups[7].Value

$arena = Select-String -Path $log -Pattern 'arena (\d+) chunks'
$chunks = if ($arena) { [int]$arena[-1].Matches[0].Groups[1].Value } else { 0 }
$resolution = Select-String -Path $log -Pattern 'framebuffer (\d+x\d+)'
$size = if ($resolution) { $resolution[-1].Matches[0].Groups[1].Value } else { 'unknown' }

"INFO frames=$frames mean=$mean p50=$p50 p95=$p95 p99=$p99 max=$max over33=$over33 chunks=$chunks size=$size log=$log"

if ($frames -lt 100) { "FAIL perf frames=$frames"; exit 1 }
if ($p99 -gt $MaxP99Ms) { "FAIL perf p99=$p99 target=$MaxP99Ms"; exit 1 }

"PASS perf p99=$p99 target=$MaxP99Ms frames=$frames chunks=$chunks size=$size"
