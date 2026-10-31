// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Frustum extraction and box test (matrix row 3 support): the CPU reference the GPU cull pass must match.
/// </summary>
public sealed class FrustumPlanesTests
{
    private static readonly Vector4[] Planes = new Vector4[6];

    [Fact]
    public void ABoxInFrontOfTheCameraIsVisible()
    {
        FrustumPlanes.Extract(CameraMath.BuildViewProjection(Vector3.Zero, new Vector3(0, 0, -1), 70f, 16, 9), Planes);

        Assert.True(FrustumPlanes.IsVisible(Planes, new Vector3(-1, 0, -10), new Vector3(1, 2, -8)));
    }

    [Fact]
    public void ABoxBehindTheCameraIsCulled()
    {
        FrustumPlanes.Extract(CameraMath.BuildViewProjection(Vector3.Zero, new Vector3(0, 0, -1), 70f, 16, 9), Planes);

        Assert.False(FrustumPlanes.IsVisible(Planes, new Vector3(-1, 0, 8), new Vector3(1, 2, 10)));
    }

    [Fact]
    public void ABoxBesideTheViewIsCulledAndOneStraddlingTheEdgeIsNot()
    {
        FrustumPlanes.Extract(CameraMath.BuildViewProjection(Vector3.Zero, new Vector3(0, 0, -1), 70f, 16, 9), Planes);

        // Far off to the side at close range: outside the cone.
        Assert.False(FrustumPlanes.IsVisible(Planes, new Vector3(200, 0, -5), new Vector3(210, 4, -1)));

        // Straddling the left edge: any overlap keeps the chunk, which is what makes the test conservative.
        Assert.True(FrustumPlanes.IsVisible(Planes, new Vector3(-20, 0, -10), new Vector3(0, 4, -8)));
    }
}
