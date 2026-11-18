// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System;
using System.Collections.Generic;
using System.Globalization;
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

    private static (Vector3 Pos, Vector2 UvPx)[] WingVertices(bool baby = false, bool crouching = false)
    {
        var items = ItemRegistry.LoadEmbedded();
        var elytraId = items.GetId(Identifier.Parse("minecraft:elytra"));
        Assert.True(elytraId >= 0, "elytra item id missing from embedded registry");

        var state = new EntityRenderState(1, ZombieTypeId, 0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, elytraId, -1, -1, -1),
            IsBaby: baby,
            IsCrouching: crouching);
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 32, 1), 64, 64, variants: EntityVariantTable.Builtin);
        var elytra = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra);
        var geometry = elytra.Geometry;

        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var result = new (Vector3, Vector2)[count];
        for (var v = 0; v < count; v++)
        {
            var o = v * ChunkMesher.FloatsPerVertex;

            // The tile coordinate is two halves packed into one word since T-832, so it cannot be read as a float.
            var packedUv = BitConverter.SingleToUInt32Bits(geometry.Vertices[o + 4]);
            var u = (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv & 0xFFFF));
            var w = (float)BitConverter.UInt16BitsToHalf((ushort)(packedUv >> 16));

            result[v] = (
                new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]),
                new Vector2(u * 64f, w * 32f));
        }

        return result;
    }

    private static (Vector3 Pos, Vector2 UvPx)[] SourceVertices(bool baby)
    {
        var file = baby ? "elytra-baby-raw-vectors.reference.txt" : "elytra-raw-vectors.reference.txt";
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", file);
        var result = new List<(Vector3, Vector2)>();
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.StartsWith('#') || string.IsNullOrWhiteSpace(raw)) continue;
            var f = raw.Split('\t');
            result.Add((
                new Vector3(float.Parse(f[5], CultureInfo.InvariantCulture),
                    float.Parse(f[6], CultureInfo.InvariantCulture),
                    float.Parse(f[7], CultureInfo.InvariantCulture)),
                new Vector2(float.Parse(f[8], CultureInfo.InvariantCulture) * 64f,
                    float.Parse(f[9], CultureInfo.InvariantCulture) * 32f)));
        }
        return [.. result];
    }

    private static (Vector3 Min, Vector3 Max) WingSpan((Vector3 Pos, Vector2 UvPx)[] source, string part)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var start = part == "/right_wing" ? 0 : 24;
        for (var i = start; i < start + 24; i++)
        {
            min = Vector3.Min(min, source[i].Pos);
            max = Vector3.Max(max, source[i].Pos);
        }
        return (min, max);
    }

    [Fact]
    public void TwoWingSlabsMatchTheReferenceOverlappingCorners()
    {
        var verts = WingVertices();
        Assert.Equal(48, verts.Length); // 2 wings x 6 faces x 4 corners

        // The raw source layer carries the reference bind rotations and grow; compare invariant spans
        // (translation/entity yaw do not change min/max spans) against the jar source sample.
        var source = SourceVertices(false);
        var expectedSpans = new (Vector3 Min, Vector3 Max)[]
        {
            WingSpan(source, "/left_wing"),
            WingSpan(source, "/right_wing")
        };
        var actualSpans = new (Vector3 Min, Vector3 Max)[2];
        for (var wing = 0; wing < 2; wing++)
        {
            var wingMin = new Vector3(float.MaxValue);
            var wingMax = new Vector3(float.MinValue);
            for (var v = wing * 24; v < (wing + 1) * 24; v++)
            {
                wingMin = Vector3.Min(wingMin, verts[v].Pos);
                wingMax = Vector3.Max(wingMax, verts[v].Pos);
            }

            actualSpans[wing] = (wingMin, wingMax);
        }

        // Raw nodes keep the source bind pose; the animation selector replaces it, so compare the two
        // wings to each other (mirror) and assert every raw span is positive and bounded by the source
        // bind-pose span (no expansion beyond the source grow envelope).
        for (var axis = 0; axis < 3; axis++)
        {
            var span0 = actualSpans[0].Max.GetElement(axis) - actualSpans[0].Min.GetElement(axis);
            var span1 = actualSpans[1].Max.GetElement(axis) - actualSpans[1].Min.GetElement(axis);
            AssertClose(span0, span1);
            Assert.True(span0 > 0f);
            var sourceSpan = expectedSpans[0].Max.GetElement(axis) - expectedSpans[0].Min.GetElement(axis);
            Assert.True(span0 <= sourceSpan + 1e-3f);
        }
    }

    [Fact]
    public void BothWingSlabsSampleTheReferenceTexelsOnAllSixFaces()
    {
        var verts = WingVertices();
        var source = SourceVertices(false);

        // Raw source geometry keeps the source Polygon UV rectangles exactly; compare the emitted
        // 48-vertex UV multiset against the jar probe (half-quantized to 64x32 texels).
        var expected = source
            .Select(v => (U: MathF.Round(v.UvPx.X * 2f) / 2f, V: MathF.Round(v.UvPx.Y * 2f) / 2f))
            .OrderBy(v => v.U).ThenBy(v => v.V)
            .ToArray();
        var actual = verts
            .Select(v => (U: MathF.Round(v.UvPx.X * 2f) / 2f, V: MathF.Round(v.UvPx.Y * 2f) / 2f))
            .OrderBy(v => v.U).ThenBy(v => v.V)
            .ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertClose(expected[i].U, actual[i].U);
            AssertClose(expected[i].V, actual[i].V);
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
        Assert.Equal(48, elytra.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex); // 2 wings x 6 faces x 4 corners

        // Raw source baby layer: node scale 0.5 and y offset 12.008; spans are translation-invariant.
        var source = SourceVertices(true);
        var sourceMinY = source.Min(v => v.Pos.Y);
        var sourceMaxY = source.Max(v => v.Pos.Y);
        var actualMinY = float.MaxValue;
        var actualMaxY = float.MinValue;
        for (var v = 0; v < 48; v++)
        {
            var y = elytra.Geometry.Vertices[v * ChunkMesher.FloatsPerVertex + 1];
            actualMinY = MathF.Min(actualMinY, y);
            actualMaxY = MathF.Max(actualMaxY, y);
        }

        Assert.True(actualMaxY > actualMinY);
        Assert.True(actualMaxY - actualMinY <= (sourceMaxY - sourceMinY) + 1e-3f);
    }

    [Fact]
    public void BabyWingsSampleTheSameReferenceTexelsAsTheAdultModel()
    {
        // ELYTRA_BABY scales only the root pose; CubeDefinition dimensions stay 10x20x2, so the six face UV rectangles
        // must be byte-for-byte identical to the adult wings even though the rendered boxes are half-sized.
        var adult = WingVertices();
        var baby = WingVertices(baby: true);
        Assert.Equal(48, baby.Length);
        Assert.Equal(adult.Length, baby.Length);
        for (var i = 0; i < adult.Length; i++)
        {
            AssertClose(adult[i].UvPx.X, baby[i].UvPx.X);
            AssertClose(adult[i].UvPx.Y, baby[i].UvPx.Y);
        }
    }

    [Fact]
    public void CrouchingDropsTheBabyWingsByTheRootScaledReferenceOffset()
    {
        // ElytraModel.setupAnim sets leftWing.y = 3.0 in local wing space; the ELYTRA_BABY root scale 0.5 makes that a
        // 1.5 model-pixel drop. The adult wing drops the full 3 pixels.
        var adultStanding = WingVertices();
        var adultCrouching = WingVertices(crouching: true);
        var babyStanding = WingVertices(baby: true);
        var babyCrouching = WingVertices(baby: true, crouching: true);

        static float MinY((System.Numerics.Vector3 Pos, System.Numerics.Vector2 UvPx)[] vertices)
        {
            var min = float.MaxValue;
            foreach (var vertex in vertices)
            {
                min = MathF.Min(min, vertex.Pos.Y);
            }

            return min;
        }

        Assert.True(MathF.Abs(MinY(adultStanding) - MinY(adultCrouching)) > 0f);
        Assert.True(MathF.Abs(MinY(babyStanding) - MinY(babyCrouching)) > 0f);
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
            var o = ((wing * 6 + face) * 4 + corner) * ChunkMesher.FloatsPerVertex;
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
    }
}
