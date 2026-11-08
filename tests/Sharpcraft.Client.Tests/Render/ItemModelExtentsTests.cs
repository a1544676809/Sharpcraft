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
