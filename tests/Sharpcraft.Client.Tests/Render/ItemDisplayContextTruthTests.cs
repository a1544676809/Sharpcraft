// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// L3 of the item-render equivalence layer: display-context truth vectors taken from the reference
/// <c>ItemTransform</c> (ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/ItemTransform.java).
/// <para>
/// The reference composes, in pose-stack (column-vector) order, <c>T(translation) · R · S · T(-0.5)</c> where the JSON
/// translation is already multiplied by 1/16 and clamped to ±5, and the scale is clamped to ±4. Because Sharpcraft uses
/// row vectors (<c>p' = p · M</c>) the same transform is the reversed product, so the model centre maps to the
/// translation and the linear part carries the scale — both rotation-independent, which is what these tests pin.
/// </para>
/// </summary>
public sealed class ItemDisplayContextTruthTests
{
    private const float Pixel = 1f / 16f;

    private static ItemModelResolution Flat(ItemDisplayContext context, ItemDisplayTransform transform) =>
        new(
            "minecraft:item/probe",
            new Dictionary<string, string> { ["layer0"] = "minecraft:item/probe" },
            new Dictionary<ItemDisplayContext, ItemDisplayTransform> { [context] = transform });

    private static ItemDisplayTransform Uniform(float rotationDegrees, float translationPixels, float scale) =>
        new(new Vector3(rotationDegrees, 0f, 0f), new Vector3(0f, translationPixels, 0f), new Vector3(scale, scale, scale));

    [Theory]
    [InlineData("gui", 0.625f)]
    [InlineData("ground", 0.25f)]
    [InlineData("fixed", 0.5f)]
    [InlineData("thirdperson_righthand", 0.375f)]
    public void EachDisplayContextScalesTheModelByItsOwnFactor(string name, float scale)
    {
        // Reference values shipped by vanilla item templates (item/template_chest): gui 0.625, ground 0.25, fixed 0.5,
        // thirdperson_righthand 0.375, all with rotation [0,0,0] for ground and non-zero rotation elsewhere.
        var context = ContextOf(name);
        var transform = new ItemDisplayTransform(Vector3.Zero, Vector3.Zero, new Vector3(scale, scale, scale));
        var resolution = Flat(context, transform);

        var extents = ItemModelExtents.ForDisplay(resolution, context);
        var size = extents.Max - extents.Min;

        // A flat generated item is 1 block wide/high and 1/16 deep; with no rotation the display scale applies uniformly.
        Assert.Equal(scale, size.X, 4);
        Assert.Equal(scale, size.Y, 4);
        Assert.Equal(scale * (1f / 16f), size.Z, 4);
    }

    [Fact]
    public void TheModelCentreMapsToTheDisplayTranslation()
    {
        // Chest thirdperson_righthand as shipped: rotation [75, 315, 0], translation [0, 2.5, 0] px, scale 0.375.
        var transform = new ItemDisplayTransform(
            new Vector3(75f, 315f, 0f),
            new Vector3(0f, 2.5f * Pixel, 0f),
            new Vector3(0.375f, 0.375f, 0.375f));

        var matrix = transform.ToMatrix();
        var centre = Vector3.Transform(new Vector3(0.5f, 0.5f, 0.5f), matrix);

        Assert.Equal(0f, centre.X, 5);
        Assert.Equal(2.5f * Pixel, centre.Y, 5);
        Assert.Equal(0f, centre.Z, 5);
    }

    [Fact]
    public void TheLinearPartCarriesTheScale()
    {
        var transform = new ItemDisplayTransform(
            new Vector3(75f, 315f, 0f),
            new Vector3(0f, 2.5f * Pixel, 0f),
            new Vector3(0.375f, 0.375f, 0.375f));

        var matrix = transform.ToMatrix();
        foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            Assert.Equal(0.375f, Vector3.TransformNormal(axis, matrix).Length(), 5);
        }
    }

    [Fact]
    public void NoTransformOnlyCentresTheModel()
    {
        // Reference ItemTransform.apply: NO_TRANSFORM is the single special case that only translates by (-0.5,-0.5,-0.5).
        var matrix = ItemDisplayTransform.None.ToMatrix();
        var expected = Matrix4x4.CreateTranslation(-0.5f, -0.5f, -0.5f);
        Assert.Equal(expected, matrix);
    }

    [Fact]
    public void TheLeftHandFixNegatesTranslationXAndRotationsYAndZ()
    {
        // Reference: applyLeftHandFix negates translation.x, rotation.y and rotation.z, leaving translation.y/z and
        // rotation.x untouched.
        var transform = new ItemDisplayTransform(
            new Vector3(10f, 20f, 30f),
            new Vector3(1f, 2f, 3f),
            new Vector3(0.5f, 0.5f, 0.5f));

        var fixedTransform = new ItemDisplayTransform(
            new Vector3(10f, -20f, -30f),
            new Vector3(-1f, 2f, 3f),
            new Vector3(0.5f, 0.5f, 0.5f));

        Assert.Equal(fixedTransform.ToMatrix(), transform.ToMatrix(applyLeftHandFix: true));
    }

    [Fact]
    public void TheRotationMatchesTheReferenceQuaternion()
    {
        // Golden vector captured by running the reference maths directly against JOML 1.10.9, the version the game ships:
        //   new Quaternionf().rotationXYZ(rad(75), rad(315), rad(0))
        // gives the column-vector rotation whose unit-axis images are
        //   X -> ( 0.707107, -0.683013,  0.183013)
        //   Y -> ( 0.000000,  0.258819,  0.965926)
        // Both sides use row vectors (p' = p · M), so the images are comparable directly; the display scale (0.375 for
        // chest thirdperson_righthand) multiplies them.
        var transform = new ItemDisplayTransform(
            new Vector3(75f, 315f, 0f),
            new Vector3(0f, 2.5f * Pixel, 0f),
            new Vector3(0.375f, 0.375f, 0.375f));
        var matrix = transform.ToMatrix();
        const float scale = 0.375f;

        // JOML mutates row vectors as well, so its printed rows are already the unit-axis images; scale them.
        var expectedX = new Vector3(0.707107f, -0.683013f, 0.183013f) * scale;
        var expectedY = new Vector3(0.000000f, 0.258819f, 0.965926f) * scale;
        var expectedZ = new Vector3(-0.707107f, -0.683013f, 0.183013f) * scale;

        var actualX = Vector3.TransformNormal(Vector3.UnitX, matrix);
        var actualY = Vector3.TransformNormal(Vector3.UnitY, matrix);
        var actualZ = Vector3.TransformNormal(Vector3.UnitZ, matrix);

        Assert.Equal(expectedX.X, actualX.X, 4);
        Assert.Equal(expectedX.Y, actualX.Y, 4);
        Assert.Equal(expectedX.Z, actualX.Z, 4);
        Assert.Equal(expectedY.X, actualY.X, 4);
        Assert.Equal(expectedY.Y, actualY.Y, 4);
        Assert.Equal(expectedY.Z, actualY.Z, 4);
        Assert.Equal(expectedZ.X, actualZ.X, 4);
        Assert.Equal(expectedZ.Y, actualZ.Y, 4);
        Assert.Equal(expectedZ.Z, actualZ.Z, 4);
    }

    private static ItemDisplayContext ContextOf(string name) => name switch
    {
        "gui" => ItemDisplayContext.Gui,
        "ground" => ItemDisplayContext.Ground,
        "fixed" => ItemDisplayContext.Fixed,
        "thirdperson_righthand" => ItemDisplayContext.ThirdPersonRightHand,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
