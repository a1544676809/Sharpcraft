# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Turns a BlockStateDump.tsv into the per-block property schema the decoder loads.

.DESCRIPTION
    The dump lists one line per state. This collapses it to one line per property:
    block, property name, and the property's values in the order the registry enumerates them.
    That order matters: it is not alphabetical for enums, and the state encoding depends on it.

.PARAMETER Dump
    Path of a file produced by BlockStateDump.

.PARAMETER Out
    Path of the schema to write.
#>
param(
    [Parameter(Mandatory = $true)][string]$Dump,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Dump)) {
    throw "dump not found: $Dump"
}

$blocks = [ordered]@{}
foreach ($line in [System.IO.File]::ReadLines($Dump)) {
    if ($line.Length -eq 0 -or $line[0] -eq '#') {
        continue
    }

    $parts = $line.Split("`t")
    $blockId = $parts[1]
    if (-not $blocks.Contains($blockId)) {
        $blocks[$blockId] = [ordered]@{}
    }

    $properties = $blocks[$blockId]
    for ($i = 2; $i -lt $parts.Length; $i++) {
        $pair = $parts[$i].Split('=', 2)
        if (-not $properties.Contains($pair[0])) {
            $properties[$pair[0]] = [System.Collections.Generic.List[string]]::new()
        }

        if (-not $properties[$pair[0]].Contains($pair[1])) {
            $properties[$pair[0]].Add($pair[1])
        }
    }
}

$writer = [System.IO.StreamWriter]::new($Out, $false, [System.Text.UTF8Encoding]::new($false))
try {
    $writer.NewLine = "`n"
    $writer.WriteLine('# SPDX-License-Identifier: LGPL-3.0-or-later')
    $writer.WriteLine('# Block state property schema of Minecraft 26.3, derived from the runtime registry.')
    $writer.WriteLine('# blockId	property	value1,value2,...')
    foreach ($blockId in $blocks.Keys) {
        foreach ($property in $blocks[$blockId].Keys) {
            $writer.WriteLine("$blockId`t$property`t$($blocks[$blockId][$property] -join ',')")
        }
    }
}
finally {
    $writer.Dispose()
}

Write-Host "wrote $Out"
