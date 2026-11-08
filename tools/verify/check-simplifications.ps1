# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Simplification registration gate (AGENTS.md principle 15).
#
# Code that deliberately simplifies vanilla behavior carries [Simplification("ID", "TASK")].
# Every active marker must have a matching machine-readable entry in docs/modules:
#   <!-- simplification:ID task:TASK -->
# The documentation entry stays after the code marker is removed when the simplification is resolved.

param(
    [string]$Root = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

$sourceMarker = [regex]'\[(?:[A-Za-z0-9_.]+\.)?Simplification\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"'
$docMarker = [regex]'<!--\s*simplification:([A-Za-z0-9_.-]+)\s+task:([A-Za-z0-9_.-]+)\s*-->'

$active = @{}
foreach ($file in Get-ChildItem (Join-Path $Root 'src') -Recurse -File -Filter '*.cs') {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        foreach ($match in $sourceMarker.Matches($line)) {
            $id = $match.Groups[1].Value
            $task = $match.Groups[2].Value
            $active[$id] = [pscustomobject]@{
                Task = $task
                File = $file.FullName.Substring($Root.Length).TrimStart('\', '/')
                Line = $lineNumber
            }
        }
    }
}

$documented = @{}
foreach ($file in Get-ChildItem (Join-Path $Root 'docs/modules') -Recurse -File -Filter '*.md') {
    $lineNumber = 0
    $inFence = $false
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        # Fenced code blocks hold examples, not registrations.
        if ($line.TrimStart().StartsWith('```')) {
            $inFence = -not $inFence
            continue
        }

        if ($inFence) { continue }

        foreach ($match in $docMarker.Matches($line)) {
            $id = $match.Groups[1].Value
            $documented[$id] = [pscustomobject]@{
                Task = $match.Groups[2].Value
                File = $file.FullName.Substring($Root.Length).TrimStart('\', '/')
                Line = $lineNumber
            }
        }
    }
}

$violations = 0
foreach ($id in $active.Keys) {
    $marker = $active[$id]
    if (-not $documented.ContainsKey($id)) {
        $violations++
        "FAIL simplification:$id $($marker.File):$($marker.Line) has no docs/modules marker (task $($marker.Task))"
        continue
    }

    if ($documented[$id].Task -ne $marker.Task) {
        $violations++
        "FAIL simplification:$id $($marker.File):$($marker.Line) task=$($marker.Task) but documentation says $($documented[$id].Task)"
    }
}

foreach ($id in $documented.Keys) {
    if (-not $active.ContainsKey($id)) {
        "INFO resolved simplification:$id retained in $($documented[$id].File):$($documented[$id].Line)"
    }
}

"INFO active=$($active.Count) documented=$($documented.Count) violations=$violations rule=simplification-registration"
if ($violations -gt 0) { exit 1 }
'PASS simplification-registration'
