// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Dropped-item transform values, pinned to the reference formulas
/// (<c>ItemEntityRenderer.submit</c> and <c>ItemEntity.getSpin</c>).
/// </summary>
public sealed class ItemEntityRendererTests
{
    [Fact]
    public void BobOscillatesAroundTenPercentOfABlock()
    {
        // bob = sin(age/10 + phase) * 0.1 + 0.1
        Assert.Equal(0.2f, ItemEntityRenderer.BobHeight(ageInTicks: 0f, bobPhase: MathF.PI / 2f), 6);
        Assert.Equal(0.0f, ItemEntityRenderer.BobHeight(ageInTicks: 0f, bobPhase: -MathF.PI / 2f), 6);
        Assert.Equal(0.1f, ItemEntityRenderer.BobHeight(ageInTicks: 0f, bobPhase: 0f), 6);
        // One full turn of the age term is 2π·10 ticks.
        Assert.Equal(
            ItemEntityRenderer.BobHeight(0f, 0.3f),
            ItemEntityRenderer.BobHeight(ItemEntityRenderer.BobAgeDivisor * MathF.PI * 2f, 0.3f),
            5);
    }

    [Fact]
    public void SpinAddsThePhaseToAgeOverTwenty()
    {
        Assert.Equal(1f, ItemEntityRenderer.Spin(ageInTicks: 20f, bobPhase: 0f), 6);
        Assert.Equal(2.5f, ItemEntityRenderer.Spin(ageInTicks: 20f, bobPhase: 1.5f), 6);
        Assert.Equal(0.5f, ItemEntityRenderer.Spin(ageInTicks: 0f, bobPhase: 0.5f), 6);
    }

    [Fact]
    public void GroundOffsetLiftsTheModelSoItsLowestPointSitsAtTheClearance()
    {
        // A generated item's lowest point is y = 0, so the offset is exactly the 1/16 clearance.
        Assert.Equal(0.0625f, ItemEntityRenderer.GroundOffset(ItemModelExtents.FlatItem), 6);

        // A skull box occupies y -8..0 model pixels, so its lowest point is half a block below the origin and the
        // reference lift is 0.5 + 1/16.
        var skull = ItemModelExtents.FromModel(SpecialItemModelDefinition.Skull);
        Assert.Equal(-0.5f, skull.Min.Y, 6);
        Assert.Equal(0.5f + 0.0625f, ItemEntityRenderer.GroundOffset(skull), 6);
    }

    [Fact]
    public void AFlatItemStaysInTheSingleColumnLayout()
    {
        Assert.False(ItemEntityRenderer.UsesScatteredLayout(ItemModelExtents.FlatItem));
        // offsetZ = modelDepth * 1.5 = (1/16) * 1.5
        Assert.Equal(0.09375f, ItemEntityRenderer.BulkZStep(ItemModelExtents.FlatItem), 6);
        // Five flat items: the first sits at -(offsetZ * 4 / 2) and the column advances by one offsetZ per item.
        Assert.Equal(-0.1875f, ItemEntityRenderer.BulkStartZ(ItemModelExtents.FlatItem, 5), 6);
        Assert.Equal(0f, ItemEntityRenderer.BulkStartZ(ItemModelExtents.FlatItem, 1), 6);
    }

    [Fact]
    public void AThickModelTakesTheScatteredLayout()
    {
        var chest = ItemModelExtents.FromModel(SpecialItemModelDefinition.Chest);
        Assert.True(ItemEntityRenderer.UsesScatteredLayout(chest));
    }

    [Fact]
    public void ScatterOffsetSpansPlusMinusFifteenPercent()
    {
        Assert.Equal(-0.15f, ItemEntityRenderer.ScatterOffset(0f), 6);
        Assert.Equal(0f, ItemEntityRenderer.ScatterOffset(0.5f), 6);
        Assert.Equal(0.15f, ItemEntityRenderer.ScatterOffset(1f), 6);
    }

