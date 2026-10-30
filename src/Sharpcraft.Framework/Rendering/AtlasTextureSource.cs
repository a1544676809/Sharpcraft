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
    public void Update(TextureData? atlas)
    {
        lock (_gate)
        {
            _atlas = atlas;
            _version++;
        }
    }
}
