// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Pins the decorated pot mesher to the reference's base layer and blank side layer
/// (<c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/DecoratedPotRenderer.java</c>).
/// The geometry constants are the source model's pixels over sixteen, and the facing table is the
/// reference's <c>180 - facing.toYRot()</c> rotation about the block centre.
/// </summary>
public sealed class DecoratedPotMesherGoldenTests
{
    private const float Tolerance = 1e-5f;
    private const float Pixel = 1f / 16f;

    // Vertex ranges for one pot: neck ring (0..23), neck rim (24..47), top (48..51),
    // bottom (52..55), back (56..59), left (60..63), right (64..67), front (68..71).
    private const int NeckRingStart = 0;
    private const int NeckRimStart = 24;
    private const int TopPlaneStart = 48;
    private const int BottomPlaneStart = 52;
    private const int FirstSideStart = 56;
    private const int VerticesPerPot = 72;
    private const int IndicesPerPot = 108;

    /// <summary>The model constants are the reference's pixels over sixteen, with its deformations applied.</summary>
    [Fact]
    public void ModelConstantsMatchTheReference()
    {
        Assert.Equal(32, DecoratedPotMesher.BaseTextureSize);
        Assert.Equal(16, DecoratedPotMesher.SideTextureSize);
        Assert.Equal(17.1f / 16f, DecoratedPotMesher.NeckRingFeetY, Tolerance);
        Assert.Equal(7.8f / 16f, DecoratedPotMesher.NeckRingWidth, Tolerance);
        Assert.Equal(2.8f / 16f, DecoratedPotMesher.NeckRingHeight, Tolerance);
        Assert.Equal(7.8f / 16f, DecoratedPotMesher.NeckRingDepth, Tolerance);
        Assert.Equal(19.8f / 16f, DecoratedPotMesher.NeckRimFeetY, Tolerance);
        Assert.Equal(6.4f / 16f, DecoratedPotMesher.NeckRimWidth, Tolerance);
        Assert.Equal(1.4f / 16f, DecoratedPotMesher.NeckRimHeight, Tolerance);
        Assert.Equal(6.4f / 16f, DecoratedPotMesher.NeckRimDepth, Tolerance);
        Assert.Equal(37f / 16f, DecoratedPotMesher.NeckPoseY, Tolerance);
        Assert.Equal(16f / 16f, DecoratedPotMesher.NeckPoseZ, Tolerance);
    }

    /// <summary>The neck ring sits above the block after the reference part pose: pi about X around y=37.</summary>
    [Fact]
    public void TheNeckRingMatchesTheReferenceBounds()
    {
        var geometry = BuildOne(facing: 2);
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, NeckRingStart, 24);

