# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Play Clientbound coverage audit (docs/analysis/protocol/play-clientbound-coverage.md).
# Lists reference play clientbound packets with NO `case PacketIds.PlayClientbound.*` in InteractiveSession.
# Informational (not a hard gate): prints the unhandled set so the gap is always visible.
param([string]$AnalysisRoot = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($AnalysisRoot)) {
    if ($env:MC_ANALYSIS_ROOT) { $AnalysisRoot = $env:MC_ANALYSIS_ROOT } else {
        $d = [System.IO.DirectoryInfo]$root
        while ($d -ne $null) { $cand = Join-Path ($(if ($d.Parent) { $d.Parent.FullName } else { $d.FullName })) 'mc-26.3-analysis'; if (Test-Path $cand) { $AnalysisRoot = $cand; break }; $d = $d.Parent }
        if (-not $AnalysisRoot) { $AnalysisRoot = Join-Path (Split-Path -Parent $root) 'mc-26.3-analysis' }
    }
}
$table = Join-Path $AnalysisRoot 'vectors/packet-tables.txt'
if (-not (Test-Path $table)) { "SKIP reason=no-reference-table path=$table"; exit 3 }

$ref = @()
foreach ($line in Get-Content $table) {
    $m = [regex]::Match($line, '(?:handshaking|status|login|configuration|play)\s+(serverbound|clientbound)\s+(\d+)\s+minecraft:(\w+)')
    if ($m.Success -and $line -match '\bplay\s+clientbound') { $ref += [pscustomobject]@{ Id = [int]$m.Groups[2].Value; Key = $m.Groups[3].Value } }
}

function ConvertTo-Snake([string]$n) { ((($n -creplace '([a-z0-9])([A-Z])', '$1_$2') -creplace '([A-Z]+)([A-Z][a-z])', '$1_$2')).ToLowerInvariant() }
$handled = @{}
Select-String -Path (Join-Path $root 'src/Sharpcraft.Launcher/InteractiveSession.cs') -Pattern 'case PacketIds\.PlayClientbound\.(\w+):' | ForEach-Object {
    $m = [regex]::Match($_.Line, 'PlayClientbound\.(\w+)'); if ($m.Success) { $handled[(ConvertTo-Snake $m.Groups[1].Value)] = $true }
}
$missing = $ref | Where-Object { -not $handled.ContainsKey($_.Key) } | Sort-Object Id
"reference=$($ref.Count) handled=$($handled.Count) missing=$($missing.Count)"
$missing | ForEach-Object { '{0,3}  {1}' -f $_.Id, $_.Key }
exit 0

