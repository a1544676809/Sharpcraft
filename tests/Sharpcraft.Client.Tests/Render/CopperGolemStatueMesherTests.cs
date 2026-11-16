// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Golden tests for the copper golem statue's standing slice. The expected offsets come from the
/// reference <c>CopperGolemModel</c> part constants and <c>CopperGolemStatueBlockRenderer</c>'s
/// <c>translation(0.5, 0, 0.5)</c> plus <c>-facing.getOpposite().toYRot()</c> transform.
/// </summary>
public sealed class CopperGolemStatueMesherTests
{
    /// <summary>
    /// Four horizontal facings times two oxidation stages. The first vertex of the first body cube
    /// is a stable source-derived sample: its world position rotates with facing, while its UV is
    /// the same 64x64 model coordinate for every sprite.
    /// </summary>
    [Theory]
    [InlineData((byte)2, 0, 0.75f, 0.3125f, 0.6875f)]
    [InlineData((byte)2, 1, 0.75f, 0.3125f, 0.6875f)]
    [InlineData((byte)3, 0, 0.25f, 0.3125f, 0.3125f)]
    [InlineData((byte)3, 1, 0.25f, 0.3125f, 0.3125f)]
    [InlineData((byte)4, 0, 0.6875f, 0.3125f, 0.25f)]
    [InlineData((byte)4, 1, 0.6875f, 0.3125f, 0.25f)]
    [InlineData((byte)5, 0, 0.3125f, 0.3125f, 0.75f)]
    [InlineData((byte)5, 1, 0.3125f, 0.3125f, 0.75f)]
    public void StandingStatueSamplesFacingAndOxidation(byte facing, int oxidation, float x, float y, float z)
    {
        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: facing, ChestMaterial: (byte)oxidation)],
            Sprites());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(9 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(9 * 36, geometry.IndexCount);
        var position = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.Equal(x, position.X, 4);
        Assert.Equal(y, position.Y, 4);
        Assert.Equal(z, position.Z, 4);
        Assert.Equal((14f / 64f, 15f / 64f), VertexFieldReader.Uv(geometry.Vertices, 0));
        Assert.Equal(oxidation + 1, VertexFieldReader.SpriteId(geometry.Vertices, 0));
    }

    /// <summary>Every bronze statue direction maps through the reference's opposite toYRot angle.</summary>
    [Theory]
    [InlineData((byte)0, -270f)]
    [InlineData((byte)1, -270f)]
    [InlineData((byte)2, 0f)]
    [InlineData((byte)3, -180f)]
    [InlineData((byte)4, -270f)]
    [InlineData((byte)5, -90f)]
    public void FacingDegreesMatchTheReferenceTransform(byte facing, float expected)
    {
        Assert.Equal(expected, CopperGolemStatueMesher.FacingDegrees(facing), 3);
    }

    /// <summary>
    /// CopperGolemStatueModel.setupAnim applies <c>root.zRot = π</c> to the baked CopperGolem
    /// layer. The expected bounds below are the standing model after that root pose at facing
    /// north; they are deliberately different from the unposed entity model bounds.
    /// </summary>
    [Fact]
    public void StandingStatueAppliesTheReferenceRootPose()
    {
        var state = new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: 0);
        var geometry = new CopperGolemStatueMesher().Build([state], Sprites());
        var (min, max) = Bounds(geometry);

        Assert.Equal(0.0625f, min.X, 4);
        Assert.Equal(0f, min.Y, 4);
        Assert.Equal(0.125f, min.Z, 4);
        Assert.Equal(0.9375f, max.X, 4);
        Assert.Equal(1.4990625f, max.Y, 4);
        Assert.Equal(0.81343746f, max.Z, 4);
    }

    /// <summary>
    /// With the model-space root pose baked into the standing definition, the facing-north statue
    /// reproduces the unposed entity geometry at yaw 180: same vertical bounds and identical vertices.
    /// </summary>
    [Fact]
    public void TheStatueStandingGeometryMatchesTheUnposedEntityModel()
    {
        var sprite = new SpriteRect(0, 0, 64, 64, 1) { RectId = 1, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
        var entityState = new EntityRenderState(1, CopperGolemModelDefinition.CopperGolemEntityTypeId, 0.5f, 0f, 0.5f, 180f, 0f);
        var unposed = new EntityModelMesher().BuildModel(CopperGolemModelDefinition.Definition, entityState, sprite, 64, 64, modelScale: 1f);
        var statue = new CopperGolemStatueMesher().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: 0)],
            [sprite, sprite, sprite, sprite]);

        var unposedBounds = Bounds(unposed);
        var statueBounds = Bounds(statue);
        Assert.Equal(0f, unposedBounds.Min.Y, 4);
        Assert.Equal(1.4990625f, unposedBounds.Max.Y, 4);
        Assert.Equal(0f, statueBounds.Min.Y, 4);
        Assert.Equal(1.4990625f, statueBounds.Max.Y, 4);
        Assert.Equal(unposed.VertexFloats, statue.VertexFloats);
        for (var field = 0; field < unposed.VertexFloats; field++)
        {
            Assert.Equal(
                BitConverter.SingleToUInt32Bits(unposed.Vertices[field]),
                BitConverter.SingleToUInt32Bits(statue.Vertices[field]));
        }
    }

    /// <summary>The four body textures are stitched into the block entity atlas and resolve by stage.</summary>
    [Fact]
    public void TheCopperGolemTexturesAreInTheBlockEntityAtlas()
    {
        Assert.Equal(
            new[]
            {
                "entity/copper_golem/copper_golem",
                "entity/copper_golem/copper_golem_exposed",
                "entity/copper_golem/copper_golem_weathered",
                "entity/copper_golem/copper_golem_oxidized"
            },
            ChestAtlas.CopperGolemStatueTextures);

        var pixels = new byte[64 * 64 * 4];
        var textures = ChestAtlas.CopperGolemStatueTextures.ToDictionary(
            path => path,
            _ => new TextureData(64, 64, pixels),
            StringComparer.Ordinal);
        var atlas = ItemAtlas.FromTextures(textures);

        var table = ChestAtlas.BuildRectTable(atlas);
        for (var stage = 0; stage < ChestAtlas.CopperGolemStatueTextures.Length; stage++)
        {
            var sprite = ChestAtlas.SpriteForCopperGolemStatue(atlas, stage);
            Assert.NotNull(sprite);
            AssertRectInTable(table, sprite!);
        }

        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(3f, 2f, 5f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: CopperGolemStatueMesher.Oxidized)],
            atlas);
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.True(geometry.VertexFloats > 0);
    }

    /// <summary>All four texture stages are packed and use distinct atlas sprites.</summary>
    [Fact]
    public void OxidationStagesSelectDistinctSprites()
    {
        var sprites = Sprites();
        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: CopperGolemStatueMesher.Oxidized)],
            sprites);

        Assert.Equal(CopperGolemStatueMesher.Oxidized + 1, VertexFieldReader.SpriteId(geometry.Vertices, 0));
        Assert.Equal("entity/copper_golem/copper_golem_oxidized", ChestAtlas.CopperGolemStatueTexture(CopperGolemStatueMesher.Oxidized));
    }

    /// <summary>The statue uses the centralized Direction ordinal predicate and its NORTH fallback.</summary>
    [Fact]
    public void TheStatueUsesTheCentralizedDirectionOrdinalPath()
    {
        Assert.Equal(47, VanillaBlockEntityTypes.CopperGolemStatue);
        Assert.True(VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(VanillaBlockEntityTypes.CopperGolemStatue));
        Assert.Equal(2, VanillaBlockEntityTypes.DirectionOrdinalFallback(VanillaBlockEntityTypes.CopperGolemStatue));
        Assert.False(VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(VanillaBlockEntityTypes.Furnace));
    }

    /// <summary>The statue type is registered and does not fall back to a placeholder renderer.</summary>
    [Fact]
    public void BlockEntityRendererHandlesTheStatue()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.CopperGolemStatue, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.DecoratedPot, renderers));
    }

    /// <summary>Rebuilding a warm statue mesher allocates nothing on the steady-state path.</summary>
    [Fact]
    public void RebuildingDoesNotAllocateOnceWarm()
    {
        var mesher = new CopperGolemStatueMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: 0),
            new(3f, 2f, 4f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 5, ChestMaterial: 1)
        ];
        var sprites = Sprites();
        mesher.Build(entities, sprites);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            mesher.Build(entities, sprites);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Theory]
    [InlineData(CopperGolemPose.Standing, 216, 324)]
    [InlineData(CopperGolemPose.Sitting, 264, 396)]
    [InlineData(CopperGolemPose.Running, 216, 324)]
    [InlineData(CopperGolemPose.Star, 216, 324)]
    public void EachPoseHasTheSourceCubeCount(CopperGolemPose pose, int vertices, int indices)
    {
        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: 0, Pose: pose)],
            Sprites());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(vertices, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        Assert.Equal(indices, geometry.IndexCount);
    }

    [Theory]
    [InlineData(CopperGolemPose.Standing, (byte)2, 0, 0.75f, 0.3125f, 0.6875f)]
    [InlineData(CopperGolemPose.Standing, (byte)2, 1, 0.75f, 0.3125f, 0.6875f)]
    [InlineData(CopperGolemPose.Standing, (byte)3, 0, 0.25f, 0.3125f, 0.3125f)]
    [InlineData(CopperGolemPose.Standing, (byte)3, 1, 0.25f, 0.3125f, 0.3125f)]
    [InlineData(CopperGolemPose.Sitting, (byte)2, 0, 0.6875f, -1.125f, 0.7375f)]
    [InlineData(CopperGolemPose.Sitting, (byte)2, 1, 0.6875f, -1.125f, 0.7375f)]
    [InlineData(CopperGolemPose.Sitting, (byte)3, 0, 0.3125f, -1.125f, 0.2625f)]
    [InlineData(CopperGolemPose.Sitting, (byte)3, 1, 0.3125f, -1.125f, 0.2625f)]
    [InlineData(CopperGolemPose.Running, (byte)2, 0, 0.75124f, -1.18749f, 0.69639f)]
    [InlineData(CopperGolemPose.Running, (byte)2, 1, 0.75124f, -1.18749f, 0.69639f)]
    [InlineData(CopperGolemPose.Running, (byte)3, 0, 0.24876f, -1.18749f, 0.30361f)]
    [InlineData(CopperGolemPose.Running, (byte)3, 1, 0.24876f, -1.18749f, 0.30361f)]
    [InlineData(CopperGolemPose.Star, (byte)2, 0, 0.75f, -1.1875f, 0.6875f)]
    [InlineData(CopperGolemPose.Star, (byte)2, 1, 0.75f, -1.1875f, 0.6875f)]
    [InlineData(CopperGolemPose.Star, (byte)3, 0, 0.25f, -1.1875f, 0.3125f)]
    [InlineData(CopperGolemPose.Star, (byte)3, 1, 0.25f, -1.1875f, 0.3125f)]
    public void EachPoseSamplesFacingAndOxidation(
        CopperGolemPose pose,
        byte facing,
        int oxidation,
        float x,
        float y,
        float z)
    {
        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: facing, ChestMaterial: (byte)oxidation, Pose: pose)],
            Sprites());

        var position = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.Equal(x, position.X, 4);
        Assert.Equal(y, position.Y, 4);
        Assert.Equal(z, position.Z, 4);
        var (u, v) = pose == CopperGolemPose.Sitting ? (15f / 64f, 19f / 64f) : (14f / 64f, 15f / 64f);
        var uv = VertexFieldReader.Uv(geometry.Vertices, 0);
        Assert.Equal(u, uv.U, 4);
        Assert.Equal(v, uv.V, 4);
        Assert.Equal(oxidation + 1, VertexFieldReader.SpriteId(geometry.Vertices, 0));
    }

    [Fact]
    public void InvalidPoseFallsBackToStanding()
    {
        var mesher = new CopperGolemStatueMesher();
        var fallback = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, Pose: (CopperGolemPose)99)],
            Sprites());
        var standing = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, Pose: CopperGolemPose.Standing)],
            Sprites());

        Assert.Equal(standing.VertexFloats, fallback.VertexFloats);
        Assert.Equal(standing.IndexCount, fallback.IndexCount);
        Assert.Equal(VertexFieldReader.Position(standing.Vertices, 0), VertexFieldReader.Position(fallback.Vertices, 0));
    }

    private static (Vector3 Min, Vector3 Max) Bounds(DynamicGeometry geometry)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var vertex = 0; vertex < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            var point = new Vector3(geometry.Vertices[vertex], geometry.Vertices[vertex + 1], geometry.Vertices[vertex + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }

    private static SpriteRect[] Sprites()
    {
        var sprites = new SpriteRect[4];
        for (var stage = 0; stage < sprites.Length; stage++)
        {
            sprites[stage] = new SpriteRect(0, 0, 64, 64, 1)
            {
                RectId = stage + 1,
                U0 = 0f,
                V0 = 0f,
                U1 = 1f,
                V1 = 1f
            };
        }

        return sprites;
    }

    private static void AssertRectInTable(float[] table, SpriteRect sprite)
    {
        var offset = sprite.RectId * 4;
        Assert.Equal(sprite.U0, table[offset]);
        Assert.Equal(sprite.V0, table[offset + 1]);
        Assert.Equal(sprite.U1, table[offset + 2]);
        Assert.Equal(sprite.V1, table[offset + 3]);
    }
}
