// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the chest geometry's actual vertex extents to the boxes the reference model declares, so a
/// future change to the box layout fails here rather than showing up as a wrong looking chest.
/// The expected values come from <c>docs/specs/render/golden-vectors/chest.md</c>, not from the
/// implementation.
/// </summary>
public sealed class ChestMesherGeometryTests
{
    private const float Pixel = 1f / 16f;
    private const float Tolerance = 1e-5f;

    /// <summary>
    /// Three boxes in the order the model declares them: body, lid, lock. Each box is twenty four
    /// vertices of six floats, position first.
    /// </summary>
    private static (float[] Vertices, int VerticesPerBox) BuildClosedChest()
    {
        var mesher = new ChestMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Chest)];
        var geometry = mesher.Build(entities, new SpriteRect(0, 0, 16, 16, 1));

        Assert.Equal(72 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        return (geometry.Vertices, 24);
    }

    private static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) Extent(
        float[] vertices,
        int firstVertex,
        int count)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        for (var vertex = firstVertex; vertex < firstVertex + count; vertex++)
        {
            var at = vertex * ChunkMesher.FloatsPerVertex;
            minX = MathF.Min(minX, vertices[at]);
            minY = MathF.Min(minY, vertices[at + 1]);
            minZ = MathF.Min(minZ, vertices[at + 2]);
            maxX = MathF.Max(maxX, vertices[at]);
            maxY = MathF.Max(maxY, vertices[at + 1]);
            maxZ = MathF.Max(maxZ, vertices[at + 2]);
        }

        return (minX, minY, minZ, maxX, maxY, maxZ);
    }

    /// <summary>The body spans one to fifteen pixels across and nought to ten up.</summary>
    [Fact]
    public void TheBodyMatchesTheReferenceBox()
    {
        var (vertices, perBox) = BuildClosedChest();
        var body = Extent(vertices, 0, perBox);

        Assert.Equal(1f * Pixel, body.MinX, Tolerance);
        Assert.Equal(0f, body.MinY, Tolerance);
        Assert.Equal(1f * Pixel, body.MinZ, Tolerance);
        Assert.Equal(15f * Pixel, body.MaxX, Tolerance);
        Assert.Equal(10f * Pixel, body.MaxY, Tolerance);
        Assert.Equal(15f * Pixel, body.MaxZ, Tolerance);
    }

    /// <summary>The closed lid sits from nine to fourteen pixels up, one pixel in from the sides.</summary>
    [Fact]
    public void TheClosedLidMatchesTheReferenceBox()
    {
        var (vertices, perBox) = BuildClosedChest();
        var lid = Extent(vertices, perBox, perBox);

        Assert.Equal(1f * Pixel, lid.MinX, Tolerance);
        Assert.Equal(9f * Pixel, lid.MinY, Tolerance);
        Assert.Equal(1f * Pixel, lid.MinZ, Tolerance);
        Assert.Equal(15f * Pixel, lid.MaxX, Tolerance);
        Assert.Equal(14f * Pixel, lid.MaxY, Tolerance);
        Assert.Equal(15f * Pixel, lid.MaxZ, Tolerance);
    }

    /// <summary>The latch spans seven to nine across and seven to eleven up, on the front face.</summary>
    [Fact]
    public void TheLatchMatchesTheReferenceBox()
    {
        var (vertices, perBox) = BuildClosedChest();
        var latch = Extent(vertices, perBox * 2, perBox);

        Assert.Equal(7f * Pixel, latch.MinX, Tolerance);
        Assert.Equal(7f * Pixel, latch.MinY, Tolerance);
        Assert.Equal(15f * Pixel, latch.MinZ, Tolerance);
        Assert.Equal(9f * Pixel, latch.MaxX, Tolerance);
        Assert.Equal(11f * Pixel, latch.MaxY, Tolerance);
        Assert.Equal(16f * Pixel, latch.MaxZ, Tolerance);
    }

    private static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) LatchExtentFor(byte facing)
    {
        var mesher = new ChestMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Chest, Facing: facing)];
        var geometry = mesher.Build(entities, new SpriteRect(0, 0, 16, 16, 1));

        return Extent(geometry.Vertices, 24 * 2, 24);
    }

    /// <summary>The reference turns a chest by minus the facing yaw, a quarter turn at a time.</summary>
    [Theory]
    [InlineData((byte)5, 0f)]
    [InlineData((byte)0, -90f)]
    [InlineData((byte)4, -180f)]
    [InlineData((byte)1, 90f)]
    public void FacingDegreesMatchTheReferenceYaw(byte facing, float degrees)
    {
        Assert.Equal(degrees, ChestMesher.FacingDegrees(facing), Tolerance);
    }

    /// <summary>The latch moves to whichever face the chest is turned towards.</summary>
    [Fact]
    public void TheLatchFollowsTheFacing()
    {
        var south = LatchExtentFor(5);
        var west = LatchExtentFor(0);
        var north = LatchExtentFor(4);
        var east = LatchExtentFor(1);

        // South puts it on the far Z face, north on the near one, west near X, east far X.
        Assert.Equal(15f * Pixel, south.MinZ, Tolerance);
        Assert.Equal(16f * Pixel, south.MaxZ, Tolerance);

        Assert.Equal(0f, north.MinZ, Tolerance);
        Assert.Equal(1f * Pixel, north.MaxZ, Tolerance);

        Assert.Equal(0f, west.MinX, Tolerance);
        Assert.Equal(1f * Pixel, west.MaxX, Tolerance);

        Assert.Equal(15f * Pixel, east.MinX, Tolerance);
        Assert.Equal(16f * Pixel, east.MaxX, Tolerance);
    }
}
