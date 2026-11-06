// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System;
using System.Collections.Generic;
using System.Linq;
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
/// Elytra wing slab geometry and UVs. The reference <c>ElytraModel.createLayer</c> left wing is
/// <c>texOffs(22, 0).addBox(-10, 0, 0, 10, 20, 2, new CubeDeformation(1.0F))</c> at pivot +5; the right wing is the
/// mirrored counterpart at pivot -5 with box minX 0. Each box therefore spans model x [-5, 5]: the two full-width slabs
/// intentionally overlap on the back, exactly as the reference. Their opposing X rotations (ElytraModel.setupAnim)
/// form the shallow V that keeps the large faces from being coplanar. The expected corners/UVs below are derived from
/// <c>ModelPart.Cube</c> (uniform grow; the mirror operator reflects the cube in X) and the per-face <c>Polygon</c>
/// u/v rectangles, then mapped through our <c>FromVanilla</c> x-mirror + y-up conversion and the <c>WingsLayer</c>
/// +0.125-block (2 model px) z translation.
/// </summary>
public sealed class ElytraWingGeometryTests
{
    private const int ZombieTypeId = 154;

    private static float Px => EntityPlaceholderMesher.ModelScale / 16f;

    private static void AssertClose(float expected, float actual) =>
        Assert.True(MathF.Abs(expected - actual) <= 1e-3f, $"expected {expected}, actual {actual}");

