// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections.Concurrent;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>Sprite and layer a single state and face draws with.</summary>
/// <param name="Sprite">Atlas sprite, or null when there is no atlas.</param>
/// <param name="Layer">Which buffer the face belongs in.</param>
public readonly record struct QuadVisual(SpriteRect? Sprite, BlockLayer Layer);

/// <summary>
/// Answers what a (state, face) pair draws with, once per pair.
/// </summary>
/// <remarks>
/// Emitting one quad used to resolve the state to a block name, ask the sprite map for the face sprite and run the layer
/// rules, then repeat the sprite lookup for the append, which allocates and hashes a few times per quad. There are only
/// a handful of distinct state and face pairs in a chunk, so the answers are cached and the emitter does one lookup.
/// </remarks>
public sealed class StateVisualCache
{
    private readonly BlockSpriteMap? _sprites;
    private readonly BlockStateLayout? _layout;
    private readonly ConcurrentDictionary<int, QuadVisual> _visuals = new();

    /// <summary>Creates a cache over one atlas and layout.</summary>
    /// <param name="sprites">Sprite map, or null when the frame has no atlas.</param>
    /// <param name="layout">State layout, or null when states cannot be named.</param>
    public StateVisualCache(BlockSpriteMap? sprites, BlockStateLayout? layout)
    {
        _sprites = sprites;
        _layout = layout;
    }

    /// <summary>Sprite and layer for a state and face.</summary>
    /// <param name="state">Block state id.</param>
    /// <param name="face">Atlas face bucket.</param>
    public QuadVisual Get(int state, BlockFace face) =>
        _visuals.GetOrAdd(((state & 0x3FFFF) << 2) | (int)face, key => Build(key >> 2, (BlockFace)(key & 3)));

    private QuadVisual Build(int state, BlockFace face)
    {
        if (_sprites is null)
        {
            return new QuadVisual(null, BlockLayer.Solid);
        }

        var sprite = _sprites.ForState(state, face);
        var layer = sprite is null || _layout?.BlockAt(state) is not { } block
            ? BlockLayerRules.Of(state, _sprites, _layout)
            : BlockLayerRules.OfSprite(block, sprite, _sprites);

        return new QuadVisual(sprite, layer);
    }
}
