// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The uniform staging alternates between two copies so a frame being recorded cannot overwrite one the GPU
/// is still reading (T-837).
/// </summary>
public sealed class FrameUniformStagingTests
{
    [Fact]
    public void EveryBlockHasTwoDistinctCopies()
    {
        var staging = new FrameUniformStaging();

        var camera0 = staging.Camera;
        var lightmap0 = staging.Lightmap;
        var fog0 = staging.Fog;
        var values0 = staging.FogValues;

        staging.Advance();

        Assert.NotSame(camera0, staging.Camera);
        Assert.NotSame(lightmap0, staging.Lightmap);
        Assert.NotSame(fog0, staging.Fog);
        Assert.NotSame(values0, staging.FogValues);
    }

    [Fact]
    public void AdvanceAlternatesAndReturnsToTheFirstCopy()
    {
        var staging = new FrameUniformStaging();
        var first = staging.Camera;

        staging.Advance();
        var second = staging.Camera;

        staging.Advance();

        Assert.NotSame(first, second);
        Assert.Same(first, staging.Camera);
    }

    [Fact]
    public void WritingOneCopyLeavesTheOtherAlone()
    {
        var staging = new FrameUniformStaging();
        var current = staging.Camera;
        current[0] = 0x11;

        staging.Advance();
        Assert.Equal(0, staging.Camera[0]);

        staging.Advance();
        Assert.Equal(0x11, staging.Camera[0]);
    }
}
