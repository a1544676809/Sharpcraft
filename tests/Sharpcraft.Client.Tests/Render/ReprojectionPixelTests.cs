// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The per-pixel half of the motion vector pass (docs/specs/render/pipeline.md 13): a pixel rebuilt from its depth and
/// reprojected must agree with the analytic answer for the same world point, which is what the shader will be checked against.
/// </summary>
public sealed class ReprojectionPixelTests
{
    private static readonly Matrix4x4 Projection = Matrix4x4.CreatePerspectiveFieldOfView(1.2f, 16f / 9f, 0.1f, 256f);
    private static readonly Matrix4x4 View = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY);

    [Fact]
    public void WorldReconstructionRoundTripsThroughProjection()
    {
        var viewProjection = View * Projection;
        Matrix4x4.Invert(viewProjection, out var inverse);

        var world = new Vector3(0.7f, -1.3f, -6f);
        Assert.True(Reprojection.TryToScreen(world, viewProjection, out var screen));

        var clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);
        var depth = clip.Z / clip.W;

        var rebuilt = Reprojection.ReconstructWorld(screen, depth, inverse);

        Assert.Equal(world.X, rebuilt.X, 3);
        Assert.Equal(world.Y, rebuilt.Y, 3);
        Assert.Equal(world.Z, rebuilt.Z, 3);
    }

    [Fact]
    public void PixelMotionMatchesTheAnalyticMotion()
    {
        var previous = View * Projection;
        var current = Matrix4x4.CreateLookAt(new Vector3(0.8f, 0f, 0f), new Vector3(0.8f, 0f, -1f), Vector3.UnitY) * Projection;
        Matrix4x4.Invert(current, out var inverse);

        var world = new Vector3(0f, 0f, -5f);
        Assert.True(Reprojection.TryToScreen(world, current, out var screen));
        var clip = Vector4.Transform(new Vector4(world, 1f), current);
        var depth = clip.Z / clip.W;

        var byPixel = Reprojection.OfPixel(screen, depth, inverse, previous, 1920, 1080);
        var analytic = Reprojection.Of(world, previous, current, 1920, 1080);

        Assert.Equal(analytic.X, byPixel.X, 2);
        Assert.Equal(analytic.Y, byPixel.Y, 2);
    }

    [Fact]
    public void AStillCameraProducesNoPixelMotion()
    {
        var viewProjection = View * Projection;
        Matrix4x4.Invert(viewProjection, out var inverse);

        var world = new Vector3(0f, 0f, -8f);
        Reprojection.TryToScreen(world, viewProjection, out var screen);
        var clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);

        var motion = Reprojection.OfPixel(screen, clip.Z / clip.W, inverse, viewProjection, 1920, 1080);

        Assert.Equal(0f, motion.X, 2);
        Assert.Equal(0f, motion.Y, 2);
    }
}