        Assert.Equal(4.1f / 16f, minX, Tolerance);
        Assert.Equal(11.9f / 16f, maxX, Tolerance);
        Assert.Equal(17.1f / 16f, minY, Tolerance);
        Assert.Equal(19.9f / 16f, maxY, Tolerance);
        Assert.Equal(4.1f / 16f, minZ, Tolerance);
        Assert.Equal(11.9f / 16f, maxZ, Tolerance);
    }

    /// <summary>The inflated neck rim caps the ring; its part pose gives the same rotation and translation.</summary>
    [Fact]
    public void TheNeckRimMatchesTheReferenceBounds()
    {
        var geometry = BuildOne(facing: 2);
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, NeckRimStart, 24);

        Assert.Equal(4.8f / 16f, minX, Tolerance);
        Assert.Equal(11.2f / 16f, maxX, Tolerance);
        Assert.Equal(15.8f / 16f, minY, Tolerance);
        Assert.Equal(17.2f / 16f, maxY, Tolerance);
        Assert.Equal(4.8f / 16f, minZ, Tolerance);
        Assert.Equal(11.2f / 16f, maxZ, Tolerance);
    }

    /// <summary>Top and bottom are the reference's one-pixel-inset 14 by 14 planes at y=16 and y=0.</summary>
    [Fact]
    public void TheTopAndBottomPlanesMatchTheReferenceBounds()
    {
        var geometry = BuildOne(facing: 2);

        var (topMinX, topMaxX, topMinY, topMaxY, topMinZ, topMaxZ) = Bounds(geometry, TopPlaneStart, 4);
        Assert.Equal(1f / 16f, topMinX, Tolerance);
        Assert.Equal(15f / 16f, topMaxX, Tolerance);
        Assert.Equal(1f, topMinY, Tolerance);
        Assert.Equal(1f, topMaxY, Tolerance);
        Assert.Equal(1f / 16f, topMinZ, Tolerance);
        Assert.Equal(15f / 16f, topMaxZ, Tolerance);

        var (bottomMinX, bottomMaxX, bottomMinY, bottomMaxY, bottomMinZ, bottomMaxZ) = Bounds(geometry, BottomPlaneStart, 4);
        Assert.Equal(1f / 16f, bottomMinX, Tolerance);
        Assert.Equal(15f / 16f, bottomMaxX, Tolerance);
        Assert.Equal(0f, bottomMinY, Tolerance);
        Assert.Equal(0f, bottomMaxY, Tolerance);
        Assert.Equal(1f / 16f, bottomMinZ, Tolerance);
        Assert.Equal(15f / 16f, bottomMaxZ, Tolerance);

        Assert.Equal(Vector3.UnitY, Normal(geometry, TopPlaneStart));
        Assert.Equal(-Vector3.UnitY, Normal(geometry, BottomPlaneStart));
        AssertQuadUvs(geometry, TopPlaneStart,
            [(28f / 32f, 27f / 32f), (14f / 32f, 27f / 32f), (14f / 32f, 13f / 32f), (28f / 32f, 13f / 32f)]);
        AssertQuadUvs(geometry, BottomPlaneStart,
            [(14f / 32f, 13f / 32f), (0f, 13f / 32f), (0f, 27f / 32f), (14f / 32f, 27f / 32f)]);
    }

    /// <summary>One pot is the two neck boxes plus the top, bottom and four side planes.</summary>
    [Fact]
    public void OnePotEmitsTwoBoxesAndSixPlanes()
    {
        var mesher = new DecoratedPotMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.DecoratedPot)];

        var geometry = mesher.Build(entities, BaseSprite(), SideSprite());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(VerticesPerPot * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(IndicesPerPot, geometry.IndexCount);
    }

    /// <summary>The four side planes are the blank 14 by 16 planes at the reference part poses.</summary>
    [Fact]
    public void TheFourSidesUseTheBlankSidePlanes()
    {
        var geometry = BuildOne(facing: 2);

        var expected = new (int Start, float MinX, float MaxX, float MinZ, float MaxZ, (float X, float Y, float Z) Centre, Vector3 Normal)[]
        {
            (56, 1f / 16f, 15f / 16f, 1f / 16f, 1f / 16f, (0.5f, 0.5f, 1f / 16f), new Vector3(0f, 0f, -1f)),
            (60, 1f / 16f, 1f / 16f, 1f / 16f, 15f / 16f, (1f / 16f, 0.5f, 0.5f), new Vector3(-1f, 0f, 0f)),
            (64, 15f / 16f, 15f / 16f, 1f / 16f, 15f / 16f, (15f / 16f, 0.5f, 0.5f), new Vector3(1f, 0f, 0f)),
            (68, 1f / 16f, 15f / 16f, 15f / 16f, 15f / 16f, (0.5f, 0.5f, 15f / 16f), new Vector3(0f, 0f, 1f))
        };

        foreach (var (start, expectedMinX, expectedMaxX, expectedMinZ, expectedMaxZ, centre, normal) in expected)
        {
            var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, start, 4);
            Assert.Equal(expectedMinX, minX, Tolerance);
            Assert.Equal(expectedMaxX, maxX, Tolerance);
            Assert.Equal(0f, minY, Tolerance);
            Assert.Equal(1f, maxY, Tolerance);
            Assert.Equal(expectedMinZ, minZ, Tolerance);
            Assert.Equal(expectedMaxZ, maxZ, Tolerance);
            Assert.Equal(centre.X, (minX + maxX) / 2f, Tolerance);
            Assert.Equal(centre.Y, (minY + maxY) / 2f, Tolerance);
            Assert.Equal(centre.Z, (minZ + maxZ) / 2f, Tolerance);
            Assert.Equal(normal.X, Normal(geometry, start).X, Tolerance);
            Assert.Equal(normal.Y, Normal(geometry, start).Y, Tolerance);
            Assert.Equal(normal.Z, Normal(geometry, start).Z, Tolerance);
            AssertQuadUvs(geometry, start,
                [(15f / 16f, 0f), (1f / 16f, 0f), (1f / 16f, 1f), (15f / 16f, 1f)]);
        }
    }

    /// <summary>Six facing samples pin the reference's <c>180 - facing.toYRot()</c> table.</summary>
    [Theory]
    [InlineData((byte)0, -90f)]
    [InlineData((byte)1, -90f)]
    [InlineData((byte)2, 0f)]
    [InlineData((byte)3, 180f)]
    [InlineData((byte)4, 90f)]
    [InlineData((byte)5, -90f)]
    public void FacingDegreesMatchTheReference(byte facing, float expectedDegrees)
    {
        Assert.Equal(expectedDegrees, DecoratedPotMesher.FacingDegrees(facing), Tolerance);
    }

    /// <summary>The front side's normal follows the whole-model rotation for every Direction ordinal.</summary>
    [Theory]
    [InlineData((byte)2, 0f, 1f)]
    [InlineData((byte)3, 0f, -1f)]
    [InlineData((byte)4, 1f, 0f)]
    [InlineData((byte)5, -1f, 0f)]
    [InlineData((byte)0, -1f, 0f)]
    [InlineData((byte)1, -1f, 0f)]
    public void TheFrontSideNormalFollowsTheFacing(byte facing, float expectedX, float expectedZ)
    {
        var geometry = BuildOne(facing);
        var normal = Normal(geometry, FirstSideStart + 12);

        Assert.Equal(expectedX, normal.X, Tolerance);
        Assert.Equal(0f, normal.Y, Tolerance);
        Assert.Equal(expectedZ, normal.Z, Tolerance);
    }

    /// <summary>All four side centres and normals rotate about (0.5, 0.5, 0.5) for the four horizontal facings.</summary>
    [Fact]
    public void TheSidesRotateAroundTheBlockCentre()
    {
        var rows = new (byte Facing, (float X, float Z) Back, Vector3 BackNormal, (float X, float Z) Left, Vector3 LeftNormal, (float X, float Z) Right, Vector3 RightNormal, (float X, float Z) Front, Vector3 FrontNormal)[]
        {
            (2, (0.5f, 1f / 16f), new(0f, 0f, -1f), (1f / 16f, 0.5f), new(-1f, 0f, 0f), (15f / 16f, 0.5f), new(1f, 0f, 0f), (0.5f, 15f / 16f), new(0f, 0f, 1f)),
            (3, (0.5f, 15f / 16f), new(0f, 0f, 1f), (15f / 16f, 0.5f), new(1f, 0f, 0f), (1f / 16f, 0.5f), new(-1f, 0f, 0f), (0.5f, 1f / 16f), new(0f, 0f, -1f)),
            (4, (1f / 16f, 0.5f), new(-1f, 0f, 0f), (0.5f, 15f / 16f), new(0f, 0f, 1f), (0.5f, 1f / 16f), new(0f, 0f, -1f), (15f / 16f, 0.5f), new(1f, 0f, 0f)),
            (5, (15f / 16f, 0.5f), new(1f, 0f, 0f), (0.5f, 1f / 16f), new(0f, 0f, -1f), (0.5f, 15f / 16f), new(0f, 0f, 1f), (1f / 16f, 0.5f), new(-1f, 0f, 0f))
        };

        foreach (var row in rows)
        {
            var geometry = BuildOne(row.Facing);
            AssertSide(geometry, FirstSideStart, row.Back, row.BackNormal);
            AssertSide(geometry, FirstSideStart + 4, row.Left, row.LeftNormal);
            AssertSide(geometry, FirstSideStart + 8, row.Right, row.RightNormal);
            AssertSide(geometry, FirstSideStart + 12, row.Front, row.FrontNormal);
        }
    }

    /// <summary>Two pots in one batch keep their own block offset and the vertex base stays right.</summary>
    [Fact]
    public void TwoPotsArePlacedAtTheirOwnBlocks()
    {
        var mesher = new DecoratedPotMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.DecoratedPot, Facing: 2),
            new(3f, 2f, 4f, VanillaBlockEntityTypes.DecoratedPot, Facing: 2)
        ];

        var geometry = mesher.Build(entities, BaseSprite(), SideSprite());

        Assert.Equal(2, mesher.LastEntityCount);
        Assert.Equal(2 * VerticesPerPot * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(2 * IndicesPerPot, geometry.IndexCount);

        var secondStart = VerticesPerPot;
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, secondStart, 24);
        Assert.Equal(3f + (4.1f / 16f), minX, Tolerance);
        Assert.Equal(3f + (11.9f / 16f), maxX, Tolerance);
        Assert.Equal(2f + (17.1f / 16f), minY, Tolerance);
        Assert.Equal(2f + (19.9f / 16f), maxY, Tolerance);
        Assert.Equal(4f + (4.1f / 16f), minZ, Tolerance);
        Assert.Equal(4f + (11.9f / 16f), maxZ, Tolerance);
    }

    /// <summary>The pot is type id forty-one and the registry routes it to a renderer.</summary>
    [Fact]
    public void TheRegistryHandlesTheDecoratedPotType()
    {
        var renderers = BlockEntityRenderers.Create();

        Assert.Equal(41, VanillaBlockEntityTypes.DecoratedPot);
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.DecoratedPot, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Bell, renderers));
    }

    /// <summary>The pot mesher skips other block entity types.</summary>
    [Fact]
    public void NonPotEntitiesAreSkipped()
    {
        var mesher = new DecoratedPotMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)];

        var geometry = mesher.Build(entities, BaseSprite(), SideSprite());

        Assert.Equal(0, mesher.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }

    /// <summary>The pot base and side textures are packed in the block entity atlas and in its rect table.</summary>
    [Fact]
    public void ThePotTexturesAreInTheBlockEntityAtlas()
    {
        Assert.Equal("entity/decorated_pot/decorated_pot_base", ChestAtlas.DecoratedPotBaseTexture);
        Assert.Equal("entity/decorated_pot/decorated_pot_side", ChestAtlas.DecoratedPotSideTexture);

        var basePixels = new byte[32 * 32 * 4];
        var sidePixels = new byte[16 * 16 * 4];
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [ChestAtlas.DecoratedPotBaseTexture] = new(32, 32, basePixels),
            [ChestAtlas.DecoratedPotSideTexture] = new(16, 16, sidePixels)
        });

        var baseSprite = ChestAtlas.SpriteForDecoratedPotBase(atlas);
        var sideSprite = ChestAtlas.SpriteForDecoratedPotSide(atlas);
        Assert.NotNull(baseSprite);
        Assert.NotNull(sideSprite);

        var table = ChestAtlas.BuildRectTable(atlas);
        AssertRectInTable(table, baseSprite!);
        AssertRectInTable(table, sideSprite!);

        var mesher = new DecoratedPotMesher();
        var geometry = mesher.Build([new(0f, 0f, 0f, VanillaBlockEntityTypes.DecoratedPot)], atlas);
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(VerticesPerPot * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
    }

    /// <summary>Rebuilding a warm mesher allocates nothing on the steady state path.</summary>
    [Fact]
    public void RebuildingDoesNotAllocateOnceWarm()
    {
        var mesher = new DecoratedPotMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.DecoratedPot, Facing: 2),
            new(3f, 2f, 4f, VanillaBlockEntityTypes.DecoratedPot, Facing: 3)
        ];

        var baseSprite = BaseSprite();
        var sideSprite = SideSprite();
        mesher.Build(entities, baseSprite, sideSprite);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            mesher.Build(entities, baseSprite, sideSprite);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    private static DynamicGeometry BuildOne(byte facing)
    {
        var mesher = new DecoratedPotMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.DecoratedPot, Facing: facing)];
        return mesher.Build(entities, BaseSprite(), SideSprite());
    }

    private static SpriteRect BaseSprite() =>
        new(X: 0, Y: 0, Width: 32, Height: 32, FrameCount: 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };

    private static SpriteRect SideSprite() =>
        new(X: 0, Y: 0, Width: 16, Height: 16, FrameCount: 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };

    private static void AssertRectInTable(float[] table, SpriteRect rect)
    {
        var offset = rect.RectId * 4;
        Assert.Equal(rect.U0, table[offset]);
        Assert.Equal(rect.V0, table[offset + 1]);
        Assert.Equal(rect.U1, table[offset + 2]);
        Assert.Equal(rect.V1, table[offset + 3]);
    }

    private static void AssertSide(DynamicGeometry geometry, int start, (float X, float Z) centre, Vector3 normal)
    {
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, start, 4);
        Assert.Equal(centre.X, (minX + maxX) / 2f, Tolerance);
        Assert.Equal(0.5f, (minY + maxY) / 2f, Tolerance);
        Assert.Equal(centre.Z, (minZ + maxZ) / 2f, Tolerance);

        var actual = Normal(geometry, start);
        Assert.Equal(normal.X, actual.X, Tolerance);
        Assert.Equal(normal.Y, actual.Y, Tolerance);
        Assert.Equal(normal.Z, actual.Z, Tolerance);
    }

    private static void AssertQuadUvs(DynamicGeometry geometry, int start, (float U, float V)[] expected)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, start + i);
            Assert.Equal(expected[i].U, u, 3);
            Assert.Equal(expected[i].V, v, 3);
        }
    }

    private static Vector3 Normal(DynamicGeometry geometry, int start)
    {
        var a = Position(geometry, start);
        var b = Position(geometry, start + 1);
        var c = Position(geometry, start + 2);
        return Vector3.Normalize(Vector3.Cross(b - a, c - b));
    }

    private static Vector3 Position(DynamicGeometry geometry, int vertex)
    {
        var offset = vertex * ChunkMesher.FloatsPerVertex;
        return new Vector3(geometry.Vertices[offset], geometry.Vertices[offset + 1], geometry.Vertices[offset + 2]);
    }

    private static (float MinX, float MaxX, float MinY, float MaxY, float MinZ, float MaxZ) Bounds(
        DynamicGeometry geometry,
        int startVertex,
        int vertexCount)
    {
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        var minZ = float.MaxValue;
        var maxZ = float.MinValue;

        for (var vertex = startVertex; vertex < startVertex + vertexCount; vertex++)
        {
            var offset = vertex * ChunkMesher.FloatsPerVertex;
            var x = geometry.Vertices[offset];
            var y = geometry.Vertices[offset + 1];
            var z = geometry.Vertices[offset + 2];
            minX = MathF.Min(minX, x);
            maxX = MathF.Max(maxX, x);
            minY = MathF.Min(minY, y);
            maxY = MathF.Max(maxY, y);
            minZ = MathF.Min(minZ, z);
            maxZ = MathF.Max(maxZ, z);
        }

        return (minX, maxX, minY, maxY, minZ, maxZ);
    }
}
