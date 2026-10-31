// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Celestial quad vertex/index layout (docs/specs/render/sky-celestial.md 2-3).</summary>
public sealed class CelestialMeshBuilderTests
{
    [Fact]
    public void QuadPacksPositionsAndUvsWithTwoTriangles()
    {
        var (vertices, indices) = CelestialMeshBuilder.CelestialQuad(
            angleRadians: 0f, height: 100f, size: 30f, uv: (0f, 0f, 1f, 1f));

        Assert.Equal(4 * CelestialMeshBuilder.FloatsPerVertex, vertices.Length);
        Assert.Equal(new ushort[] { 0, 1, 2, 0, 2, 3 }, indices);

        // corner (-1,-1) -> world (30,100,-30), uv (0,0)
        Assert.Equal(30f, vertices[0], 3);
        Assert.Equal(100f, vertices[1], 3);
        Assert.Equal(-30f, vertices[2], 3);
        Assert.Equal(0f, vertices[3], 3);
        Assert.Equal(0f, vertices[4], 3);

        // corner (1,1) at offset 2 -> world (-30,100,30), uv (1,1)
        var o = 2 * CelestialMeshBuilder.FloatsPerVertex;
        Assert.Equal(-30f, vertices[o], 3);
        Assert.Equal(100f, vertices[o + 1], 3);
        Assert.Equal(30f, vertices[o + 2], 3);
        Assert.Equal(1f, vertices[o + 3], 3);
        Assert.Equal(1f, vertices[o + 4], 3);
    }
}
