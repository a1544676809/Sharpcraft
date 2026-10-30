// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Frustum culling (docs/specs/render/pipeline.md 6): an entity behind the camera or far off to the side must be rejected,
/// one straight ahead kept, and the box support point must be used so a large box straddling the edge is not dropped.
/// </summary>
public sealed class ViewFrustumTests
{
    private static readonly Matrix4x4 View = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -1f), new Vector3(0f, 1f, 0f));

    private static readonly Matrix4x4 Projection = Matrix4x4.CreatePerspectiveFieldOfView(
        MathF.PI / 2f,
        aspectRatio: 1f,
        nearPlaneDistance: 0.1f,
        farPlaneDistance: 100f);

    [Fact]
    public void ABoxInFrontOfTheCameraIsVisible()
    {
        var frustum = ViewFrustum.FromViewProjection(View * Projection);

        Assert.True(frustum.IsBoxVisible(new Vector3(0f, 0f, -5f), new Vector3(0.3f, 0.9f, 0.3f)));
    }

    [Fact]
    public void ABoxBehindTheCameraIsCulled()
    {
        var frustum = ViewFrustum.FromViewProjection(View * Projection);

        Assert.False(frustum.IsBoxVisible(new Vector3(0f, 0f, 5f), new Vector3(0.3f, 0.9f, 0.3f)));
    }

    [Fact]
    public void ABoxFarToTheSideIsCulled()
    {
        var frustum = ViewFrustum.FromViewProjection(View * Projection);

        Assert.False(frustum.IsBoxVisible(new Vector3(20f, 0f, -5f), new Vector3(0.3f, 0.9f, 0.3f)));
    }

    [Fact]
    public void ABoxBeyondTheFarPlaneIsCulled()
    {
        var frustum = ViewFrustum.FromViewProjection(View * Projection);

        Assert.False(frustum.IsBoxVisible(new Vector3(0f, 0f, -200f), new Vector3(0.3f, 0.9f, 0.3f)));
    }

    [Fact]
    public void ALargeBoxStraddlingTheEdgeIsVisible()
    {
        var frustum = ViewFrustum.FromViewProjection(View * Projection);

        Assert.True(frustum.IsBoxVisible(new Vector3(6f, 0f, -5f), new Vector3(5f, 1f, 5f)));
    }
}
