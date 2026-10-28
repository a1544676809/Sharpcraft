// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Globalization;
using MinecraftCSharp.Common.Identifiers;

namespace MinecraftCSharp.Common.Registries;

/// <summary>
/// The vanilla 26.3 block registry layout, built from an embedded snapshot of registration order and
/// state counts. Global block state ids are assigned exactly as vanilla does — blocks in registration
/// order, each owning a contiguous range of its state count — so a state id observed on the wire maps
/// to the same block the official client would use
/// (docs/analysis/common/registry-order.md §5).
/// </summary>
/// <remarks>
/// The snapshot carries identifiers and counts only: no assets, no decompiled code. Servers that add
/// or reorder blocks will disagree with it, which is why the live <c>registry_data</c> packet remains
/// the authority when it is available (T-039).
/// </remarks>
public static class VanillaBlockRegistry
{
    /// <summary>Resource name of the embedded snapshot.</summary>
    public const string SnapshotResourceName = "MinecraftCSharp.Common.Registries.Data.vanilla-block-states-26.3.tsv";

    /// <summary>Total number of blocks in the baseline registry.</summary>
    public const int BlockCount = 1286;

    /// <summary>Total number of global block states in the baseline registry.</summary>
    public const int BlockStateCount = 35723;

    private static readonly Lazy<BlockStateLayout> CachedLayout = new(LoadLayout, isThreadSafe: true);

    /// <summary>The baseline layout, parsed once.</summary>
    public static BlockStateLayout Layout => CachedLayout.Value;

    /// <summary>Reads the snapshot into a layout.</summary>
    /// <exception cref="InvalidDataException">The embedded snapshot is missing or malformed.</exception>
    public static BlockStateLayout LoadLayout()
    {
        var assembly = typeof(VanillaBlockRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(SnapshotResourceName)
            ?? throw new InvalidDataException($"embedded block snapshot not found: {SnapshotResourceName}");

        using var reader = new StreamReader(stream);
        var blocks = new List<(Identifier Id, int StateCount)>(BlockCount);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length < 3)
            {
                throw new InvalidDataException($"block snapshot line has {fields.Length} fields: '{line}'");
            }

            if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                throw new InvalidDataException($"block snapshot index is not a number: '{line}'");
            }

            if (index != blocks.Count)
            {
                throw new InvalidDataException(
                    $"block snapshot is out of order at '{line}': expected index {blocks.Count}, found {index}");
            }

            if (!Identifier.TryParse(fields[1].Trim(), out var id))
            {
                throw new InvalidDataException($"block snapshot identifier is invalid: '{line}'");
            }

            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
            {
                throw new InvalidDataException($"block snapshot state count is invalid: '{line}'");
            }

            blocks.Add((id, count));
        }

        if (blocks.Count != BlockCount)
        {
            throw new InvalidDataException(
                $"block snapshot holds {blocks.Count} blocks but the baseline is {BlockCount}");
        }

        var layout = new BlockStateLayout(blocks);
        if (layout.TotalStates != BlockStateCount)
        {
            throw new InvalidDataException(
                $"block snapshot yields {layout.TotalStates} states but the baseline is {BlockStateCount}");
        }

        return layout;
    }
}
