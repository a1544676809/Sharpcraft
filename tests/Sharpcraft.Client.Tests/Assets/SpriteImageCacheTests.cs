// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Reusing decoded sprites (docs/specs/quality-attributes.md): unchanged bytes must not be decoded twice, and the cache is
/// capped because it holds pixels, which grow with resolution.
/// </summary>
public sealed class SpriteImageCacheTests
{
    [Fact]
    public void UnchangedBytesAreNotDecodedTwice()
    {
        var cache = new SpriteImageCache();
        var location = new Identifier("minecraft", "block/stone");
        var bytes = new byte[] { 1, 2, 3 };
        var decodes = 0;

        var first = cache.GetOrDecode(location, bytes, _ => { decodes++; return Make(4); });
        var second = cache.GetOrDecode(location, bytes, _ => { decodes++; return Make(4); });

        Assert.Equal(1, decodes);
        Assert.Same(first, second);
    }

    [Fact]
    public void ChangedBytesAreDecodedAgain()
    {
        var cache = new SpriteImageCache();
        var location = new Identifier("minecraft", "block/stone");
        var decodes = 0;

        cache.GetOrDecode(location, [1], _ => { decodes++; return Make(2); });
        cache.GetOrDecode(location, [2], _ => { decodes++; return Make(2); });

        Assert.Equal(2, decodes);
    }

    [Fact]
    public void AHighResolutionImageBeyondTheCapIsNotCached()
    {
        var cache = new SpriteImageCache();
        var location = new Identifier("minecraft", "block/huge");

        // One byte per pixel keeps the test fast: 70 million pixels is past the 64 MiB cap.
        cache.GetOrDecode(location, [1], _ => new RgbaImage(8400, 8400, new byte[8400 * 8400 * 4]));

        Assert.Equal(0, cache.Count);
    }

    private static RgbaImage Make(int side) => new(side, side, new byte[side * side * 4]);
}
