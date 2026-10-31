# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Chunk batch acknowledgement (docs/specs/quality/vanilla-equivalence-matrix.md row 4).
#
# The server sends one batch and waits for ChunkBatchReceived before sending the next, so a session that never answers
# stalls after the first batch. This asks for a large preload and requires that far more than one batch arrives.
param(
    [string]$Address = '127.0.0.1:25565',
    [int]$CollectSeconds = 15,
    [int]$TargetChunks = 200,
    [int]$MinChunks = 40
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$log = Join-Path $env:TEMP "verify-batch-$([guid]::NewGuid().ToString('N').Substring(0,8)).log"

$env:MC_ASSETS_OUT = "$env:TEMP\mcs-assets-pack"
$env:MC_PACK_FORMAT = '97.1'
$env:MC_WALK_COLLECT = "$CollectSeconds"
$env:MC_WALK_SECONDS = '2'
$env:MC_START_CHUNKS = "$TargetChunks"

Push-Location $root
try {
    & dotnet run --project src\Sharpcraft.Launcher -c Release --no-build -- walk $Address *> $log
}
finally {
    Pop-Location
    Remove-Item Env:\MC_WALK_COLLECT, Env:\MC_START_CHUNKS -ErrorAction SilentlyContinue
}

$chunks = Select-String -Path $log -Pattern 'chunks: (\d+) ok, (\d+) failed'
if (-not $chunks) { "FAIL batch reason=no-summary log=$log"; exit 2 }

$ok = [int]$chunks[-1].Matches[0].Groups[1].Value
$failed = [int]$chunks[-1].Matches[0].Groups[2].Value

$packets = Select-String -Path $log -Pattern 'packets: '
$finished = 0
if ($packets) {
    $m = [regex]::Match($packets[-1].Line, '(?<![0-9])11x(\d+)')
    if ($m.Success) { $finished = [int]$m.Groups[1].Value }
}

"INFO chunks=$ok failed=$failed batchFinished=$finished target=$TargetChunks log=$log"

if ($failed -gt 0) { "FAIL batch failed=$failed"; exit 1 }
if ($ok -lt $MinChunks) { "FAIL batch chunks=$ok minimum=$MinChunks (a session that does not acknowledge stalls at nine)"; exit 1 }

"PASS batch chunks=$ok batchFinished=$finished"
