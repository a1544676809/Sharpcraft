// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;
using MinecraftCSharp.Framework.Rendering;

namespace MinecraftCSharp.Framework.Tests;

/// <summary>
/// Camera and clip-space math, verified without a GPU (docs/decisions/0003-wgpu-binding.md).
/// </summary>
public sealed class CameraMathTests
{
    [Fact]
    public void ProjectionMapsNearToZeroAndFarToOne()
    {
        var projection = CameraMath.BuildProjection(fieldOfViewDegrees: 60f, aspectRatio: 1.5f, near: 0.5f, far: 500f);

        // A point on the near plane lands on z/w = 0, the far plane on z/w = 1 (WebGPU clip range).
        var nearPoint = Vector4.Transform(new Vector3(0, 0, -0.5f), projection);
        var farPoint = Vector4.Transform(new Vector3(0, 0, -500f), projection);

        Assert.Equal(0f, nearPoint.Z / nearPoint.W, 4);
        Assert.Equal(1f, farPoint.Z / farPoint.W, 4);
    }

    [Fact]
    public void ViewProjectionPutsTheTargetNearTheScreenCentre()
    {
        var eye = new Vector3(10, 20, 30);
        var target = new Vector3(2, 4, 6);

        var matrix = CameraMath.BuildViewProjection(eye, target, 60f, 16f / 9f);
        var clip = Vector4.Transform(target, matrix);

        Assert.Equal(0f, clip.X / clip.W, 3);
        Assert.Equal(0f, clip.Y / clip.W, 3);
        Assert.InRange(clip.Z / clip.W, 0f, 1f);
    }

    [Fact]
    public void DefaultCameraFramesTheSubjectAndLooksDownAtIt()
    {
        var centre = new Vector3(8, 64, 8);

        var (eye, target) = CameraMath.DefaultCamera(centre, radius: 16f);

        Assert.Equal(centre, target);
        Assert.True(eye.Y > centre.Y, "the eye must be above the subject");

        var distance = Vector3.Distance(eye, centre);
        Assert.True(distance > 16f, $"a radius-16 subject needs more than 16 blocks of distance, got {distance}");

        // Doubling the subject must push the camera further out.
        var (farEye, _) = CameraMath.DefaultCamera(centre, radius: 32f);
        Assert.True(Vector3.Distance(farEye, centre) > distance);
    }

    [Fact]
    public void PixelDimensionsMatchTheExplicitAspectRatio()
    {
        var eye = new Vector3(0, 10, 20);
        var target = Vector3.Zero;

        var fromPixels = CameraMath.BuildViewProjection(eye, target, 60f, width: 960, height: 540);
        var fromAspect = CameraMath.BuildViewProjection(eye, target, 60f, 960f / 540f);

        Assert.Equal(fromAspect, fromPixels);
    }

    [Fact]
    public void PixelDimensionsDriveTheAspectRatio()
    {
        var eye = new Vector3(0, 0, 30);
        var target = Vector3.Zero;

        // A tall framebuffer must widen the vertical extent, so the matrices cannot match.
        var square = CameraMath.BuildViewProjection(eye, target, 60f, width: 500, height: 500);
        var wide = CameraMath.BuildViewProjection(eye, target, 60f, width: 1000, height: 500);

        Assert.NotEqual(square.M11, wide.M11);
    }

    [Fact]
    public void NonPositivePixelDimensionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CameraMath.BuildViewProjection(Vector3.Zero, Vector3.UnitZ, 60f, width: 0, height: 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CameraMath.BuildViewProjection(Vector3.Zero, Vector3.UnitZ, 60f, width: 10, height: 0));
    }

    [Fact]
    public void InvalidArgumentsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CameraMath.BuildProjection(60f, 0f, 0.1f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => CameraMath.BuildProjection(0f, 1f, 0.1f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => CameraMath.BuildViewProjection(Vector3.Zero, Vector3.UnitZ, 60f, -1f));
    }

    [Fact]
    public void ClipSpaceTransformInterleavesPositionAndColour()
    {
        var request = new FrameRequest(
            Width: 32,
            Height: 32,
            // Two vertices of position(3) + colour(3) + tile(2) + sprite rect(4).
            Vertices:
            [
                0f, 0f, 0f, 0.25f, 0.5f, 0.75f, 2f, 3f, 0.125f, 0.25f, 0.5f, 0.75f,
                1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 0.125f, 0.25f, 0.5f, 0.75f
            ],
            Indices: [0, 1, 1],
            ClearColour: new Vector4(0, 0, 0, 1),
            CameraPosition: new Vector3(0, 0, 10),
            CameraTarget: Vector3.Zero);

        var matrix = CameraMath.BuildViewProjection(request.CameraPosition, request.CameraTarget, 60f, 1f);
        var data = ClipSpaceTransform.Build(request, matrix);

        Assert.Equal(2 * ClipSpaceTransform.FloatsPerVertex, data.Length);

        // Colour, tile factor and sprite rect are copied verbatim; position comes from the matrix.
        Assert.Equal(0.25f, data[4]);
        Assert.Equal(0.5f, data[5]);
        Assert.Equal(0.75f, data[6]);
        Assert.Equal(2f, data[7]);
        Assert.Equal(3f, data[8]);
        Assert.Equal(0.125f, data[9]);
        Assert.Equal(0.25f, data[10]);
        Assert.Equal(0.5f, data[11]);
        Assert.Equal(0.75f, data[12]);
        Assert.Equal(1f, data[13 + 4]);

        // The origin sits behind the eye at z=10 in a right-handed view, so w is positive.
        Assert.True(data[3] > 0f);
    }

    [Fact]
    public void EmptyGeometryProducesNoVertices()
    {
        var request = new FrameRequest(8, 8, [], [], Vector4.One, new Vector3(0, 0, 5), Vector3.Zero);

        var data = ClipSpaceTransform.Build(request, Matrix4x4.Identity);

        Assert.Empty(data);
    }
}
