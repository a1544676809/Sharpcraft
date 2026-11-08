// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Item model extents drive the item-entity ground offset and layout branch, so the flat-item numbers are pinned to the
/// reference constants (<c>ItemModelGenerator.MIN_Z = 7.5</c>, <c>MAX_Z = 8.5</c>).
/// </summary>
public sealed class ItemModelExtentsTests
{
    [Fact]
    public void FlatItemMatchesTheGeneratedGeometry()
    {
        var flat = ItemModelExtents.FlatItem;
        Assert.Equal(0f, flat.Min.X, 6);
        Assert.Equal(0f, flat.Min.Y, 6);
        Assert.Equal(7.5f / 16f, flat.Min.Z, 6);
        Assert.Equal(1f, flat.Max.X, 6);
        Assert.Equal(1f, flat.Max.Y, 6);
        Assert.Equal(8.5f / 16f, flat.Max.Z, 6);
        Assert.Equal(1f / 16f, flat.Depth, 6);
    }

    [Fact]
    public void AFlatItemIsNotDeeperThanTheThreshold()
    {
        // The reference compares with strict "greater", so a generated item stays in the single-column layout.
        Assert.False(ItemModelExtents.FlatItem.IsDeeperThanFlatItem);
        Assert.Equal(ItemModelExtents.FlatItemDepth, ItemModelExtents.FlatItem.Depth, 6);
        Assert.Equal(0.0625f, ItemModelExtents.FlatItemThreshold, 6);
    }

