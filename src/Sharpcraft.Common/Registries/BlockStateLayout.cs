// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Global block-state id layout: blocks appear in registration order and each block owns a
/// contiguous id range whose size is its state count.
/// </summary>
/// <remarks>
/// This mirrors how vanilla assigns <c>Block.BLOCK_STATE_REGISTRY</c> ids (iteration over
/// the block registry, then over each block's states), so a base id plus a
/// <see cref="BlockStateDefinition"/> offset reproduces the protocol-visible id.
/// </remarks>
public sealed class BlockStateLayout
{
    private readonly List<Entry> _blocks = [];
    private readonly Dictionary<Identifier, int> _baseIds = [];

    /// <summary>One block's contiguous id range.</summary>
    public readonly record struct Entry(Identifier Id, int BaseId, int StateCount);

    /// <summary>Builds the layout from blocks in registration order.</summary>
    /// <exception cref="InvalidOperationException">A block is registered twice.</exception>
    /// <exception cref="ArgumentException">A block has a non-positive state count.</exception>
    public BlockStateLayout(IEnumerable<(Identifier Id, int StateCount)> blocksInRegistrationOrder)
    {
        ArgumentNullException.ThrowIfNull(blocksInRegistrationOrder);

        var nextBase = 0;
        foreach (var (id, stateCount) in blocksInRegistrationOrder)
        {
            if (stateCount <= 0)
            {
                throw new ArgumentException($"block '{id}' must own at least one state", nameof(blocksInRegistrationOrder));
            }

            if (!_baseIds.TryAdd(id, nextBase))
            {
                throw new InvalidOperationException($"block '{id}' is registered twice");
            }

            _blocks.Add(new Entry(id, nextBase, stateCount));
            nextBase = checked(nextBase + stateCount);
        }

        TotalStates = nextBase;
    }

    /// <summary>Blocks in registration order, with their id ranges.</summary>
    public IReadOnlyList<Entry> Blocks => _blocks;

    /// <summary>Total number of block states across all blocks.</summary>
    public int TotalStates { get; }

    /// <summary>Base id of a block.</summary>
    /// <exception cref="KeyNotFoundException">Unknown block.</exception>
    public int GetBaseId(Identifier blockId) =>
        _baseIds.TryGetValue(blockId, out var baseId) ? baseId : throw new KeyNotFoundException($"unknown block '{blockId}'");

    /// <summary>Attempts to read a block's base id.</summary>
    public bool TryGetBaseId(Identifier blockId, out int baseId) => _baseIds.TryGetValue(blockId, out baseId);

    /// <summary>Global state id for a state of <paramref name="definition"/>.</summary>
    public int GetStateId(BlockStateDefinition definition, IReadOnlyList<string> valuesInPropertyOrder)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return GetBaseId(definition.Id) + definition.IndexOf(valuesInPropertyOrder);
    }
}
