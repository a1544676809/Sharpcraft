# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Live protocol and movement equivalence (docs/specs/quality/vanilla-equivalence-matrix.md rows 1, 2, 4, 5).
#
# Pure logic: connect, walk for a fixed time, then assert the counters and the walking speed a correct client must show.
# The speed is computed from the once per second position trace, so the join teleport and the start up are excluded and the
# figure is comparable with the reference walking speed of 4.317 blocks per second.
param(
    [string]$Address = '127.0.0.1:25565',
    [double]$Seconds = 20,
    [int]$MinKeepAlives = 1,
    [double]$ReferenceSpeed = 4.317,
    [double]$SpeedTolerance = 0.25,
    [int]$CollectSeconds = -1,
    [int]$MaxCorrections = 1
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$log = Join-Path $env:TEMP "verify-protocol-$([guid]::NewGuid().ToString('N').Substring(0,8)).log"

$env:MC_ASSETS_OUT = "$env:TEMP\mcs-assets-pack"
$env:MC_PACK_FORMAT = '97.1'
$env:MC_WALK_SECONDS = "$Seconds"
$env:MC_WALK_QUIET = '0'
if ($CollectSeconds -ge 0) { $env:MC_WALK_COLLECT = "$CollectSeconds" }

Push-Location $root
try {
    & dotnet run --project src\Sharpcraft.Launcher -c Release --no-build -- walk $Address *> $log
}
finally {
    Pop-Location
    Remove-Item Env:\MC_WALK_COLLECT -ErrorAction SilentlyContinue
}

$summary = Select-String -Path $log -Pattern 'protocol: ticks=(\d+) corrections=(\d+) distance=([\d.]+) keepalives=(\d+) batchacks=(\d+) survived=(\w+)'
if (-not $summary) {
    $why = (Select-String -Path $log -Pattern 'server dropped us|refused the login|chunk collection failed' | Select-Object -First 1)
    "FAIL protocol reason=no-summary log=$log detail=$($why.Line)"
    exit 2
}

$m = $summary[-1].Matches[0]
$ticks = [int]$m.Groups[1].Value
$corrections = [int]$m.Groups[2].Value
$distance = [double]$m.Groups[3].Value
$keepalives = [int]$m.Groups[4].Value
$batchacks = [int]$m.Groups[5].Value
$survived = $m.Groups[6].Value -eq 'True'

$trace = Select-String -Path $log -Pattern 't=([\d.]+) at \((-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\)' | ForEach-Object {
    $t = [regex]::Match($_.Line, 't=([\d.]+) at \((-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\)')
    [pscustomobject]@{
        Seconds = [double]$t.Groups[1].Value
        X = [double]$t.Groups[2].Value
        Y = [double]$t.Groups[3].Value
        Z = [double]$t.Groups[4].Value
    }
}

if ($trace.Count -lt 2) {
    "FAIL speed reason=no-trace samples=$($trace.Count) log=$log"
    exit 2
}

$first = $trace[0]
$last = $trace[-1]
$dx = $last.X - $first.X
$dz = $last.Z - $first.Z
$span = $last.Seconds - $first.Seconds
$speed = if ($span -gt 0) { [math]::Round([math]::Sqrt(($dx * $dx) + ($dz * $dz)) / $span, 3) } else { 0 }

"INFO ticks=$ticks corrections=$corrections distance=$distance keepalives=$keepalives batchacks=$batchacks speed=$speed samples=$($trace.Count) span=$span log=$log"

if (-not $survived) { "FAIL protocol survived=false"; exit 1 }
if ($ticks -le 0) { "FAIL protocol ticks=$ticks"; exit 1 }

# One correction is the join teleport. Any more means the server pulled the player back during the walk, which is what a
# client that does not get its first movements accepted looks like.
if ($corrections -gt $MaxCorrections) { "FAIL corrections=$corrections max=$MaxCorrections"; exit 1 }
if ($keepalives -lt $MinKeepAlives) { "FAIL protocol keepalives=$keepalives threshold=$MinKeepAlives"; exit 1 }
if ([math]::Abs($speed - $ReferenceSpeed) -gt $SpeedTolerance) {
    "FAIL speed=$speed reference=$ReferenceSpeed tolerance=$SpeedTolerance"
    exit 1
}

"PASS protocol ticks=$ticks keepalives=$keepalives speed=$speed distance=$distance"
