// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Identifiers;

namespace MinecraftCSharp.Common.Registries;

/// <summary>
/// The state shape of one block: its properties sorted by name, and the mapping from
/// property values to the state's offset inside the block.
/// </summary>
/// <remarks>
/// Enumeration order is the cartesian product of the properties with the <b>last
/// property varying fastest</b>, matching vanilla's state construction
/// (docs/analysis/common/registry-order.md 5).
/// </remarks>
public sealed class BlockStateDefinition
{
    private readonly BlockStateProperty[] _properties;

    /// <summary>Creates a definition; properties are sorted by name like vanilla.</summary>
    public BlockStateDefinition(Identifier id, IEnumerable<BlockStateProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        Id = id;
        _properties = properties
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        var count = 1;
        foreach (var property in _properties)
        {
            if (property.Radix == 0)
            {
                throw new ArgumentException($"property '{property.Name}' has no values", nameof(properties));
            }

            count = checked(count * property.Radix);
        }

        StateCount = count;
    }

    /// <summary>Identifier of the owning block.</summary>
    public Identifier Id { get; }

    /// <summary>Properties in sorted-name order.</summary>
    public IReadOnlyList<BlockStateProperty> Properties => _properties;

    /// <summary>Number of states this block owns.</summary>
    public int StateCount { get; }

    /// <summary>
    /// Offset of a state inside the block, given one value per property <b>in
    /// <see cref="Properties"/> order</b>. The last property varies fastest.
    /// </summary>
    /// <exception cref="ArgumentException">Wrong number of values or an unknown value.</exception>
    public int IndexOf(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count != _properties.Length)
        {
            throw new ArgumentException(
                $"expected {_properties.Length} values but got {values.Count}", nameof(values));
        }

        var index = 0;
        for (var i = 0; i < _properties.Length; i++)
        {
            var position = _properties[i].IndexOf(values[i]);
            if (position < 0)
            {
                throw new ArgumentException(
                    $"'{values[i]}' is not a valid value for property '{_properties[i].Name}'", nameof(values));
            }

            index = (index * _properties[i].Radix) + position;
        }

        return index;
    }
}
