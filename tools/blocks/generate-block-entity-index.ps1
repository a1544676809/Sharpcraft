#!/usr/bin/env pwsh
# SPDX-License-Identifier: LGPL-3.0-or-later
# Copyright (C) 2026 Sharpcraft contributors
#
# Regenerates src/Sharpcraft.Common/World/Blocks/BlockEntityBlockIndex.cs from the reference's block
# entity type registration data. The output holds block ids only, never reference code.
#
#   pwsh -NoProfile -File tools/blocks/generate-block-entity-index.ps1            # write
#   pwsh -NoProfile -File tools/blocks/generate-block-entity-index.ps1 -Check     # verify only
#
# Inputs:
#   $env:MC_ANALYSIS_ROOT  analysis workspace (default: sibling ../mc-26.3-analysis)
#   $env:SHARPCRAFT_PACK   extracted pack used to enumerate real block ids
#                          (default: newest minecraft-*-*.pack.zip under the local app data)

[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$analysis = if ($env:MC_ANALYSIS_ROOT) { $env:MC_ANALYSIS_ROOT } else { Join-Path (Split-Path -Parent $repo) 'mc-26.3-analysis' }
$source = Get-ChildItem $analysis -Recurse -Filter 'BlockEntityTypes.java' | Select-Object -First 1
if (-not $source) { throw "BlockEntityTypes.java not found under $analysis" }

$pack = $env:SHARPCRAFT_PACK
if (-not $pack) {
    $pack = (Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Sharpcraft\packs') -Filter 'minecraft-*.pack.zip' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if (-not $pack) { throw 'no extracted pack found; set SHARPCRAFT_PACK' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($pack)
try {
    $blockIds = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '^assets/minecraft/blockstates/(.+)\.json$') {
            [void]$blockIds.Add($Matches[1])
        }
    }
}
finally {
    $zip.Dispose()
}

# Walk the registration list: every register(...) names a type id and the blocks that carry it.
# A token suffixed with .asList() is a family (sixteen banners, every wood shelf) whose members are
# found by matching the block ids the pack actually has, so no colour or wood list is hard coded.
$types = [ordered]@{}
$current = $null
$depth = 0
foreach ($line in Get-Content $source.FullName) {
    if ($null -eq $current) {
        if ($line -match 'BlockEntityTypeIds\.([A-Z_]+)') {
            $current = $Matches[1]
            $depth = 0
            $types[$current] = [System.Collections.Generic.List[string]]::new()
        }
        else {
            continue
        }
    }

    foreach ($m in [regex]::Matches($line, 'Blocks\.([A-Z0-9_]+)(\.asList\(\))?')) {
        $isFamily = $m.Groups[2].Success
        [void]$types[$current].Add(('{0}|{1}' -f $m.Groups[1].Value, $isFamily))
    }

    $depth += ([regex]::Matches($line, '\(').Count - [regex]::Matches($line, '\)').Count)
    if ($depth -le 0 -and $line -match ';\s*$') { $current = $null }
}

$pairs = [System.Collections.Generic.Dictionary[string, string]]::new()
$unmapped = [System.Collections.Generic.List[string]]::new()
foreach ($type in $types.Keys) {
    foreach ($token in ($types[$type] | Select-Object -Unique)) {
        $parts = $token -split '\|'
        $name = $parts[0]
        $isFamily = $parts[1] -eq 'True'
        $base = $name.ToLowerInvariant()

        if (-not $isFamily) {
            if ($blockIds.Contains($base)) { $pairs[$base] = $type } else { [void]$unmapped.Add("$name ($type)") }
            continue
        }

        $suffix = if ($base -like 'dyed_*') { $base.Substring(5) } else { $base }
        $members = @($blockIds | Where-Object { $_ -like "*_$suffix" -or $_ -eq $suffix })
        if ($members.Count -eq 0) { [void]$unmapped.Add("$name ($type)"); continue }
        foreach ($member in $members) {
            if ($pairs.ContainsKey($member) -and $pairs[$member] -ne $type) {
                throw "block $member claimed by both $($pairs[$member]) and $type"
            }

            $pairs[$member] = $type
        }
    }
}

if ($unmapped.Count -gt 0) { throw "unmapped registration entries: $($unmapped -join ', ')" }

# Type ids are the order of VanillaBlockEntityTypes.Paths, which mirrors the reference's registry order.
$record = Join-Path $repo 'src/Sharpcraft.Common/World/BlockEntityRecord.cs'
$text = Get-Content $record -Raw
$list = [regex]::Match($text, '(?s)private static readonly string\[\] Paths\s*=\s*\[(.*?)\];')
if (-not $list.Success) { throw 'VanillaBlockEntityTypes.Paths not found' }
$names = [regex]::Matches($list.Groups[1].Value, '"([a-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value }
$typeIds = @{}
for ($i = 0; $i -lt $names.Count; $i++) { $typeIds[$names[$i].ToUpperInvariant()] = $i }

$rows = $pairs.Keys | Sort-Object | ForEach-Object {
    $type = $pairs[$_]
    if (-not $typeIds.ContainsKey($type)) { throw "unknown block entity type $type for block $_" }
    [pscustomobject]@{ Path = $_; Id = $typeIds[$type] }
}

$builder = [System.Text.StringBuilder]::new()
$header = @(
    '// SPDX-License-Identifier: LGPL-3.0-or-later',
    '// Copyright (C) 2026 Sharpcraft contributors',
    '//',
    '// Generated by tools/blocks/generate-block-entity-index.ps1 from the reference''s block entity',
    '// type registration data. Do not edit by hand; rerun the generator instead.',
    '',
    'namespace Sharpcraft.Common.World.Blocks;',
    '',
    '/// <summary>',
    '/// Every block that carries a block entity, and which type it is. Generated from the reference''s',
    '/// block entity type registration data; do not edit by hand. Issue 96.',
    '/// </summary>',
    '/// <remarks>',
    '/// The reference decides this with an interface check (a block implements <c>EntityBlock</c>) because',
    '/// its blocks are objects. Ours are registry ids, so the same fact is stored as data. One entry per',
    '/// block, not per type: a type covers many blocks (sixteen banners, sixteen wall banners, every sign,',
    '/// head and shelf variant). See docs/decisions/0031-block-behaviour-layering.md.',
    '/// </remarks>',
    '[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.BlockEntityType", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/BlockEntityType.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "the valid blocks of every registered type, as data")]',
    'public static class BlockEntityBlockIndex',
    '{',
    '    /// <summary>Block paths, sorted, so a lookup is a binary search.</summary>',
    '    private static readonly string[] Paths =',
    '    ['
)
foreach ($line in $header) { [void]$builder.AppendLine($line) }
foreach ($row in $rows) { [void]$builder.AppendLine("        `"$($row.Path)`",") }
[void]$builder.AppendLine('    ];')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    /// <summary>Block entity type id of the entry at the same position in <see cref="Paths"/>.</summary>')
[void]$builder.AppendLine('    private static readonly byte[] TypeIds =')
[void]$builder.AppendLine('    [')
$line = '       '
foreach ($row in $rows) {
    $line += " $($row.Id),"
    if ($line.Length -gt 96) { [void]$builder.AppendLine($line); $line = '       ' }
}
if ($line.Trim()) { [void]$builder.AppendLine($line) }
[void]$builder.AppendLine('    ];')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    /// <summary>How many blocks carry a block entity.</summary>')
[void]$builder.AppendLine('    public static int Count => Paths.Length;')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    /// <summary>The block paths this index covers, sorted. Read only; used by tests and tools.</summary>')
[void]$builder.AppendLine('    public static System.Collections.Generic.IReadOnlyList<string> BlockPaths => Paths;')
[void]$builder.AppendLine()
[void]$builder.AppendLine('    /// <summary>Block entity type of a block path.</summary>')
[void]$builder.AppendLine('    /// <param name="blockPath">Block id path, without the namespace.</param>')
[void]$builder.AppendLine('    /// <param name="typeId">Type id when the block carries one.</param>')
[void]$builder.AppendLine('    /// <returns>True when the block carries a block entity.</returns>')
[void]$builder.AppendLine('    public static bool TryGetType(string blockPath, out int typeId)')
[void]$builder.AppendLine('    {')
[void]$builder.AppendLine('        var low = 0;')
[void]$builder.AppendLine('        var high = Paths.Length - 1;')
[void]$builder.AppendLine('        while (low <= high)')
[void]$builder.AppendLine('        {')
[void]$builder.AppendLine('            var middle = (low + high) >> 1;')
[void]$builder.AppendLine('            var order = string.CompareOrdinal(Paths[middle], blockPath);')
[void]$builder.AppendLine('            if (order == 0)')
[void]$builder.AppendLine('            {')
[void]$builder.AppendLine('                typeId = TypeIds[middle];')
[void]$builder.AppendLine('                return true;')
[void]$builder.AppendLine('            }')
[void]$builder.AppendLine()
[void]$builder.AppendLine('            if (order < 0)')
[void]$builder.AppendLine('            {')
[void]$builder.AppendLine('                low = middle + 1;')
[void]$builder.AppendLine('            }')
[void]$builder.AppendLine('            else')
[void]$builder.AppendLine('            {')
[void]$builder.AppendLine('                high = middle - 1;')
[void]$builder.AppendLine('            }')
[void]$builder.AppendLine('        }')
[void]$builder.AppendLine()
[void]$builder.AppendLine('        typeId = -1;')
[void]$builder.AppendLine('        return false;')
[void]$builder.AppendLine('    }')
[void]$builder.AppendLine('}')

$target = Join-Path $repo 'src/Sharpcraft.Common/World/Blocks/BlockEntityBlockIndex.cs'
$generated = $builder.ToString()
if ($Check) {
    $existing = if (Test-Path $target) { Get-Content $target -Raw } else { '' }
    if ($existing.TrimEnd() -ne $generated.TrimEnd()) {
        Write-Error 'BlockEntityBlockIndex.cs is stale; rerun without -Check'
        exit 1
    }

    Write-Host "block entity index: ok ($($rows.Count) blocks)"
    exit 0
}

Set-Content -Path $target -Value $generated -NoNewline
Write-Host "block entity index: wrote $($rows.Count) blocks"
