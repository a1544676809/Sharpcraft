// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Vanilla mesh scaling bake tests (T-667). Pins the reference <c>MeshTransformer.scaling</c> math that the baby elytra
/// used to hand-code: yOffset = 24.016·(1−factor) in the y-down frame, geometry scaled, UV sampling pinned to the
/// original cube, and all per-part metadata/selectors preserved.
/// </summary>
public sealed class VanillaMeshBakeTests
{
    private const int ZombieTypeId = 154;

    private static float Px => EntityPlaceholderMesher.ModelScale / 16f;

    private static void AssertClose(float expected, float actual, float tolerance = 1e-3f) =>
        Assert.True(MathF.Abs(expected - actual) <= tolerance, $"expected {expected}, actual {actual}");

    /// <summary>The reference ElytraModel left/right wing (10×20×2 at tex 22,0, pivot ±5, grow 1) in its 24.008 frame.</summary>
    private static EntityModelPart ReferenceWing(bool right) => LegacyModelPartFactory.FromVanilla(
        right ? "equipment_Elytra_4_right_wing" : "equipment_Elytra_4_left_wing",
        right ? -5f : 5f,
        0f,
        0f,
        right ? 0f : -10f,
        0f,
        0f,
        10f,
        20f,
        2f,
        22,
        0,
        mirror: right,
        modelHeight: 24f,
        texturePath: "entity/equipment/wings/elytra",
        grow: 1f,
        kind: EntityGeometryLayerKind.Elytra);

    [Fact]
    public void ScaleByOneIsAnExactIdentity()
    {
        var wings = new[] { ReferenceWing(false), ReferenceWing(true) };
        var baked = VanillaMeshBake.Scale(wings, 1f);

        Assert.Equal(wings.Length, baked.Length);
        for (var i = 0; i < wings.Length; i++)
        {
            Assert.Equal(wings[i], baked[i]);
        }
    }

