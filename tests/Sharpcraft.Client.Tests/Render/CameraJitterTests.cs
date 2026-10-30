// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Sub-pixel jitter (docs/specs/render/pipeline.md 13): the offsets must stay inside one pixel, must not all point the same
/// way, and must move the projection by less than a pixel or the image would visibly swim.
/// </summary>
public sealed class CameraJitterTests
{
    [Fact]
    public void EveryOffsetStaysInsideOnePixel()
    {
        for (var index = 0; index < CameraJitter.Period; index++)
        {
            var offset = CameraJitter.For(index);

            Assert.InRange(offset.X, -0.5f, 0.5f);
            Assert.InRange(offset.Y, -0.5f, 0.5f);
        }
    }

    [Fact]
    public void TheSequenceIsCentredOnZero()
    {
        var sum = Vector2.Zero;

        for (var index = 0; index < CameraJitter.Period; index++)
        {
            sum += CameraJitter.For(index);
        }

        Assert.Equal(0f, sum.X, 4);
        Assert.Equal(0f, sum.Y, 4);
    }

    [Fact]
    public void TheSequenceRepeatsAndHandlesNegativeFrames()
    {
        Assert.Equal(CameraJitter.For(0), CameraJitter.For(CameraJitter.Period));
        Assert.Equal(CameraJitter.For(3), CameraJitter.For(-5));
    }

    [Fact]
    public void TheProjectionMovesByLessThanOnePixel()
    {
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(1.2f, 1.6f, 0.1f, 256f);
        var jittered = CameraJitter.Apply(projection, CameraJitter.For(1), 1920, 1080);

        // Clip space is -1..1 over the viewport, so two units equal the viewport width: one pixel is 2/1920.
        Assert.InRange(MathF.Abs(jittered.M31 - projection.M31), 0f, 2f / 1920f + 1e-6f);
        Assert.InRange(MathF.Abs(jittered.M32 - projection.M32), 0f, 2f / 1080f + 1e-6f);
    }
}
