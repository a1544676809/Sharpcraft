// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Drop layout offsets. The reference <c>ItemEntityRenderer.submitMultipleFromCount</c> submits the first drawn item at
/// the entity origin and offsets only the following ones: a three-axis scatter for thick models, and a stacked Z walk
/// with a half-amplitude scatter for thin ones.
/// <para>
/// An offset on the first item is rotated with the drop, so it makes the item sweep a small circle around the entity
/// instead of spinning in place — which is exactly the "bed orbits its long edge" defect, and it appeared at random
/// because the offset comes from the entity id hash.
/// </para>
/// </summary>
public sealed class DroppedItemLayoutTests
{
    private static readonly ItemModelExtents ThickModel = new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.14f, 0.3f));
    private static readonly ItemModelExtents ThinModel = new(new Vector3(0f, 0f, 0.484375f), new Vector3(0.5f, 0.5f, 0.515625f));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheFirstDrawnItemSitsOnTheEntityOrigin(bool scattered)
    {
        var offset = ItemEntityRenderer.ItemOffset(0, scattered, scattered ? ThickModel : ThinModel, count: 1, 0.87f, 0.13f, 0.55f);

        Assert.Equal(Vector3.Zero, offset);
    }

    [Fact]
    public void LaterItemsOfAThickModelAreScatteredOnAllThreeAxes()
    {
        var offset = ItemEntityRenderer.ItemOffset(1, scattered: true, ThickModel, count: 2, 0.9f, 0.1f, 0.55f);

        Assert.NotEqual(0f, offset.X);
        Assert.NotEqual(0f, offset.Y);
        Assert.NotEqual(0f, offset.Z);
        Assert.True(MathF.Abs(offset.X) <= ItemEntityRenderer.ScatterSpread);
        Assert.True(MathF.Abs(offset.Z) <= ItemEntityRenderer.ScatterSpread);
    }

    [Fact]
    public void LaterItemsOfAThinModelOnlyMarchAlongZ()
    {
        var offset = ItemEntityRenderer.ItemOffset(1, scattered: false, ThinModel, count: 2, 0.9f, 0.1f, 0.55f);

        Assert.NotEqual(0f, offset.Z);
        Assert.True(MathF.Abs(offset.X) <= ItemEntityRenderer.ScatterSpread * 0.5f);
        Assert.True(MathF.Abs(offset.Y) <= ItemEntityRenderer.ScatterSpread * 0.5f);
    }

    [Fact]
    public void AThickModelIsRecognisedByItsDepth()
    {
        Assert.True(ItemEntityRenderer.UsesScatteredLayout(ThickModel));
        Assert.False(ItemEntityRenderer.UsesScatteredLayout(ThinModel));
    }
}
