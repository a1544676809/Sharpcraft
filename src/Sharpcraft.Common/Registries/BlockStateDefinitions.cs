// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Per-block block-state property schemas, so a global state id can be read back into the property
/// values it holds instead of only being built from them.
/// </summary>
/// <remarks>
/// The baseline snapshot lists one line per property: block, property name, and the values in the
/// order the registry enumerates them. That order is not alphabetical for enums, and the state
/// encoding depends on it, so it is carried verbatim rather than sorted
/// (docs/specs/render/block-state-properties.md).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.state.StateDefinition", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/StateDefinition.java")]
public sealed class BlockStateDefinitions
{
    /// <summary>Embedded resource holding the baseline property schema.</summary>
    public const string SchemaResourceName = "Sharpcraft.Common.Registries.Data.vanilla-block-properties-26.3.tsv";

    private static readonly Lazy<BlockStateDefinitions> Cached = new(Load, isThreadSafe: true);

    private readonly Dictionary<Identifier, BlockStateDefinition> _definitions;

    private BlockStateDefinitions(Dictionary<Identifier, BlockStateDefinition> definitions)
    {
        _definitions = definitions;
    }

    /// <summary>The baseline schema, parsed once.</summary>
    public static BlockStateDefinitions Baseline => Cached.Value;

    /// <summary>Number of blocks the schema covers.</summary>
    public int Count => _definitions.Count;

    /// <summary>Looks up one block's definition.</summary>
    /// <param name="block">Block identifier.</param>
    /// <param name="definition">Its definition when found.</param>
    public bool TryGet(Identifier block, out BlockStateDefinition definition) =>
        _definitions.TryGetValue(block, out definition!);

    /// <summary>Reads one property of a state by its global id.</summary>
    /// <param name="stateId">Global block-state id.</param>
    /// <param name="property">Property name, case sensitive as the registry writes it.</param>
    /// <param name="value">The value that state holds for the property.</param>
    /// <returns>Whether the state exists and carries the property.</returns>
    public bool TryGetProperty(int stateId, string property, out string value)
    {
        ArgumentNullException.ThrowIfNull(property);

        value = string.Empty;

        var layout = VanillaBlockRegistry.Layout;
        if (layout.BlockAt(stateId) is not { } block || !TryGet(block, out var definition))
        {
            return false;
        }

        if (!layout.TryGetBaseId(block, out var baseId))
        {
            return false;
        }

        var offset = stateId - baseId;
        if ((uint)offset >= (uint)definition.StateCount)
        {
            return false;
        }

        return definition.TryGetPropertyValue(offset, property, out value);
    }

    /// <summary>Reads the schema out of the assembly's embedded snapshot.</summary>
    /// <exception cref="InvalidDataException">The snapshot is missing or malformed.</exception>
    public static BlockStateDefinitions Load()
    {
        var assembly = typeof(BlockStateDefinitions).Assembly;
        using var stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidDataException($"embedded block property schema not found: {SchemaResourceName}");

        using var reader = new StreamReader(stream);
        var properties = new Dictionary<Identifier, List<BlockStateProperty>>();
        var order = new List<Identifier>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length != 3)
            {
                throw new InvalidDataException($"property schema line has {fields.Length} fields: '{line}'");
            }

            if (!Identifier.TryParse(fields[0].Trim(), out var block))
            {
                throw new InvalidDataException($"property schema block identifier is invalid: '{line}'");
            }

            var values = fields[2].Split(',');
            if (values.Length == 0 || values.Any(string.IsNullOrEmpty))
            {
                throw new InvalidDataException($"property schema values are invalid: '{line}'");
            }

            if (!properties.TryGetValue(block, out var list))
            {
                list = [];
                properties[block] = list;
                order.Add(block);
            }

            if (list.Any(existing => string.Equals(existing.Name, fields[1], StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"property '{fields[1]}' repeats for {block}: '{line}'");
            }

            list.Add(new BlockStateProperty(fields[1], values));
        }

        var definitions = new Dictionary<Identifier, BlockStateDefinition>(order.Count);
        foreach (var block in order)
        {
            definitions[block] = new BlockStateDefinition(block, properties[block]);
        }

        return new BlockStateDefinitions(definitions);
    }
}
