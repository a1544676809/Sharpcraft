// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Motion reprojection (docs/specs/render/pipeline.md 13): a still camera must produce no motion, a camera that slides must
/// move the image the other way, and a point behind the camera must be reported as invalid rather than giving a number.
/// </summary>
public sealed class ReprojectionTests
{
    private static readonly Matrix4x4 Projection = Matrix4x4.CreatePerspectiveFieldOfView(1.2f, 16f / 9f, 0.1f, 256f);

    [Fact]
    public void AStillCameraProducesNoMotion()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        var motion = Reprojection.Of(new Vector3(0f, 0f, -5f), viewProjection, viewProjection, 1920, 1080);

        Assert.Equal(0f, motion.X, 3);
        Assert.Equal(0f, motion.Y, 3);
    }

    [Fact]
    public void ACameraSlidingRightMovesTheImageLeft()
    {
        var previous = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;
        var current = Matrix4x4.CreateLookAt(new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, -1f), Vector3.UnitY) * Projection;

        var motion = Reprojection.Of(new Vector3(0f, 0f, -5f), previous, current, 1920, 1080);

        // The surface appears further left now, so it was to the right last frame.
        Assert.True(motion.X > 0f, $"expected a positive x displacement, got {motion.X}");
        Assert.Equal(0f, motion.Y, 3);
    }

    [Fact]
    public void AMovingSurfaceProducesMoreMotionWhenCloser()
    {
        var previous = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;
        var current = Matrix4x4.CreateLookAt(new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, -1f), Vector3.UnitY) * Projection;

        var near = Reprojection.Of(new Vector3(0f, 0f, -2f), previous, current, 1920, 1080);
        var far = Reprojection.Of(new Vector3(0f, 0f, -20f), previous, current, 1920, 1080);

        Assert.True(MathF.Abs(near.X) > MathF.Abs(far.X));
    }

    [Fact]
    public void APointBehindTheCameraIsInvalid()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        var motion = Reprojection.Of(new Vector3(0f, 0f, 5f), viewProjection, viewProjection, 1920, 1080);

        Assert.True(float.IsNaN(motion.X));
    }

    [Fact]
    public void HistoryReturnsThePreviousFrameOnAdvance()
    {
        var history = new FrameHistory();
        Assert.False(history.HasPrevious);

        var first = history.Advance(Matrix4x4.Identity);
        Assert.Equal(default(Matrix4x4), first);
        Assert.True(history.HasPrevious);
        Assert.Equal(Matrix4x4.Identity, history.PreviousViewProjection);
    }
}
