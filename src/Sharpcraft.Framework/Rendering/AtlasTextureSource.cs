// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The block atlas the terrain draws from, with a version that says when it changed
/// (docs/decisions/0017-reload-invalidation-by-layout.md).
/// </summary>
/// <remarks>
/// A reload can produce a new atlas with different dimensions, so the texture has to be replaced rather than resized in
/// place. The version is what the draw loop watches; the caller decides whether the geometry also has to be rebuilt,
/// because that depends on whether the layout changed, which only the atlas builder knows.
/// </remarks>
public sealed class AtlasTextureSource
{
    private readonly Lock _gate = new();
    private TextureData? _atlas;
    private float[] _spriteRects = [];
    private int _version;

    /// <summary>Current atlas image, or null when there is none.</summary>
    public TextureData? Atlas
    {
        get
        {
            lock (_gate)
            {
                return _atlas;
            }
        }
    }

    /// <summary>Increases whenever the atlas is replaced.</summary>
    public int Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>Replaces the atlas image and bumps the version.</summary>
    /// <param name="atlas">New atlas image.</param>
    public void Update(TextureData? atlas) => Update(atlas, []);

    /// <summary>Replaces the atlas image and its sprite rectangle table, bumping the version.</summary>
    /// <param name="atlas">New atlas image.</param>
    /// <param name="spriteRects">Atlas bounds as four floats per sprite, indexed by the id a vertex carries (T-832).</param>
    /// <remarks>
    /// The table travels with the image because both are renumbered together: a rebuilt atlas assigns new ids, so a
    /// table from the previous one would point at the wrong sprites.
    /// </remarks>
    public void Update(TextureData? atlas, float[] spriteRects)
    {
        ArgumentNullException.ThrowIfNull(spriteRects);

        lock (_gate)
        {
            _atlas = atlas;
            _spriteRects = spriteRects;
            _version++;
        }
    }

    /// <summary>Current sprite rectangle table, empty when there is no atlas.</summary>
    public float[] SpriteRects
    {
        get
        {
            lock (_gate)
            {
                return _spriteRects;
            }
        }
    }
}