    [Fact]
    public void RootTransformTranslatesThenSpins()
    {
        var extents = ItemModelExtents.FlatItem;
        var matrix = ItemEntityRenderer.RootTransform(ageInTicks: 0f, bobPhase: 0f, extents);

        // Translation applies first: the origin lands at y = bob(0) + groundOffset = 0.1 + 0.0625.
        var origin = System.Numerics.Vector3.Transform(System.Numerics.Vector3.Zero, matrix);
        Assert.Equal(0.1625f, origin.Y, 6);
        Assert.Equal(0f, origin.X, 6);
        Assert.Equal(0f, origin.Z, 6);

        // Spin then rotates about that origin: at age 20 the angle is 1 rad.
        var turned = ItemEntityRenderer.RootTransform(ageInTicks: 20f, bobPhase: 0f, extents);
        var unitX = System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitX, turned);
        Assert.Equal(MathF.Cos(1f), unitX.X, 5);
        Assert.Equal(-MathF.Sin(1f), unitX.Z, 5);
    }

    [Fact]
    public void TheItemTransformAppliesBeforeTheSpin()
    {
        // The reference order is T_entity · T_bob · R_spin · T_offset · itemTransform (column vectors). With row vectors
        // that reverses, so the item transform is innermost. A dropped flat item must therefore spin about its own
        // centre: the model centre (0.5, 0.5, 0.5) lands on the entity position plus the rotated offset, whatever the
        // spin angle is.
        var extents = ItemModelExtents.FlatItem;
        var centre = new System.Numerics.Vector3(0.5f, 0.5f, 0.5f);
        var entity = new System.Numerics.Vector3(10f, 0f, -4f);

        foreach (var age in new[] { 0f, 10f, 20f, 31f })
        {
            var placement = ItemEntityRenderer.Placement(
                ItemDisplayTransform.None.ToMatrix(), extents, age, bobPhase: 0f,
                offset: System.Numerics.Vector3.Zero, entityPosition: entity);

            var placed = System.Numerics.Vector3.Transform(centre, placement);
            Assert.Equal(10f, placed.X, 4);
            Assert.Equal(-4f, placed.Z, 4);
            // The only vertical movement is bob + the ground clearance.
            Assert.Equal(
                ItemEntityRenderer.BobHeight(age, 0f) + ItemEntityRenderer.GroundOffset(extents),
                placed.Y,
                4);
        }
    }

    [Fact]
    public void TheStackOffsetIsRotatedByTheSpin()
    {
        // Offsets sit inside the spin, so a thin stack's Z column turns with the drop instead of staying world aligned.
        var extents = ItemModelExtents.FlatItem;
        const float quarterTurn = MathF.PI / 2f;
        var placement = ItemEntityRenderer.Placement(
            ItemDisplayTransform.None.ToMatrix(), extents, ageInTicks: 10f, bobPhase: quarterTurn - 0.5f,
            offset: new System.Numerics.Vector3(0f, 0f, 1f), entityPosition: System.Numerics.Vector3.Zero);

        var centre = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0.5f, 0.5f, 0.5f), placement);
        // spin = age/20 + phase = 0.5 + (π/2 − 0.5) = π/2. A quarter turn about +Y maps +Z onto +X under the same
        // convention the reference uses (x' = x·cos + z·sin, z' = −x·sin + z·cos).
        Assert.Equal(1f, centre.X, 4);
        Assert.Equal(0f, centre.Z, 4);
    }

    [Fact]
    public void ThinStackItemsSpreadAlongAZColumnWithHalfAmplitudeJitter()
    {
        var extents = ItemModelExtents.FlatItem;
        const int count = 3;
        // step = (1/16)*1.5 = 0.09375, start = -(step*(3-1)/2) = -0.09375
        var first = ItemEntityRenderer.ThinItemOffset(extents, 0, count, randomX: 0.5f, randomY: 0.5f);
        var second = ItemEntityRenderer.ThinItemOffset(extents, 1, count, randomX: 0.5f, randomY: 0.5f);
        var third = ItemEntityRenderer.ThinItemOffset(extents, 2, count, randomX: 0.5f, randomY: 0.5f);

        Assert.Equal(-0.09375f, first.Z, 6);
        Assert.Equal(0f, second.Z, 6);
        Assert.Equal(0.09375f, third.Z, 6);
        // random 0.5 is the zero point of the jitter, and the jitter amplitude is half the scatter spread.
        Assert.Equal(0f, first.X, 6);
        Assert.Equal(0f, first.Y, 6);
        var extreme = ItemEntityRenderer.ThinItemOffset(extents, 0, count, 0f, 0f);
        Assert.Equal(-0.075f, extreme.X, 6);
        Assert.Equal(-0.075f, extreme.Y, 6);
    }

    [Fact]
    public void ThickStackItemsScatterOnEveryAxis()
    {
        var offset = ItemEntityRenderer.ThickItemOffset(randomX: 1f, randomY: 0f, randomZ: 0.5f);
        Assert.Equal(0.15f, offset.X, 6);
        Assert.Equal(-0.15f, offset.Y, 6);
        Assert.Equal(0f, offset.Z, 6);
    }
}
