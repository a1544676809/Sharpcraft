// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Frame size declared in a sprite's <c>.mcmeta</c> (docs/analysis/assets/texture-atlas-sizing.md §1): guessing a square
/// of the image width is only the common case.
/// </summary>
public sealed class AnimationFrameSizeTests
{
    [Fact]
    public void ADeclaredFrameSizeIsRead()
    {
        var size = AnimationFrameSize.TryRead(
            """{ "animation": { "width": 16, "height": 32, "frametime": 2 } }"""u8.ToArray());

        Assert.NotNull(size);
        Assert.Equal(16, size!.Value.Width);
        Assert.Equal(32, size.Value.Height);
    }

    [Fact]
    public void APartialOrMissingFrameSizeYieldsNothing()
    {
        Assert.Null(AnimationFrameSize.TryRead(null));
        Assert.Null(AnimationFrameSize.TryRead("""{ "texture": { "blur": true } }"""u8.ToArray()));
        Assert.Null(AnimationFrameSize.TryRead("""{ "animation": { "width": 16 } }"""u8.ToArray()));
    }

    [Fact]
    public void BrokenMetadataIsIgnored()
    {
        Assert.Null(AnimationFrameSize.TryRead("not json"u8.ToArray()));
    }
}
