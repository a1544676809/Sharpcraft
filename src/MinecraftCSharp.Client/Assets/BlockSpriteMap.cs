// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Identifiers;
using MinecraftCSharp.Common.Registries;

namespace MinecraftCSharp.Client.Assets;

/// <summary>Which face of a block is being textured.</summary>
public enum BlockFace
{
    /// <summary>+Y face.</summary>
    Top,

    /// <summary>The four vertical faces.</summary>
    Side,

    /// <summary>-Y face.</summary>
    Bottom
}

/// <summary>
/// Maps a block state id to the atlas sprite for a given face. The primary convention is vanilla's:
/// block <c>minecraft:stone</c> uses <c>minecraft:block/stone</c>. Blocks whose texture names differ
/// (<c>grass_block</c> is drawn from <c>grass_block_top</c>, <c>grass_block_side</c> and <c>dirt</c>)
/// fall back to face-suffixed lookups, and anything unmatched uses the missing sprite.
/// </summary>
/// <remarks>
/// This is the bridge to full model resolution: it only uses what the resource stack provides, but it
/// does not yet read <c>blockstates</c> or <c>models</c> JSON, so the fallback chain is documented
/// rather than assumed (docs/modules/render.md §6.1).
/// </remarks>
public sealed class BlockSpriteMap
{
    /// <summary>Directory prefix every block sprite lives under.</summary>
    public const string TextureDirectory = "block/";

    private readonly SpriteAtlas _atlas;
    private readonly BlockStateLayout? _layout;
    private readonly int[]? _blockByState;
    private readonly SpriteRect[][]? _facesByBlock;
    private readonly Dictionary<Identifier, SpriteRect[]> _cache = new();

    /// <summary>Creates a map over an atlas, optionally backed by a block state layout.</summary>
    /// <param name="atlas">Stitched atlas; must contain the missing sprite.</param>
    /// <param name="layout">Block state layout, or null when it is not known yet.</param>
    public BlockSpriteMap(SpriteAtlas atlas, BlockStateLayout? layout = null)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        _atlas = atlas;
        _layout = layout;

        if (layout is null)
        {
            return;
        }

        _blockByState = new int[layout.TotalStates];
        _facesByBlock = new SpriteRect[layout.Blocks.Count][];

        for (var block = 0; block < layout.Blocks.Count; block++)
        {
            var entry = layout.Blocks[block];
            _facesByBlock[block] = ResolveFaces(entry.Id);

            for (var offset = 0; offset < entry.StateCount; offset++)
            {
                _blockByState[entry.BaseId + offset] = block;
            }
        }
    }

    /// <summary>True when a layout is present, meaning state ids resolve to real blocks.</summary>
    public bool HasLayout => _layout is not null;

    /// <summary>Number of distinct block identifiers resolved so far.</summary>
    public int BlockCount => _cache.Count;

    /// <summary>Resolves the sprite for a block state id and face.</summary>
    public SpriteRect ForState(int stateId, BlockFace face = BlockFace.Side)
    {
        if (_blockByState is not null && _facesByBlock is not null && (uint)stateId < (uint)_blockByState.Length)
        {
            var faces = _facesByBlock[_blockByState[stateId]];
            return face switch
            {
                BlockFace.Top => faces[0],
                BlockFace.Bottom => faces[2],
                _ => faces[1]
            };
        }

        return _atlas.Get(SpriteAtlas.MissingSprite);
    }

    /// <summary>Resolves the sprite for a block identifier and face.</summary>
    public SpriteRect ForBlock(Identifier blockId, BlockFace face = BlockFace.Side)
    {
        var faces = ResolveFaces(blockId);
        return face switch
        {
            BlockFace.Top => faces[0],
            BlockFace.Bottom => faces[2],
            _ => faces[1]
        };
    }

    /// <summary>Resolves by texture path, for callers that already know the exact texture.</summary>
    public SpriteRect ForTexture(string texturePath) =>
        _atlas.Get(new Identifier("minecraft", TextureDirectory + texturePath));

    private SpriteRect[] ResolveFaces(Identifier blockId)
    {
        if (_cache.TryGetValue(blockId, out var cached))
        {
            return cached;
        }

        var path = blockId.Path;

        // 1) A texture named exactly like the block covers every face.
        var direct = new Identifier("minecraft", TextureDirectory + path);
        if (_atlas.Contains(direct))
        {
            var sprite = _atlas.Get(direct);
            var uniform = new[] { sprite, sprite, sprite };
            _cache[blockId] = uniform;
            return uniform;
        }

        // 2) Face-suffixed textures, as grass and many other blocks use.
        var top = Lookup($"{path}_top");
        var side = Lookup($"{path}_side");
        var bottom = Lookup($"{path}_bottom");

        if (top is not null || side is not null || bottom is not null)
        {
            var missing = _atlas.Get(SpriteAtlas.MissingSprite);
            var faces = new[]
            {
                top ?? side ?? missing,
                side ?? top ?? missing,
                bottom ?? side ?? top ?? missing
            };

            _cache[blockId] = faces;
            return faces;
        }

        // 3) Nothing matched: the missing sprite, which is how vanilla shows an unknown texture.
        var fallbackSprite = _atlas.Get(SpriteAtlas.MissingSprite);
        var fallback = new[] { fallbackSprite, fallbackSprite, fallbackSprite };
        _cache[blockId] = fallback;
        return fallback;
    }

    private SpriteRect? Lookup(string texturePath)
    {
        var id = new Identifier("minecraft", TextureDirectory + texturePath);
        return _atlas.Contains(id) ? _atlas.Get(id) : null;
    }
}
