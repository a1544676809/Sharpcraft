// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Parent/local-pose composition (docs/analysis/render/entity-model-approximations.md). The horse ear is a three-level
/// chain (head_parts → head → right_ear) with the rotation only on head_parts, so it exercises the recursion without
/// depending on animation.
/// </summary>
[Collection(NonParallelCollection.Name)]
public sealed class EntityModelHierarchyTests
{
    private static Vector3[] Vertices(EntityModelDefinition model, int typeId, string onlyPart)
    {
        var mesher = new EntityModelMesher();
        var entity = new EntityRenderState(1, typeId, 0f, 0f, 0f, 180f, 0f);
        var sprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, 64, 64, 1);
        var geometry = mesher.Build([entity], sprite, 64, 64, onlyPart: onlyPart, variants: EntityVariantTable.Builtin);
        var points = new Vector3[geometry.VertexFloats / 14];
        for (var i = 0; i < points.Length; i++)
        {
            var o = i * 14;
            points[i] = new Vector3(geometry.Vertices[o], geometry.Vertices[o + 1], geometry.Vertices[o + 2]);
        }

        return points;
    }

    private static (Vector3 Min, Vector3 Max) Bounds(Vector3[] points)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var point in points)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }

    [Fact]
    public void TheHorseEarLandsWhereTheReferenceCompositionPutsIt()
    {
        // right_ear is a head child with a zero pose; head is a zero-pose child of head_parts. The reference therefore
        // places the ear at head_parts pivot + RotX(+30deg) * cube. Recomputed by hand in the baked y-up space:
        // x in [0.55, 2.55], y in [30.66, 33.758], z in [-15.036, -12.67] model pixels.
        var geometry = Vertices(HorseModelDefinition.Definition, HorseModelDefinition.HorseEntityTypeId, "right_ear");
        Assert.NotEmpty(geometry);

        const float px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;
        var (min, max) = Bounds(geometry);

        Assert.Equal(0.55f * px, min.X, 3);
        Assert.Equal(2.55f * px, max.X, 3);
        Assert.Equal(30.66f * px, min.Y, 2);
        Assert.Equal(33.758f * px, max.Y, 2);
        Assert.Equal(-15.036f * px, min.Z, 2);
        Assert.Equal(-12.67f * px, max.Z, 2);
    }

    [Fact]
    public void TheRabbitHeadComposesBodyAndHeadRotations()
    {
        // body xRot -22.5deg -> head xRot +22.5deg -> cube; hand-composed in the baked y-up space.
        var geometry = Vertices(RabbitModelDefinition.Definition, RabbitModelDefinition.RabbitEntityTypeId, "head");
        var (min, max) = Bounds(geometry);

        const float px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;
        Assert.Equal(-2.5f * px, min.X, 3);
        Assert.Equal(2.5f * px, max.X, 3);
        Assert.Equal(6.998f * px, min.Y, 2);
        Assert.Equal(11.998f * px, max.Y, 2);
        Assert.Equal(-5.478f * px, min.Z, 2);
        Assert.Equal(-0.478f * px, max.Z, 2);
    }

    [Fact]
    public void TheCamelHeadCubesFollowTheBodyAndHeadPivots()
    {
        var geometry = Vertices(CamelModelDefinition.Definition, CamelModelDefinition.CamelEntityTypeId, "head_lower");
        var (min, max) = Bounds(geometry);

        const float px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;
        Near(-3.5f * px, min.X);
        Near(3.5f * px, max.X);
        Near(22f * px, min.Y);
        Near(30f * px, max.Y);
        Near(-25f * px, min.Z);
        Near(-6f * px, max.Z);
    }

    private static void Near(float expected, float actual) =>
        Assert.True(MathF.Abs(expected - actual) < 1e-4f, $"expected {expected}, actual {actual}");
}