    [Fact]
    public void ACuboidModelUsesItsElementsAndBecomesDeep()
    {
        var resolution = new ItemModelResolution(
            "minecraft:block/stone",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.Ground] = new(default, new System.Numerics.Vector3(0f, 3f * 0.0625f, 0f),
                    new System.Numerics.Vector3(0.25f, 0.25f, 0.25f))
            },
            Elements:
            [
                new ItemModelElement(
                    new System.Numerics.Vector3(0f, 0f, 0f),
                    new System.Numerics.Vector3(16f, 16f, 16f),
                    new Dictionary<ModelFaceDirection, ItemModelFace>())
            ]);

        var model = ItemModelExtents.FromResolution(resolution);
        Assert.Equal(0f, model.Min.X, 6);
        Assert.Equal(1f, model.Max.X, 6);
        Assert.Equal(1f, model.Max.Z - model.Min.Z, 6);

        // The reference measures the box after the display transform: ground scale 0.25 shrinks the cube to a quarter
        // block, which still clears the flat-item threshold.
        var ground = ItemModelExtents.ForDisplay(resolution, ItemDisplayContext.Ground);
        Assert.Equal(0.25f, ground.Max.X - ground.Min.X, 6);
        Assert.Equal(0.25f, ground.Depth, 6);
        Assert.True(ground.IsDeeperThanFlatItem);
    }

    [Fact]
    public void AFlatModelStaysThinAfterTheDisplayTransform()
    {
        var resolution = new ItemModelResolution(
            "minecraft:item/iron_sword",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.Ground] = new(default, System.Numerics.Vector3.Zero,
                    new System.Numerics.Vector3(0.25f, 0.25f, 0.25f))
            });

        // No elements: the generated sprite box is used, and scaling by a quarter keeps it well under the threshold.
        var ground = ItemModelExtents.ForDisplay(resolution, ItemDisplayContext.Ground);
        Assert.Equal(0.0625f * 0.25f, ground.Depth, 6);
        Assert.False(ground.IsDeeperThanFlatItem);
    }

    [Fact]
    public void AHandheldSwordDropIsHalfABlockWide()
    {
        // items/iron_sword -> item/iron_sword -> item/handheld -> item/generated. Only item/generated declares a ground
        // transform: translation (0,2,0) model pixels and scale 0.5, so a handled drop is 0.5 x 0.5 blocks with a
        // 0.03125 block extrusion - thinner than the flat-item threshold, which keeps it in the single-column layout.
        var resolution = new ItemModelResolution(
            "minecraft:item/iron_sword",
            new Dictionary<string, string> { ["layer0"] = "minecraft:item/iron_sword" },
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>
            {
                [ItemDisplayContext.Ground] = new(
                    default,
                    new System.Numerics.Vector3(0f, 2f * 0.0625f, 0f),
                    new System.Numerics.Vector3(0.5f, 0.5f, 0.5f))
            });

        var ground = ItemModelExtents.ForDisplay(resolution, ItemDisplayContext.Ground);
        Assert.Equal(0.5f, ground.Max.X - ground.Min.X, 6);
        Assert.Equal(0.5f, ground.Max.Y - ground.Min.Y, 6);
        Assert.Equal(0.03125f, ground.Depth, 6);
        Assert.False(ground.IsDeeperThanFlatItem);
        // The stack step is depth * 1.5, the reference offsetZ.
        Assert.Equal(0.046875f, ItemEntityRenderer.BulkZStep(ground), 6);
    }

    [Fact]
    public void SpecialModelsReportTheirAuthoredVerticalExtent()
    {
        // Truth vector from the reference authoring convention. Block-entity and special models are authored Y-up and
        // are submitted without any flip (ChestRenderer adds only a facing rotation; ChestSpecialRenderer adds nothing):
        //   bottom addBox(1,0,1, 14,10,14)              -> y 0..10
        //   lid    addBox(0,0,0, 14,5,14) at offset 9   -> y 9..14
        //   lock   addBox(7,-2,14, 2,4,1) at offset 9   -> y 7..11
        // so the authored box is y 0..14 px = 0..0.875 blocks, resting on zero.
        var chest = ItemModelExtents.FromModel(SpecialItemModelDefinition.Chest);
        Assert.Equal(0f, chest.Min.Y, 4);
        Assert.Equal(14f / 16f, chest.Max.Y, 4);

        // Living-entity models are the other convention: authored Y-down, flipped by the living renderer, which is why
        // a skull box addBox(-4,-8,-4, 8,8,8) sits at y -8..0 in its authored space.
        var skull = ItemModelExtents.FromModel(SpecialItemModelDefinition.Skull);
        Assert.Equal(-0.5f, skull.Min.Y, 4);
        Assert.Equal(0f, skull.Max.Y, 4);
    }

    [Fact]
    public void SpecialModelsAreBakedWithTheLivingEntityFlipped()
    {
        // The bakery applies the *living entity* convention (negate Y, then shift by the 24 px model height), while
        // block-style special models are authored Y-up. So the stored cubes of the chest are the authored box mirrored:
        //   authored bottom y 0..10  -> stored 14..24
        //   authored lid    y 9..14  -> stored 10..15
        // Any renderer that draws the stored cubes as they are (the held-item path does) therefore shows the model
        // upside down; only a conversion back — y_authored = 24 - y_stored — restores the reference orientation.
        var compiled = SpecialItemModelDefinition.Chest.Compiled;
        Assert.NotNull(compiled);
        var bottom = Assert.Single(compiled!.Cubes, cube => cube.Name == "bottom");
        var lid = Assert.Single(compiled.Cubes, cube => cube.Name == "lid");

        Assert.Equal(14f, bottom.Bottom, 4);
        Assert.Equal(10f, bottom.Height, 4);
        Assert.Equal(10f, lid.Bottom, 4);
        Assert.Equal(5f, lid.Height, 4);

        // Storage is therefore upside down relative to the authored layout: the lid sits below the body.
        Assert.True(lid.Bottom < bottom.Bottom);
    }

    [Fact]
    public void AThreeDimensionalModelIsDeeperThanAFlatItem()
    {
        var chest = ItemModelExtents.FromModel(SpecialItemModelDefinition.Chest);
        Assert.True(chest.IsDeeperThanFlatItem);
        // The reference chest body spans 14 of 16 pixels on X, but the lock protrudes in front of it: the lock cube is
        // (7,-2,14,2,4,1) under the (0,9,1) pose, so Z runs 1..16 = 15 pixels.
        Assert.Equal(14f / 16f, chest.Max.X - chest.Min.X, 4);
        Assert.Equal(15f / 16f, chest.Max.Z - chest.Min.Z, 4);
    }

    [Fact]
    public void ASkullCubeIsHalfABlock()
    {
        // Reference SkullModel.createHeadModel: addBox(-4,-8,-4, 8,8,8) model pixels.
        var skull = ItemModelExtents.FromModel(SpecialItemModelDefinition.Skull);
        Assert.Equal(0.5f, skull.Max.X - skull.Min.X, 4);
        Assert.Equal(0.5f, skull.Max.Z - skull.Min.Z, 4);
        Assert.True(skull.IsDeeperThanFlatItem);
    }
}
