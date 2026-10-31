// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Per state answers to the questions the mesher asks about every candidate face: is the neighbour opaque, is this block
/// a leaf, and are two states the same block.
/// </summary>
/// <remarks>
/// Asking those per cell meant resolving a state id to a name and then looking the name up for its texture alpha, tens of
/// thousands of times per chunk (measured at sixty four and three hundred nanoseconds per call). Both answers depend only
/// on the state, so they are computed once here: one array read replaces two dictionary probes and the string comparison.
/// </remarks>
public sealed class StateCullTable
{
    private const byte OpaqueFlag = 1;
    private const byte LeavesFlag = 2;

    private readonly byte[] _flags;
    private readonly int[] _family;

    /// <summary>Builds the table for a layout, using the atlas for the alpha question.</summary>
    /// <param name="layout">State layout, which fixes the block order and the state ids.</param>
    /// <param name="sprites">Sprite map, or null when there is no atlas and every block counts as opaque.</param>
    public StateCullTable(BlockStateLayout layout, BlockSpriteMap? sprites)
    {
        ArgumentNullException.ThrowIfNull(layout);

        _flags = new byte[layout.TotalStates];
        _family = new int[layout.TotalStates];

        var blocks = layout.Blocks;

        for (var family = 0; family < blocks.Count; family++)
        {
            var entry = blocks[family];
            var isLeaves = entry.Id.Path.EndsWith("_leaves", StringComparison.Ordinal);

            var isOpaque = true;

            if (sprites is not null)
            {
                var transparency = sprites.TransparencyOf(entry.Id);
                isOpaque = !transparency.HasTransparent && !transparency.HasTranslucent;
            }

            var flag = (byte)((isOpaque ? OpaqueFlag : 0) | (isLeaves ? LeavesFlag : 0));
            var end = Math.Min(entry.BaseId + entry.StateCount, _flags.Length);

            for (var state = entry.BaseId; state < end; state++)
            {
                _flags[state] = flag;
                _family[state] = family;
            }
        }
    }

    /// <summary>True when the state hides the face of the block beside it.</summary>
    /// <param name="state">Block state id.</param>
    public bool IsOpaque(int state) =>
        state >= 0 && state < _flags.Length && (_flags[state] & OpaqueFlag) != 0;

    /// <summary>True when the state is a leaf block, whose inner faces stay visible.</summary>
    /// <param name="state">Block state id.</param>
    public bool IsLeaves(int state) =>
        state >= 0 && state < _flags.Length && (_flags[state] & LeavesFlag) != 0;

    /// <summary>True when both states belong to the same block, so only their shared face is dropped.</summary>
    /// <param name="first">First state id.</param>
    /// <param name="second">Second state id.</param>
    public bool SameFamily(int first, int second) =>
        first >= 0 && first < _family.Length && second >= 0 && second < _family.Length && _family[first] == _family[second];
}