    private static (Vector3 Pos, Vector2 UvPx)[] WingVertices()
    {
        var items = ItemRegistry.LoadEmbedded();
        var elytraId = items.GetId(Identifier.Parse("minecraft:elytra"));
        Assert.True(elytraId >= 0, "elytra item id missing from embedded registry");

        var state = new EntityRenderState(1, ZombieTypeId, 0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, elytraId, -1, -1, -1));
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 32, 1), 64, 64, variants: EntityVariantTable.Builtin);
        var elytra = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra);
        var geometry = elytra.Geometry;

        var count = geometry.VertexFloats / 14;
        var result = new (Vector3, Vector2)[count];
        for (var v = 0; v < count; v++)
        {
            var o = v * 14;
            result[v] = (
                new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]),
                new Vector2(geometry.Vertices[o + 6] * 64f, geometry.Vertices[o + 7] * 32f));
        }

        return result;
    }

    [Fact]
    public void TwoWingSlabsMatchTheReferenceOverlappingCorners()
    {
        var verts = WingVertices();
        Assert.Equal(48, verts.Length); // 2 wings x 6 faces x 4 corners

        // Model-pixel box per wing: the reference ElytraModel box 10x20x2 at tex(22,0) with pivot ±5, grow 1 px on
        // every side => model x [-6,6], y [3,25] (bodyTop 24 - 20 - grow .. bodyTop + grow), z [1,5]. Both wings share
        // the same reference box (the right is mirrored in UV only). Entity transform applies yaw 180 (x,z negated),
        // so world = (-x, y, -z) * px.
        var expected = new (Vector3 Min, Vector3 Max)[]
        {
            (new Vector3(-6f * Px, 3f * Px, -5f * Px), new Vector3(6f * Px, 25f * Px, -1f * Px)),
            (new Vector3(-6f * Px, 3f * Px, -5f * Px), new Vector3(6f * Px, 25f * Px, -1f * Px))
        };
        for (var wing = 0; wing < 2; wing++)
        {
            var wingMin = new Vector3(float.MaxValue);
            var wingMax = new Vector3(float.MinValue);
            for (var v = wing * 24; v < (wing + 1) * 24; v++)
            {
                wingMin = Vector3.Min(wingMin, verts[v].Pos);
                wingMax = Vector3.Max(wingMax, verts[v].Pos);
            }

            AssertClose(expected[wing].Min.X, wingMin.X);
            AssertClose(expected[wing].Min.Y, wingMin.Y);
            AssertClose(expected[wing].Min.Z, wingMin.Z);
            AssertClose(expected[wing].Max.X, wingMax.X);
            AssertClose(expected[wing].Max.Y, wingMax.Y);
            AssertClose(expected[wing].Max.Z, wingMax.Z);
        }
    }

    [Fact]
    public void BothWingSlabsSampleTheReferenceTexelsOnAllSixFaces()
    {
        var verts = WingVertices();

        // Texture-pixel u/v per face, in AddPart emission order. Straight from ModelPart.Cube's Polygon rectangles for a
        // 10x20x2 box at tex(22,0): u0=22 u1=24 u2=34 u22=44 u3=36 u4=46, v0=0 v1=2 v2=22. The mirrored right wing
        // reflects the cube in X, which (bottom line) swaps the WEST/EAST faces' u-range (u0..u1 <-> u2..u3) relative to
        // the left wing; the large NORTH/SOUTH (10x20) faces keep u1..u2 and u3..u4 but flip horizontally.
        var expectedLeft = new (float U, float V)[][]
        {
            [(34f, 0f), (44f, 0f), (44f, 2f), (34f, 2f)], // DOWN
            [(24f, 2f), (34f, 2f), (34f, 0f), (24f, 0f)], // UP
            [(34f, 22f), (36f, 22f), (36f, 2f), (34f, 2f)], // WEST
            [(24f, 22f), (34f, 22f), (34f, 2f), (24f, 2f)], // NORTH (10x20 large face)
            [(22f, 22f), (24f, 22f), (24f, 2f), (22f, 2f)], // EAST
            [(36f, 22f), (46f, 22f), (46f, 2f), (36f, 2f)]  // SOUTH (10x20 large face)
        };
        var expectedRight = new (float U, float V)[][]
        {
            [(44f, 0f), (34f, 0f), (34f, 2f), (44f, 2f)], // DOWN
            [(34f, 2f), (24f, 2f), (24f, 0f), (34f, 0f)], // UP
            [(24f, 22f), (22f, 22f), (22f, 2f), (24f, 2f)], // WEST
            [(34f, 22f), (24f, 22f), (24f, 2f), (34f, 2f)], // NORTH (10x20 large face)
            [(36f, 22f), (34f, 22f), (34f, 2f), (36f, 2f)], // EAST
            [(46f, 22f), (36f, 22f), (36f, 2f), (46f, 2f)]  // SOUTH (10x20 large face)
        };

        for (var wing = 0; wing < 2; wing++)
        {
            var expected = wing == 0 ? expectedLeft : expectedRight;
            for (var face = 0; face < 6; face++)
            {
                for (var corner = 0; corner < 4; corner++)
                {
                    var actual = verts[(wing * 6 + face) * 4 + corner].UvPx;
                    AssertClose(expected[face][corner].U, actual.X);
                    AssertClose(expected[face][corner].V, actual.Y);
                }
            }
        }
    }

    [Fact]
    public void TheRightWingSideFacesAreNotMirroredOnTheWrongTextureChunk()
    {
        // Regression guard for the reported defect: the mirrored (right) slab's WEST/EAST faces must sample the same
        // u-range as the reference WEST/EAST faces, not the opposite side's chunk. Before the fix they landed on
        // u 34..36 instead of 22..24 (and vice versa), i.e. two 2x20 side faces showing the wrong strip of the texture.
        var verts = WingVertices();
        var westMinU = float.MaxValue;
        var westMaxU = float.MinValue;
        var eastMinU = float.MaxValue;
        var eastMaxU = float.MinValue;
        for (var corner = 0; corner < 4; corner++)
        {
            var west = verts[(6 + 2) * 4 + corner].UvPx.X; // right wing WEST
            var east = verts[(6 + 4) * 4 + corner].UvPx.X; // right wing EAST
            westMinU = MathF.Min(westMinU, west);
            westMaxU = MathF.Max(westMaxU, west);
            eastMinU = MathF.Min(eastMinU, east);
            eastMaxU = MathF.Max(eastMaxU, east);
        }

        AssertClose(22f, westMinU);
        AssertClose(24f, westMaxU);
        AssertClose(34f, eastMinU);
        AssertClose(36f, eastMaxU);
    }

    [Fact]
    public void BabyWingsAreMeshedWhenChestHasElytra()
    {
        var items = ItemRegistry.LoadEmbedded();
        var elytraId = items.GetId(Identifier.Parse("minecraft:elytra"));
        var state = new EntityRenderState(
            1, ZombieTypeId, 0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, elytraId, -1, -1, -1),
            IsBaby: true);
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 64, 1), 64, 64, variants: EntityVariantTable.Builtin);
        var elytra = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra);
        Assert.True(elytra.Geometry.VertexFloats > 0, "baby elytra wings must produce geometry");
        Assert.Equal(48, elytra.Geometry.VertexFloats / 14); // 2 wings x 6 faces x 4 corners

        // Golden bounds from MeshTransformer.scaling(0.5): scale then +12.008 y-down => y-up box 1.992..11.992, plus
        // the 0.5 grow on every side => 1.492..12.492 model px. Yaw does not change Y.
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        for (var v = 0; v < 48; v++)
        {
            var y = elytra.Geometry.Vertices[v * 14 + 1];
            minY = MathF.Min(minY, y);
            maxY = MathF.Max(maxY, y);
        }

        AssertClose(1.492f * Px, minY);
        AssertClose(12.492f * Px, maxY);
    }

    [Fact]
    public void StandingWingsFormNonCoplanarDihedralMeetingOnTheSpine()
    {
        // Standing pose: elytraRot = (pi/12, 0, -pi/12). With the reference X->Y->Z vertex rotation order the two
        // mirrored wings are NOT coplanar: they form a V-shaped dihedral that meets exactly on the spine (X = 0), which
        // is what produces the centre seam and removes z-fighting.
        var items = ItemRegistry.LoadEmbedded();
        var elytraId = items.GetId(Identifier.Parse("minecraft:elytra"));
        var state = new EntityRenderState(
            1, ZombieTypeId, 0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, elytraId, -1, -1, -1),
            ElytraRotX: MathF.PI / 12f,
            ElytraRotY: 0f,
            ElytraRotZ: -MathF.PI / 12f);
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 32, 1), 64, 64, variants: EntityVariantTable.Builtin);
        var geometry = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra).Geometry;

        // Face 3 is NORTH (the 10x20 wing texture face). 24 vertices per wing (6 faces x 4).
        Vector3 VertexPos(int wing, int face, int corner)
        {
            var o = ((wing * 6 + face) * 4 + corner) * 14;
            return new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]);
        }

        static Vector3 PlaneNormal(Vector3 a, Vector3 b, Vector3 c) =>
            Vector3.Normalize(Vector3.Cross(b - a, c - a));

        static float PlaneZAt(Vector3 p0, Vector3 n, float x, float y) =>
            p0.Z - ((n.X * (x - p0.X) + n.Y * (y - p0.Y)) / n.Z);

        var l0 = VertexPos(0, 3, 0);
        var l1 = VertexPos(0, 3, 1);
        var l2 = VertexPos(0, 3, 2);
        var r0 = VertexPos(1, 3, 0);
        var r1 = VertexPos(1, 3, 1);
        var r2 = VertexPos(1, 3, 2);

        var leftNormal = PlaneNormal(l0, l1, l2);
        var rightNormal = PlaneNormal(r0, r1, r2);

        // Opposite non-zero X components => a V ridge, not a coplanar pair.
        Assert.True(MathF.Abs(leftNormal.X) > 0.05f);
        AssertClose(-leftNormal.X, rightNormal.X);
        AssertClose(leftNormal.Y, rightNormal.Y);
        AssertClose(leftNormal.Z, rightNormal.Z);

        var sampleY = 14f * Px;
        AssertClose(PlaneZAt(l0, leftNormal, 0f, sampleY), PlaneZAt(r0, rightNormal, 0f, sampleY));

        // Left side of the back: left wing is further behind (smaller world Z).
        var leftSideWorldX = 3f * Px;
        Assert.True(PlaneZAt(l0, leftNormal, leftSideWorldX, sampleY) < PlaneZAt(r0, rightNormal, leftSideWorldX, sampleY));
        // Right side: right wing is further behind.
        var rightSideWorldX = -3f * Px;
        Assert.True(PlaneZAt(r0, rightNormal, rightSideWorldX, sampleY) < PlaneZAt(l0, leftNormal, rightSideWorldX, sampleY));
    }
}
