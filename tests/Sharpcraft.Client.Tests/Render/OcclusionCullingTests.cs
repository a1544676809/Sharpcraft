// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Hierarchical occlusion culling (docs/specs/render/occlusion-culling.md): the test may keep a hidden box but must never
/// drop a visible one, so a box closer than the occluder is kept, a box behind it is dropped, and a box behind the camera
/// makes the test give up rather than guess.
/// </summary>
public sealed class OcclusionCullingTests
{
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) *
        Matrix4x4.CreatePerspectiveFieldOfView(1.2f, 16f / 9f, 0.1f, 256f);

    [Fact]
    public void ABoxInFrontOfTheOccluderIsKept()
    {
        // The near box is closer than the far box, so treating the far one as the occluder must keep the near one.
        Assert.True(OcclusionCulling.TryProjectBox(new Vector3(0f, 0f, -5f), new Vector3(0.5f), ViewProjection, out _, out _, out var nearDepth));
        Assert.True(OcclusionCulling.TryProjectBox(new Vector3(0f, 0f, -60f), new Vector3(0.5f), ViewProjection, out _, out _, out var farDepth));

        Assert.True(farDepth > nearDepth, "the farther box must report a larger depth");
        Assert.False(OcclusionCulling.IsOccluded(nearDepth, farthestOccluderDepth: farDepth));
    }

    [Fact]
    public void ABoxBehindTheOccluderIsCulled()
    {
        Assert.True(OcclusionCulling.TryProjectBox(new Vector3(0f, 0f, -60f), new Vector3(0.5f), ViewProjection, out _, out _, out var farDepth));
        Assert.True(OcclusionCulling.TryProjectBox(new Vector3(0f, 0f, -5f), new Vector3(0.5f), ViewProjection, out _, out _, out var nearDepth));

        Assert.True(OcclusionCulling.IsOccluded(farDepth, farthestOccluderDepth: nearDepth));
    }

    [Fact]
    public void TheProjectedRectangleCoversTheBox()
    {
        OcclusionCulling.TryProjectBox(Vector3.Zero, new Vector3(1f), ViewProjection, out var min, out var max, out _);

        Assert.True(min.X < 0.5f && max.X > 0.5f, "rectangle must straddle the screen centre");
        Assert.True(min.Y < 0.5f && max.Y > 0.5f);
    }

    [Fact]
    public void ABoxStraddlingTheCameraIsNotRejected()
    {
        // Behind the camera: no rectangle can be trusted, so the caller must keep drawing it.
        Assert.False(OcclusionCulling.TryProjectBox(new Vector3(0f, 0f, 5f), new Vector3(1f), ViewProjection, out _, out _, out _));
    }

    [Fact]
    public void EqualityIsNotOcclusion()
    {
        // Coincident surfaces must not disappear, hence the bias.
        Assert.False(OcclusionCulling.IsOccluded(0.5f, 0.5f));
    }
}
