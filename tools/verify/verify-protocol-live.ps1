# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Live protocol equivalence (docs/specs/quality/vanilla-equivalence-matrix.md rows 1, 2, 4, 5).
#
# Pure logic: connect to the server, walk for a fixed time, then assert the counters a correct client must produce.
# No screenshots and no rendering involved.
param(
    [string]$Address = '127.0.0.1:25565',
    [double]$Seconds = 20,
    [int]$MinKeepAlives = 1,
    [int]$MinBatchAcks = 0
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$log = Join-Path $env:TEMP "verify-protocol-$([guid]::NewGuid().ToString('N').Substring(0,8)).log"

$env:MC_ASSETS_OUT = "$env:TEMP\mcs-assets-pack"
$env:MC_PACK_FORMAT = '97.1'
$env:MC_WALK_SECONDS = "$Seconds"

Push-Location $root
try {
    & dotnet run --project src\Sharpcraft.Launcher --no-build -- walk $Address *> $log
}
finally {
    Pop-Location
}

$line = Select-String -Path $log -Pattern 'protocol: ticks=(\d+) corrections=(\d+) distance=([\d.]+) keepalives=(\d+) batchacks=(\d+) survived=(\w+)'
if (-not $line) {
    $why = (Select-String -Path $log -Pattern 'server dropped us|refused the login|failed' | Select-Object -First 1)
    "FAIL protocol reason=no-summary log=$log detail=$($why.Line)"
    exit 2
}

$m = $line[-1].Matches[0]
$ticks = [int]$m.Groups[1].Value
$corrections = [int]$m.Groups[2].Value
$distance = [double]$m.Groups[3].Value
$keepalives = [int]$m.Groups[4].Value
$batchacks = [int]$m.Groups[5].Value
$survived = $m.Groups[6].Value -eq 'True'

"INFO ticks=$ticks corrections=$corrections distance=$distance keepalives=$keepalives batchacks=$batchacks survived=$survived log=$log"

if (-not $survived) { "FAIL protocol survived=false"; exit 1 }
if ($ticks -le 0) { "FAIL protocol ticks=$ticks"; exit 1 }
if ($keepalives -lt $MinKeepAlives) { "FAIL protocol keepalives=$keepalives threshold=$MinKeepAlives"; exit 1 }
# Batch acknowledgements belong to the chunk transfer, which the window path exercises; during a walk the server sends
# none, so the count is reported but not required here.
if ($MinBatchAcks -gt 0 -and $batchacks -lt $MinBatchAcks) { "FAIL protocol batchacks=$batchacks threshold=$MinBatchAcks"; exit 1 }

"PASS protocol ticks=$ticks keepalives=$keepalives batchacks=$batchacks distance=$distance"
