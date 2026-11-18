// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Pins the bell mesher to the reference model: the body cube at five to eleven across, six to
/// thirteen up and five to eleven deep, and the base cube at four to twelve across, four to six up
/// and four to twelve deep, both in sixteenths of a block. These are truth values from the
/// reference, not values read back from the implementation.
/// </summary>
public sealed class BellMesherGoldenTests
{
    private const float Tolerance = 1e-6f;

    /// <summary>The model constants are the reference's pixels over sixteen.</summary>
    [Fact]
    public void ModelConstantsMatchTheReference()
    {
        Assert.Equal(6f / 16f, BellMesher.BodyWidth, Tolerance);
        Assert.Equal(7f / 16f, BellMesher.BodyHeight, Tolerance);
        Assert.Equal(6f / 16f, BellMesher.BodyDepth, Tolerance);
        Assert.Equal(6f / 16f, BellMesher.BodyFeetY, Tolerance);
        Assert.Equal(8f / 16f, BellMesher.BaseWidth, Tolerance);
        Assert.Equal(2f / 16f, BellMesher.BaseHeight, Tolerance);
        Assert.Equal(8f / 16f, BellMesher.BaseDepth, Tolerance);
        Assert.Equal(4f / 16f, BellMesher.BaseFeetY, Tolerance);
        Assert.Equal(32, BellMesher.TextureSize);
    }

    /// <summary>The body box spans five to eleven pixels across and six to thirteen up.</summary>
    [Fact]
    public void TheBodyBoxMatchesTheReferenceBounds()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)];

        var geometry = mesher.Build(entities, Sprite());
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, 0, 24);

        Assert.Equal(5f / 16f, minX, Tolerance);
        Assert.Equal(11f / 16f, maxX, Tolerance);
        Assert.Equal(6f / 16f, minY, Tolerance);
        Assert.Equal(13f / 16f, maxY, Tolerance);
        Assert.Equal(5f / 16f, minZ, Tolerance);
        Assert.Equal(11f / 16f, maxZ, Tolerance);
    }

    /// <summary>The base box spans four to twelve pixels across and four to six up.</summary>
    [Fact]
    public void TheBaseBoxMatchesTheReferenceBounds()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)];

        var geometry = mesher.Build(entities, Sprite());
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, 24, 24);

        Assert.Equal(4f / 16f, minX, Tolerance);
        Assert.Equal(12f / 16f, maxX, Tolerance);
        Assert.Equal(4f / 16f, minY, Tolerance);
        Assert.Equal(6f / 16f, maxY, Tolerance);
        Assert.Equal(4f / 16f, minZ, Tolerance);
        Assert.Equal(12f / 16f, maxZ, Tolerance);
    }

    /// <summary>One bell is two boxes: twenty four vertices and thirty six indices each.</summary>
    [Fact]
    public void OneBellEmitsTwoBoxes()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)];

        var geometry = mesher.Build(entities, Sprite());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(2 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(2 * 36, geometry.IndexCount);
    }

    /// <summary>Two bells in one batch keep their own block offset and the vertex base stays right.</summary>
    [Fact]
    public void TwoBellsArePlacedAtTheirOwnBlocks()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell),
            new(3f, 2f, 4f, VanillaBlockEntityTypes.Bell)
        ];

        var geometry = mesher.Build(entities, Sprite());

        Assert.Equal(2, mesher.LastEntityCount);
        Assert.Equal(2 * 2 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(2 * 2 * 36, geometry.IndexCount);

        // The second bell's body starts after the first bell's two boxes.
        var (minX, maxX, minY, maxY, minZ, maxZ) = Bounds(geometry, 48, 24);
        Assert.Equal(3f + (5f / 16f), minX, Tolerance);
        Assert.Equal(3f + (11f / 16f), maxX, Tolerance);
        Assert.Equal(2f + (6f / 16f), minY, Tolerance);
        Assert.Equal(2f + (13f / 16f), maxY, Tolerance);
        Assert.Equal(4f + (5f / 16f), minZ, Tolerance);
        Assert.Equal(4f + (11f / 16f), maxZ, Tolerance);
    }

    /// <summary>The bell is type id thirty and the registry routes it to a renderer.</summary>
    [Fact]
    public void TheRegistryHandlesTheBellType()
    {
        var renderers = BlockEntityRenderers.Create();

        Assert.Equal(30, VanillaBlockEntityTypes.Bell);
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Bell, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
        // The chest renderer still owns its own types; adding the bell must not take them over.
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Chest, renderers));
    }

    /// <summary>The bell mesher skips other block entity types, including the chest one.</summary>
    [Fact]
    public void NonBellEntitiesAreSkipped()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Chest)];

        var geometry = mesher.Build(entities, Sprite());

        Assert.Equal(0, mesher.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }

    /// <summary>UVs are normalised to the bell's 32 by 32 sheet, not the default 64 by 64 one.</summary>
    [Fact]
    public void TheBellFacesUseThe32PixelSheet()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)];

        var geometry = mesher.Build(entities, Sprite());

        // task-215: source-frame UVs, verified keyed 1e-5 against bell-bone-raw-vectors.reference.txt
        // (RawRenderUnitProbe, BELL layer). The legacy path wrapped these differently; raw uses the
        // literal source cube UVs. First body vertex = texOffs(0,0) corner (12,0) on the 32px sheet.
        var (bodyU, bodyV) = VertexFieldReader.Uv(geometry.Vertices, 0);
        Assert.Equal(12f / 32f, bodyU, 3);
        Assert.Equal(0f, bodyV, 3);

        // First base vertex = texOffs(0,13) corner (16,13) on the 32px sheet.
        var (baseU, baseV) = VertexFieldReader.Uv(geometry.Vertices, 24);
        Assert.Equal(16f / 32f, baseU, 3);
        Assert.Equal(13f / 32f, baseV, 3);
    }

    /// <summary>The bell texture is packed in the block entity atlas and its rect is in the table.</summary>
    [Fact]
    public void TheBellTextureIsPartOfTheBlockEntityAtlas()
    {
        Assert.Equal("entity/bell/bell_body", ChestAtlas.BellTexture);

        var pixel = new byte[32 * 32 * 4];
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [ChestAtlas.BellTexture] = new(32, 32, pixel)
        });

        var bell = ChestAtlas.SpriteForBell(atlas);
        Assert.NotNull(bell);

        var table = ChestAtlas.BuildRectTable(atlas);
        var offset = bell!.RectId * 4;
        Assert.Equal(bell.U0, table[offset]);
        Assert.Equal(bell.V0, table[offset + 1]);
        Assert.Equal(bell.U1, table[offset + 2]);
        Assert.Equal(bell.V1, table[offset + 3]);

        var mesher = new BellMesher();
        var geometry = mesher.Build([new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)], atlas);
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(2 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
    }

    /// <summary>Rebuilding a warm mesher allocates nothing on the steady state path.</summary>
    [Fact]
    public void RebuildingDoesNotAllocateOnceWarm()
    {
        var mesher = new BellMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.Bell),
            new(3f, 2f, 4f, VanillaBlockEntityTypes.Bell)
        ];

        var sprite = Sprite(); // hoisted: SpriteRect is a reference type, so building one per frame would allocate
        mesher.Build(entities, sprite); // warm the buffers

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            mesher.Build(entities, sprite);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    private static SpriteRect Sprite() =>
        new(X: 0, Y: 0, Width: 32, Height: 32, FrameCount: 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };

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
