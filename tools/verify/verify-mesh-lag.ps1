# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Client side mesh lag (docs/specs/quality/vanilla-equivalence-matrix.md row 3).
#
# The measurement must exclude the time the reference server spends sending chunks, so it works like this: the player
# stands still, the server sends the whole view once, and the script takes the moment the last chunk arrived and the
# moment the client finished drawing everything that had arrived. The difference is the client's own lag.
param(
    [string]$Address = '127.0.0.1:25565',
    [int]$Seconds = 30,
    [double]$MaxClientLagSeconds = 2.0
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$log = Join-Path $env:TEMP "verify-meshlag-$([guid]::NewGuid().ToString('N').Substring(0,8)).log"

$env:MC_ASSETS_OUT = "$env:TEMP\mcs-assets-pack"
$env:MC_PACK_FORMAT = '97.1'
$env:MC_DIAG = '1'
$env:MC_WINDOW_SECONDS = "$Seconds"
Remove-Item Env:\MC_WINDOW_AUTOWALK -ErrorAction SilentlyContinue

Push-Location $root
try {
    & dotnet run --project src\Sharpcraft.Launcher -c Release --no-build -- window $Address *> $log
}
finally {
    Pop-Location
    Remove-Item Env:\MC_DIAG -ErrorAction SilentlyContinue
}

$rows = Select-String -Path $log -Pattern 'streamrender: world=(\d+) arrived=(\d+) arrivalLast=([\d.]+) meshes=(\d+).*built=(\d+).*t=([\d.]+)s' | ForEach-Object {
    $m = $_.Matches[0]
    [pscustomobject]@{
        Arrived = [int]$m.Groups[2].Value
        Arrival = [double]$m.Groups[3].Value
        Meshes = [int]$m.Groups[4].Value
        Seconds = [double]$m.Groups[6].Value
    }
}

if ($rows.Count -lt 4) { "FAIL meshlag reason=no-telemetry samples=$($rows.Count) log=$log"; exit 2 }

$arrivedTotal = ($rows | Measure-Object -Property Arrived -Maximum).Maximum
$serverDone = ($rows | Measure-Object -Property Arrival -Maximum).Maximum
$atServerDone = $rows | Where-Object { $_.Arrival -ge $serverDone } | Select-Object -First 1
$caughtUp = ($rows | Where-Object { $_.Meshes -ge ($arrivedTotal - 2) } | Select-Object -First 1)

if (-not $caughtUp) {
    "FAIL meshlag reason=never-caught-up arrived=$arrivedTotal serverDone=$serverDone log=$log"
    exit 1
}

$clientLag = [math]::Round($caughtUp.Seconds - $serverDone, 1)
$backlog = $arrivedTotal - $atServerDone.Meshes

"INFO arrived=$arrivedTotal serverDone=${serverDone}s meshesWhenServerDone=$($atServerDone.Meshes) backlog=$backlog caughtUp=$($caughtUp.Seconds)s clientLag=${clientLag}s log=$log"

if ($clientLag -gt $MaxClientLagSeconds) {
    "FAIL meshlag clientLag=${clientLag}s max=$MaxClientLagSeconds backlog=$backlog"
    exit 1
}

"PASS meshlag clientLag=${clientLag}s max=$MaxClientLagSeconds arrived=$arrivedTotal"
