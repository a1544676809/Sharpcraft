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
    /// Four horizontal facings times two oxidation stages. The emitted first vertex is a stable
    /// source-derived sample; its world position rotates with facing and is taken verbatim from the
    /// validated world-space reference sample (ref://mc-26.3-analysis/vectors/CopperGolemStatueVectors.java,
    /// recorded in Repro/Data/copper-golem-statue-vectors.golden.txt - standing rows, first emitted
    /// vertex). UV (14/64,15/64) is the source face UV, identical for every sprite.
    /// </summary>
    [Theory]
    [InlineData((byte)2, 0, 0.25f, 0.6875f, 0.6875f)]
    [InlineData((byte)2, 1, 0.25f, 0.6875f, 0.6875f)]
    [InlineData((byte)3, 0, 0.75f, 0.6875f, 0.3125f)]
    [InlineData((byte)3, 1, 0.75f, 0.6875f, 0.3125f)]
    [InlineData((byte)4, 0, 0.6875f, 0.6875f, 0.75f)]
    [InlineData((byte)4, 1, 0.6875f, 0.6875f, 0.75f)]
    [InlineData((byte)5, 0, 0.3125f, 0.6875f, 0.25f)]
    [InlineData((byte)5, 1, 0.3125f, 0.6875f, 0.25f)]
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
    /// Bounds of the standing statue at facing north. The source applies <c>root.zRot = PI</c>,
    /// which is the y-down to y-up flip the shared bakery already performs, so these numbers come
    /// from the plain createBodyLayer values with no model height folded in.
    /// </summary>
    [Fact]
    public void StandingStatueStaysInsideItsBlock()
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
    /// Source-pose application check. The old StandingMatchesUnposedEntityModel invariant (a facing-
    /// north statue byte-equal to an unposed entity) was removed: it is not source-true. The reference
    /// CopperGolemStatueModel.setupAnim (ref://mc-26.3-analysis/src/net/minecraft/client/model/object/
    /// statue/CopperGolemStatueModel.java:13-16) overrides root.y=0 and root.zRot=PI and the statue
    /// layer also carries its own child-pivot overrides, so an unposed entity cannot equal the statue.
    /// Equivalence is instead proven against the source oracle by CopperGolemStatueModelSpaceTests
    /// (4 poses x 216 vertices, model space) and CopperGolemStatueGoldenTests (world space).
    /// This test only asserts the statue applies the exact source root pose literals.
    /// </summary>
    [Fact]
    public void StatuePoseAppliesSourceRootOverrides()
    {
        var field = typeof(CopperGolemStatueMesher).GetField("StatuePose",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);
        var pose = (EntityPose)field!.GetValue(null)!;
        var root = pose.Get("root");
        // Source setupAnim: root.y = 0, root.zRot = PI (CopperGolemStatueModel.java:13-16).
        Assert.Equal(0f, root.Position.X);
        Assert.Equal(0f, root.Position.Y);
        Assert.Equal(0f, root.Position.Z);
        Assert.Equal(0f, root.Rotation.X);
        Assert.Equal(0f, root.Rotation.Y);
        Assert.Equal(MathF.PI, root.Rotation.Z, 6);
        // Scale is a delta added to 1 by the frame builder; zero means no scaling.
        Assert.Equal(0f, root.Scale.X);
        Assert.Equal(0f, root.Scale.Y);
        Assert.Equal(0f, root.Scale.Z);
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

    /// <summary>
    /// Reference Y ranges for all four statue poses from <c>CopperGolemStatueModel.setupAnim</c>
    /// (<c>root.y = 0</c>, <c>root.zRot = PI</c>) applied to the four <c>CopperGolemModel</c> layers.
    /// This is an independent gate: a reintroduced +24px baseline or a missing root flip must turn
    /// this red instead of being silently absorbed by re-sampled vertex theories.
    /// Facing rotates about Y only, so north is representative of every facing.
    /// </summary>
    [Theory]
    [InlineData(CopperGolemPose.Standing, 0f, 1.4991f)]
    [InlineData(CopperGolemPose.Sitting, 0f, 1.2491f)]
    [InlineData(CopperGolemPose.Running, -0.0237f, 1.4803f)]
    [InlineData(CopperGolemPose.Star, 0.0042f, 1.4991f)]
    public void EachPoseStaysWithinTheReferenceYRange(CopperGolemPose pose, float expectedMinY, float expectedMaxY)
    {
        const float tolerance = 0.001f;
        var geometry = new CopperGolemStatueMesher().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: 0, Pose: pose)],
            Sprites());
        var (min, max) = Bounds(geometry);

        Assert.InRange(min.Y, expectedMinY - tolerance, expectedMinY + tolerance);
        Assert.InRange(max.Y, expectedMaxY - tolerance, expectedMaxY + tolerance);
    }

    // Vertex-0 world positions from the validated reference sample
    // (ref://mc-26.3-analysis/vectors/CopperGolemStatueVectors.java,
    // Repro/Data/copper-golem-statue-vectors.golden.txt, first emitted vertex per pose/facing).
    [Theory]
    [InlineData(CopperGolemPose.Standing, (byte)2, 0, 0.25f, 0.6875f, 0.6875f)]
    [InlineData(CopperGolemPose.Standing, (byte)2, 1, 0.25f, 0.6875f, 0.6875f)]
    [InlineData(CopperGolemPose.Standing, (byte)3, 0, 0.75f, 0.6875f, 0.3125f)]
    [InlineData(CopperGolemPose.Standing, (byte)3, 1, 0.75f, 0.6875f, 0.3125f)]
    [InlineData(CopperGolemPose.Sitting, (byte)2, 0, 0.3125f, 0.4375f, 0.7375f)]
    [InlineData(CopperGolemPose.Sitting, (byte)2, 1, 0.3125f, 0.4375f, 0.7375f)]
    [InlineData(CopperGolemPose.Sitting, (byte)3, 0, 0.6875f, 0.4375f, 0.2625f)]
    [InlineData(CopperGolemPose.Sitting, (byte)3, 1, 0.6875f, 0.4375f, 0.2625f)]
    [InlineData(CopperGolemPose.Running, (byte)2, 0, 0.281448f, 0.722595f, 0.654547f)]
    [InlineData(CopperGolemPose.Running, (byte)2, 1, 0.281448f, 0.722595f, 0.654547f)]
    [InlineData(CopperGolemPose.Running, (byte)3, 0, 0.718552f, 0.722595f, 0.345453f)]
    [InlineData(CopperGolemPose.Running, (byte)3, 1, 0.718552f, 0.722595f, 0.345453f)]
    [InlineData(CopperGolemPose.Star, (byte)2, 0, 0.25f, 0.6875f, 0.6875f)]
    [InlineData(CopperGolemPose.Star, (byte)2, 1, 0.25f, 0.6875f, 0.6875f)]
    [InlineData(CopperGolemPose.Star, (byte)3, 0, 0.75f, 0.6875f, 0.3125f)]
    [InlineData(CopperGolemPose.Star, (byte)3, 1, 0.75f, 0.6875f, 0.3125f)]
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
