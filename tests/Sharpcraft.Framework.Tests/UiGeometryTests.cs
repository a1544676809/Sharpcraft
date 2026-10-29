// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Two dimensional geometry and its batching rules (docs/modules/ui.md 9 and 10). The vertex budget, the range
/// splitting and the absence of per frame allocation are all contract, so each is pinned.
/// </summary>
public sealed class UiGeometryTests
{
    [Fact]
    public void AQuadIsFourVerticesAndTwoTriangles()
    {
        var geometry = new UiGeometry();
        geometry.SetScissor(0, 0, 100, 100);

        geometry.AddRectangle(10, 20, 30, 40, UiGeometry.PackColour(255, 0, 0, 255));

        Assert.Equal(4, geometry.Vertices.Length / UiGeometry.FloatsPerVertex);
        Assert.Equal(6, geometry.Indices.Length);
        Assert.Equal(1, geometry.QuadCount);
        Assert.Equal(20, UiGeometry.BytesPerVertex);
    }

    [Fact]
    public void TheQuadCornersCarryTheRequestedRectangle()
    {
        var geometry = new UiGeometry();
        geometry.SetScissor(0, 0, 100, 100);

        geometry.AddRectangle(10, 20, 30, 40, 0);

        var vertices = geometry.Vertices;
        Assert.Equal(10f, vertices[0]);
        Assert.Equal(20f, vertices[1]);
        Assert.Equal(40f, vertices[5]);   // second vertex x = 10 + 30
        Assert.Equal(60f, vertices[11]);  // third vertex y = 20 + 40
    }

    [Fact]
    public void ChangingTheScissorStartsANewRangeAndRepeatingItDoesNot()
    {
        var geometry = new UiGeometry();

        geometry.SetScissor(0, 0, 50, 50);
        geometry.AddRectangle(0, 0, 10, 10, 0);
        geometry.SetScissor(0, 0, 50, 50); // identical, must not open a range
        geometry.AddRectangle(0, 0, 10, 10, 0);
        geometry.SetScissor(50, 0, 100, 50); // different, must open one
        geometry.AddRectangle(60, 0, 10, 10, 0);

        Assert.Equal(2, geometry.Ranges.Length);
        Assert.Equal(12, geometry.Ranges[0].IndexCount);
        Assert.Equal(6, geometry.Ranges[1].IndexCount);
        Assert.Equal(12, geometry.Ranges[1].FirstIndex);
    }

    [Fact]
    public void AScreenOfManyWidgetsStillCostsAFewRanges()
    {
        // The chat overlay and the player list are two panels, so a forty line screen is two draws.
        var geometry = new UiGeometry();

        geometry.SetScissor(0, 400, 320, 480);
        for (var line = 0; line < 20; line++)
        {
            geometry.AddRectangle(4, 400 + (line * 4), 300, 4, 0);
        }

        geometry.SetScissor(400, 0, 480, 200);
        for (var row = 0; row < 20; row++)
        {
            geometry.AddRectangle(404, row * 10, 70, 10, 0);
        }

        Assert.Equal(2, geometry.Ranges.Length);
        Assert.Equal(40, geometry.QuadCount);
    }

    [Fact]
    public void ColoursPackWithRedInTheLowByte()
    {
        var packed = UiGeometry.PackColour(0x12, 0x34, 0x56, 0x78);

        Assert.Equal(0x78563412u, packed);
    }

    [Fact]
    public void RebuildingAFrameDoesNotAllocateOnceWarm()
    {
        var geometry = new UiGeometry();
        geometry.SetScissor(0, 0, 100, 100);
        geometry.AddRectangle(0, 0, 10, 10, 0); // warm the buffers

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            geometry.Clear();
            geometry.SetScissor(0, 0, 100, 100);

            for (var quad = 0; quad < 16; quad++)
            {
                geometry.AddRectangle(quad, quad, 8, 8, 0);
            }
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Fact]
    public void TheProjectionPutsTheOriginTopLeftAndTheFarCornerBottomRight()
    {
        Span<float> matrix = stackalloc float[UiProjection.FloatsPerMatrix];
        UiProjection.WriteOrthographic(320f, 240f, matrix);

        // Applying the two scale terms and the translations is enough for an orthographic matrix.
        var topLeft = (matrix[0] * 0f + matrix[12], matrix[5] * 0f + matrix[13]);
        var bottomRight = (matrix[0] * 320f + matrix[12], matrix[5] * 240f + matrix[13]);

        Assert.Equal(-1f, topLeft.Item1, 3);
        Assert.Equal(1f, topLeft.Item2, 3);
        Assert.Equal(1f, bottomRight.Item1, 3);
        Assert.Equal(-1f, bottomRight.Item2, 3);
    }
}
