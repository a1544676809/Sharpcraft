# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors

<#
.SYNOPSIS
    Compares two ModelProbe TSV outputs with a numeric tolerance.

.DESCRIPTION
    Rows are matched by the stable vertex key (layer, part, cube, polygon, vertex), not by file
    order. Position and UV values are compared numerically. This is the before/after check for a
    bake-refactor step: raw output should match exactly enough for the selected tolerance, and final
    block-space output should remain at the same world position.

.PARAMETER Reference
    The original ModelProbe TSV.

.PARAMETER Actual
    The new ModelProbe TSV.

.PARAMETER Tolerance
    Maximum absolute difference for x/y/z/u/v. Defaults to 0.000001.
#>
param(
    [Parameter(Mandatory = $true)][string]$Reference,
    [Parameter(Mandatory = $true)][string]$Actual,
    [double]$Tolerance = 0.000001
)

$ErrorActionPreference = 'Stop'

function Read-Probe($path) {
    $rows = @{}
    foreach ($line in [System.IO.File]::ReadLines($path)) {
        if (-not $line -or $line.StartsWith('#')) { continue }
        $parts = $line.Split("`t")
        if ($parts.Length -ne 10) {
            throw "bad ModelProbe row in $path`: $line"
        }

        $key = $parts[0..4] -join '|'
        $rows[$key] = [pscustomobject]@{
            Key = $key
            X = [double]$parts[5]
            Y = [double]$parts[6]
            Z = [double]$parts[7]
            U = [double]$parts[8]
            V = [double]$parts[9]
        }
    }
    return $rows
}

$referenceRows = Read-Probe $Reference
$actualRows = Read-Probe $Actual
$differences = [System.Collections.Generic.List[string]]::new()

foreach ($key in ($referenceRows.Keys | Sort-Object)) {
    if (-not $actualRows.ContainsKey($key)) {
        $differences.Add("missing: $key")
        continue
    }

    $expected = $referenceRows[$key]
    $actualValue = $actualRows[$key]
    $deltas = @(
        [Math]::Abs($expected.X - $actualValue.X),
        [Math]::Abs($expected.Y - $actualValue.Y),
        [Math]::Abs($expected.Z - $actualValue.Z),
        [Math]::Abs($expected.U - $actualValue.U),
        [Math]::Abs($expected.V - $actualValue.V)
    )
    if (($deltas | Measure-Object -Maximum).Maximum -gt $Tolerance) {
        $differences.Add(("delta {0}: expected ({1:F6},{2:F6},{3:F6},{4:F6},{5:F6}) actual ({6:F6},{7:F6},{8:F6},{9:F6},{10:F6})" -f `
            $key, $expected.X, $expected.Y, $expected.Z, $expected.U, $expected.V,
            $actualValue.X, $actualValue.Y, $actualValue.Z, $actualValue.U, $actualValue.V))
    }
}

foreach ($key in ($actualRows.Keys | Sort-Object)) {
    if (-not $referenceRows.ContainsKey($key)) {
        $differences.Add("extra: $key")
    }
}

if ($differences.Count -gt 0) {
    $differences | Select-Object -First 30 | ForEach-Object { Write-Host $_ }
    Write-Error "$($differences.Count) differences (tolerance $Tolerance)"
}

Write-Host "model probe matches: $($referenceRows.Count) vertices (tolerance $Tolerance)"
