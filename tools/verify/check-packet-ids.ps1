# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Packet id equivalence check (docs/specs/quality/vanilla-equivalence-matrix.md row 1).
#
# The reference's own registration dump lists every play packet id. This script compares our ProtocolConstants against it
# and fails on any constant that disagrees, because a wrong id silently routes a packet into the "unhandled" branch.
param(
    [string]$AnalysisRoot = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($AnalysisRoot)) {
    $AnalysisRoot = if ($env:MC_ANALYSIS_ROOT) { $env:MC_ANALYSIS_ROOT } else { Join-Path (Split-Path -Parent $root) 'mc-26.3-analysis' }
}

$table = Join-Path $AnalysisRoot 'vectors/packet-tables.txt'
$constants = Join-Path $root 'src/Sharpcraft.Common/Network/ProtocolConstants.cs'

if (-not (Test-Path $table)) { "SKIP reason=no-reference-table path=$table"; exit 3 }

# region -> id -> name from the reference dump
$reference = @{}
foreach ($line in Get-Content $table) {
    $m = [regex]::Match($line, '(handshake|status|login|configuration|play)\s+(serverbound|clientbound)\s+(\d+)\s+minecraft:(\w+)')
    if (-not $m.Success) { continue }
    $key = "$($m.Groups[1].Value)/$($m.Groups[2].Value)"
    if (-not $reference.ContainsKey($key)) { $reference[$key] = @{} }
    $reference[$key][$m.Groups[4].Value] = [int]$m.Groups[3].Value
}

# our constants, grouped by the enclosing class
$text = Get-Content $constants -Raw
$classMap = @{
    'PlayClientbound'          = 'play/clientbound'
    'PlayServerbound'          = 'play/serverbound'
    'ConfigurationClientbound' = 'configuration/clientbound'
    'ConfigurationServerbound' = 'configuration/serverbound'
    'LoginClientbound'         = 'login/clientbound'
    'LoginServerbound'         = 'login/serverbound'
    'StatusClientbound'        = 'status/clientbound'
    'StatusServerbound'        = 'status/serverbound'
}

function ConvertTo-Snake([string]$name) {
    (($name -creplace '([a-z0-9])([A-Z])', '$1_$2' -creplace '([A-Z]+)([A-Z][a-z])', '$1_$2')).ToLowerInvariant()
}

$mismatches = 0
$checked = 0
foreach ($klass in $classMap.Keys) {
    $start = $text.IndexOf("public static class $klass")
    if ($start -lt 0) { continue }
    $next = $text.IndexOf('public static class ', $start + 10)
    $body = if ($next -gt $start) { $text.Substring($start, $next - $start) } else { $text.Substring($start) }
    $region = $classMap[$klass]
    if (-not $reference.ContainsKey($region)) { continue }

    foreach ($m in [regex]::Matches($body, 'public const int (\w+) = (\d+);')) {
        $name = $m.Groups[1].Value
        $value = [int]$m.Groups[2].Value
        $snake = ConvertTo-Snake $name
        if (-not $reference[$region].ContainsKey($snake)) { continue }
        $checked++
        $expected = $reference[$region][$snake]
        if ($expected -ne $value) {
            $mismatches++
            "FAIL packet-id $region/${snake} ours=$value reference=$expected"
        }
    }
}

"INFO checked=$checked mismatches=$mismatches"
if ($mismatches -gt 0) { exit 1 }
"PASS packet-ids checked=$checked"