    [Fact]
    public void ScalingAnAdultElytraWingByHalfMatchesTheBabyExpectations()
    {
        var scaled = VanillaMeshBake.Scale(ReferenceWing(false), 0.5f);

        // Reference scaling(0.5): yDown' = 0.5·yDown + 12.008, converted back to y-up at modelHeight 24.
        AssertClose(1.992f, scaled.Bottom);
        AssertClose(11.992f, scaled.PivotY);
        Assert.Equal(5f, scaled.Width);
        Assert.Equal(10f, scaled.Height);
        Assert.Equal(1f, scaled.Depth);
        Assert.Equal(0.5f, scaled.Grow);
        AssertClose(-2.5f, scaled.PivotX);
        AssertClose(0f, scaled.PivotZ);
        AssertClose(0.5f, scaled.CentreZ);

        // UV sampling stays on the original 10×20×2 cube even though the geometry is half sized.
        Assert.Equal(10, scaled.UvWidth);
        Assert.Equal(20, scaled.UvHeight);
        Assert.Equal(2, scaled.UvDepth);

        // The WingsLayer +2 model-px z shift is applied after the bake (unscaled), giving the legacy 2 / 2.5.
        var withLayer = scaled with { CentreZ = scaled.CentreZ + 2f, PivotZ = scaled.PivotZ + 2f };
        AssertClose(2f, withLayer.PivotZ);
        AssertClose(2.5f, withLayer.CentreZ);

        // The migrated WithElytra helper snaps the sub-pixel result to the historical whole-pixel baby pivots.
        var body = LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16);
        var migrated = EntityModelLayerHelpers.WithElytra([body], baby: true)
            .Single(part => part.Name == "equipment_Elytra_4_left_wing");
        Assert.Equal(2f, migrated.Bottom);
        Assert.Equal(12f, migrated.PivotY);
        Assert.Equal(body.CentreZ + 2f, migrated.PivotZ);
        Assert.Equal(body.CentreZ + 2.5f, migrated.CentreZ);
        Assert.Equal(5f, migrated.Width);
        Assert.Equal(10f, migrated.Height);
        Assert.Equal(1f, migrated.Depth);
        Assert.Equal(0.5f, migrated.Grow);
        Assert.Equal(10, migrated.UvWidth);
        Assert.Equal(20, migrated.UvHeight);
        Assert.Equal(2, migrated.UvDepth);
    }

    [Fact]
    public void ScalingTwicePreservesTheOriginalUvBox()
    {
        var first = VanillaMeshBake.Scale(ReferenceWing(false), 0.5f);
        var second = VanillaMeshBake.Scale(first, 0.5f);

        // Geometry shrinks again, but the UV sampling rectangle stays at the original 10x20x2 cube.
        Assert.Equal(2.5f, second.Width);
        Assert.Equal(5f, second.Height);
        Assert.Equal(0.5f, second.Depth);
        Assert.Equal(10, second.UvWidth);
        Assert.Equal(20, second.UvHeight);
        Assert.Equal(2, second.UvDepth);
    }

    [Fact]
    public void MirroredPartMetadataSurvivesTheBake()
    {
        var right = ReferenceWing(true);
        var scaled = VanillaMeshBake.Scale(right, 0.5f);

        Assert.Equal("equipment_Elytra_4_right_wing", scaled.Name);
        Assert.True(scaled.Mirror);
        Assert.Equal("entity/equipment/wings/elytra", scaled.TexturePath);
        Assert.Equal(22, scaled.TextureX);
        Assert.Equal(0, scaled.TextureY);
        AssertClose(2.5f, scaled.PivotX);
        Assert.Equal(EntityGeometryLayerKind.Elytra, scaled.Kind);
    }

    [Fact]
    public void WithElytraBabyKeepsTheSameSixFaceUvsAsAdult()
    {
        var adult = WingUvPixels(baby: false);
        var baby = WingUvPixels(baby: true);

        Assert.Equal(48, adult.Length); // 2 wings × 6 faces × 4 corners
        Assert.Equal(adult.Length, baby.Length);
        for (var i = 0; i < adult.Length; i++)
        {
            AssertClose(adult[i].X, baby[i].X);
            AssertClose(adult[i].Y, baby[i].Y);
        }
    }

    [Fact]
    public void WithElytraBabyPreservesTheCrouchYOffsetSelector()
    {
        var body = LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16);
        var wings = EntityModelLayerHelpers.WithElytra([body], baby: true);
        var wing = Assert.Single(wings, part => part.Name == "equipment_Elytra_4_left_wing");
        Assert.NotNull(wing.YOffsetSelector);

        var standing = new EntityRenderState(1, ZombieTypeId, 0f, 0f, 0f, 0f, 0f, IsBaby: true, IsCrouching: false);
        var crouching = new EntityRenderState(1, ZombieTypeId, 0f, 0f, 0f, 0f, 0f, IsBaby: true, IsCrouching: true);

        AssertClose(0f, wing.YOffsetSelector!(standing));
        AssertClose(-1.5f, wing.YOffsetSelector!(crouching));
    }

    /// <summary>Mirrors ElytraWingGeometryTests: builds the elytra layer and returns per-vertex texture-pixel UVs.</summary>
    private static Vector2[] WingUvPixels(bool baby)
    {
        var items = ItemRegistry.LoadEmbedded();
        var elytraId = items.GetId(Identifier.Parse("minecraft:elytra"));
        Assert.True(elytraId >= 0, "elytra item id missing from the embedded registry");

        var state = new EntityRenderState(
            1,
            ZombieTypeId,
            0f,
            0f,
            0f,
            0f,
            0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, elytraId, -1, -1, -1),
            IsBaby: baby);
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 32, 1), 64, 64, variants: EntityVariantTable.Builtin);
        var elytra = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra);
        var geometry = elytra.Geometry;

        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new Vector2[count];
        for (var v = 0; v < count; v++)
        {
            var offset = v * ChunkMesher.FloatsPerVertex;
            var packedUv = BitConverter.SingleToUInt32Bits(geometry.Vertices[offset + 4]);
            result[v] = new Vector2(
                (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv & 0xFFFF)) * 64f,
                (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv >> 16)) * 32f);
        }

        return result;
    }
}
