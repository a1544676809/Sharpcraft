// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Dropped-item geometry end to end: an item entity's stack must turn into quads placed above the ground with the
/// reference layout rules applied.
/// </summary>
public sealed class DroppedItemMesherTests
{
    private sealed class FakeAtlas : ISpriteAtlas
    {
        public SpriteRect? Get(string? texturePath) =>
            texturePath is null ? null : new SpriteRect(0, 0, 16, 16, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
    }

    private static EntityRenderState Drop(int id, int itemId, int count = 1, float age = 0f) =>
        new(id, EntityTracker.ItemEntityTypeId, X: 0f, Y: 0f, Z: 0f, Yaw: 0f, Pitch: 0f,
            ItemEntityItemId: itemId, ItemEntityCount: count, AgeInTicks: age);

    [Fact]
    public void ANonItemEntityProducesNoLayer()
    {
        var mesher = new DroppedItemMesher();
        var layer = mesher.Build([Drop(1, itemId: -1)], new FakeAtlas());
        Assert.Null(layer);
    }

    [Fact]
    public void TheGeneratedSpriteBoxProducesTwoQuads()
    {
        // A generated model has no elements, so it becomes the extruded 0..16 x 0..16 x 7.5..8.5 box: two quads.
        var resolution = new ItemModelResolution(
            "minecraft:item/apple",
            new Dictionary<string, string> { ["layer0"] = "minecraft:item/apple" },
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>());
        var quads = new List<ItemModelQuad>();
        DroppedItemMesher.CollectQuads(quads, resolution);

        Assert.Equal(2, quads.Count);
        Assert.All(quads, quad => Assert.Equal("item/apple", quad.Texture));
        Assert.Equal(ModelFaceDirection.South, quads[0].Direction);
        Assert.Equal(ModelFaceDirection.North, quads[1].Direction);
        // The extrusion sits between z = 7.5 and 8.5 model pixels. FaceInfo.SOUTH starts at maxZ, so corner 0 is 8.5/16
        // and the opposite face's corner 0 is 7.5/16.
        Assert.Equal(8.5f / 16f, quads[0].Corner0.Z, 6);
        Assert.Equal(7.5f / 16f, quads[1].Corner0.Z, 6);
    }

    [Fact]
    public void ACuboidModelProducesSixQuads()
    {
        var faces = new Dictionary<ModelFaceDirection, ItemModelFace>();
        foreach (var direction in Enum.GetValues<ModelFaceDirection>())
        {
            faces[direction] = new ItemModelFace("block/stone", new Vector4(0f, 0f, 16f, 16f));
        }

        var resolution = new ItemModelResolution(
            "minecraft:block/stone",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>(),
            Elements: [new ItemModelElement(Vector3.Zero, new Vector3(16f, 16f, 16f), faces)]);

        var quads = new List<ItemModelQuad>();
        DroppedItemMesher.CollectQuads(quads, resolution);
        Assert.Equal(6, quads.Count);
    }

    [Fact]
    public void ThePhaseIsStablePerEntityAndInsideOneTurn()
    {
        var first = DroppedItemMesher.PhaseOf(42);
        Assert.Equal(first, DroppedItemMesher.PhaseOf(42));
        Assert.InRange(first, 0f, MathF.PI * 2f);
        Assert.NotEqual(first, DroppedItemMesher.PhaseOf(43));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 3)]
    [InlineData(32, 3)]
    [InlineData(33, 4)]
    [InlineData(48, 4)]
    [InlineData(49, 5)]
    [InlineData(64, 5)]
    public void RenderedAmountFollowsTheReferenceBuckets(int stackCount, int expected)
    {
        Assert.Equal(expected, DroppedItemMesher.RenderedAmount(stackCount));
    }
}
