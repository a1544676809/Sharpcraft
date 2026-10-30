// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The textures the interface draws text and GUI sprites from, and a version that says when they changed
/// (docs/decisions/0016-resource-pack-file-watch.md).
/// </summary>
/// <remarks>
/// A reload replaces the font atlas and the sprite images but the renderer already uploaded the old ones, so the new data
/// has to reach it. Passing a mutable source with a version lets the draw loop notice the change and re-upload, without the
/// loader knowing anything about the GPU.
/// </remarks>
public sealed class UiTextureSource
{
    private readonly Lock _gate = new();
    private TextureData? _glyph;
    private IReadOnlyList<TextureData> _sprites = [];
    private int _version;

    /// <summary>Glyph atlas, or null when text cannot be drawn.</summary>
    public TextureData? Glyph
    {
        get
        {
            lock (_gate)
            {
                return _glyph;
            }
        }
    }

    /// <summary>GUI sprites, in the order a range's sprite index refers to.</summary>
    public IReadOnlyList<TextureData> Sprites
    {
        get
        {
            lock (_gate)
            {
                return _sprites;
            }
        }
    }

    /// <summary>Increases whenever the textures are replaced.</summary>
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

    /// <summary>Replaces the textures and bumps the version.</summary>
    /// <param name="glyph">New glyph atlas, or null.</param>
    /// <param name="sprites">New sprite images.</param>
    public void Update(TextureData? glyph, IReadOnlyList<TextureData> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        lock (_gate)
        {
            _glyph = glyph;
            _sprites = sprites;
            _version++;
        }
    }
}
