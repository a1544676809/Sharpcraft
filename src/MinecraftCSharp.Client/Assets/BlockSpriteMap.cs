// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Identifiers;
using MinecraftCSharp.Common.Registries;

namespace MinecraftCSharp.Client.Assets;

/// <summary>
/// Maps a block state id to the atlas sprite that represents it. The convention is vanilla's:
/// block <c>minecraft:stone</c> uses <c>minecraft:block/stone</c>. Anything without a sprite falls
/// back to the missing sprite, which is how a vanilla client shows an unknown texture.
/// </summary>
/// <remarks>
/// Two modes exist because state ids only carry meaning together with the block layout from
/// <c>registry_data</c>: with a layout the lookup is a precomputed array (no per-face cost), without
/// one every state resolves to the missing sprite. Real-server texturing therefore depends on the
/// layout being assembled, which is tracked as a separate task.
/// </remarks>
public sealed class BlockSpriteMap
{
    /// <summary>Directory prefix every block sprite is expected under.</summary>
    public const string TextureDirectory = "block/";

    private readonly SpriteAtlas _atlas;
    private readonly BlockStateLayout? _layout;
    private readonly SpriteRect[]? _spritesByState;
    private readonly Dictionary<Identifier, SpriteRect> _cache = new();

    /// <summary>Creates a map over an atlas, optionally backed by a block state layout.</summary>
    /// <param name="atlas">Stitched atlas; must contain the missing sprite.</param>
    /// <param name="layout">Block state layout, or null when it has not been assembled yet.</param>
    public BlockSpriteMap(SpriteAtlas atlas, BlockStateLayout? layout = null)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        _atlas = atlas;
        _layout = layout;

        if (layout is not null)
        {
            _spritesByState = new SpriteRect[layout.TotalStates];
            for (var block = 0; block < layout.Blocks.Count; block++)
            {
                var entry = layout.Blocks[block];
                var sprite = ForBlock(entry.Id);

                for (var offset = 0; offset < entry.StateCount; offset++)
                {
                    _spritesByState[entry.BaseId + offset] = sprite;
                }
            }
        }
    }

    /// <summary>True when a layout is present, meaning state ids resolve to real blocks.</summary>
    public bool HasLayout => _layout is not null;

    /// <summary>Number of distinct block identifiers seen while building the map.</summary>
    public int BlockCount => _cache.Count;

    /// <summary>Resolves the sprite for a block state id.</summary>
    public SpriteRect ForState(int stateId)
    {
        if (_spritesByState is not null && (uint)stateId < (uint)_spritesByState.Length)
        {
            return _spritesByState[stateId];
        }

        return _atlas.Get(SpriteAtlas.MissingSprite);
    }

    /// <summary>Resolves the sprite for a block identifier, using the vanilla naming convention.</summary>
    public SpriteRect ForBlock(Identifier blockId)
    {
        if (_cache.TryGetValue(blockId, out var cached))
        {
            return cached;
        }

        // minecraft:stone -> minecraft:block/stone
        var candidate = new Identifier(blockId.Namespace, TextureDirectory + blockId.Path);
        var sprite = _atlas.Get(candidate);

        _cache[blockId] = sprite;
        return sprite;
    }

    /// <summary>
    /// Resolves by texture path instead of block id, for blocks whose texture name differs from the
    /// block name (for example a block that uses <c>grass_block_top</c>).
    /// </summary>
    public SpriteRect ForTexture(string texturePath) =>
        _atlas.Get(new Identifier("minecraft", TextureDirectory + texturePath));
}
