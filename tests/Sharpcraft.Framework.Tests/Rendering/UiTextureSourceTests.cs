// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// The texture source the renderer watches (docs/decisions/0016-resource-pack-file-watch.md): a reload replaces the
/// images, and the version is what tells the draw loop to re-upload rather than comparing bytes every frame.
/// </summary>
public sealed class UiTextureSourceTests
{
    [Fact]
    public void AnUpdateReplacesTheImagesAndBumpsTheVersion()
    {
        var source = new UiTextureSource();
        Assert.Null(source.Glyph);
        Assert.Empty(source.Sprites);
        Assert.Equal(0, source.Version);

        var glyph = new TextureData(2, 2, new byte[16]);
        var sprites = new[] { new TextureData(1, 1, new byte[4]) };

        source.Update(glyph, sprites);

        Assert.Same(glyph, source.Glyph);
        Assert.Single(source.Sprites);
        Assert.Equal(1, source.Version);

        source.Update(null, []);

        Assert.Null(source.Glyph);
        Assert.Empty(source.Sprites);
        Assert.Equal(2, source.Version);
    }

    [Fact]
    public void ReadingTwiceAtTheSameVersionSeesTheSameImages()
    {
        var source = new UiTextureSource();
        var glyph = new TextureData(1, 1, new byte[4]);
        source.Update(glyph, []);

        Assert.Same(source.Glyph, source.Glyph);
        Assert.Equal(source.Version, source.Version);
    }
}
