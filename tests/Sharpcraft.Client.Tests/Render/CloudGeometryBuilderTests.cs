// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Cloud face geometry (CloudRenderer cell size/thickness/face brightness), docs/modules/clouds.md 2.</summary>
public sealed class CloudGeometryBuilderTests
{
    [Fact]
    public void OneTopFaceProducesFourVerticesAndTwoTriangles()
    {
        var quads = new[] { new CloudQuad(0, 0, CloudFaceDirection.Up, false) };
        var origin = new Vector3(100f, 192f, 100f);
        var eye = new Vector3(0f, 2000f, 0f); // far above so distance/fogEnd stays near 1 (use big fogEnd)
        var (vertices, indices) = CloudGeometryBuilder.Build(quads, eye, origin, fogEnd: 1e9f);

        Assert.Equal(4 * 7, vertices.Length);
        Assert.Equal(6, indices.Length);
        // Up face vertex 0 is (0,1,0): origin + thickness 4 up.
        Assert.Equal(100f, vertices[0], 3);
        Assert.Equal(196f, vertices[1], 3);
        Assert.Equal(100f, vertices[2], 3);
        // Top brightness is 1.0.
        Assert.Equal(1f, vertices[3], 3);
        Assert.Equal(1f, vertices[6], 3); // alpha
        Assert.Equal(0u, indices[0]);
        Assert.Equal(3u, indices[5]);
    }

    [Fact]
    public void BottomFaceUsesTheBottomBrightness()
    {
        var quads = new[] { new CloudQuad(0, 0, CloudFaceDirection.Down, false) };
        var (vertices, _) = CloudGeometryBuilder.Build(quads, new Vector3(0f, 0f, 0f), new Vector3(0f, 192f, 0f), fogEnd: 1e9f);

        Assert.Equal(0.7f, vertices[3], 3);
    }
}
