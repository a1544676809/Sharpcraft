// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// The atlas source the renderer watches (docs/decisions/0017-reload-invalidation-by-layout.md): a reload can produce a
/// differently sized atlas, so the texture is replaced rather than resized, and the version is the signal.
/// </summary>
public sealed class AtlasTextureSourceTests
{
    [Fact]
    public void AnUpdateReplacesTheAtlasAndBumpsTheVersion()
    {
        var source = new AtlasTextureSource();
        Assert.Null(source.Atlas);
        Assert.Equal(0, source.Version);

        var first = new TextureData(4, 4, new byte[64]);
        source.Update(first);

        Assert.Same(first, source.Atlas);
        Assert.Equal(1, source.Version);

        // A different size is allowed; that is exactly why the texture has to be recreated instead of rewritten.
        var second = new TextureData(8, 8, new byte[256]);
        source.Update(second);

        Assert.Same(second, source.Atlas);
        Assert.Equal(8, source.Atlas!.Width);
        Assert.Equal(2, source.Version);
    }

    [Fact]
    public void ClearingTheAtlasStillAdvancesTheVersion()
    {
        var source = new AtlasTextureSource();
        source.Update(new TextureData(1, 1, new byte[4]));
        source.Update(null);

        Assert.Null(source.Atlas);
        Assert.Equal(2, source.Version);
    }
}
