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
        // The reference gives the north face explicitly reversed U so the far face lines up with the near one; the
        // generic auto-UV would return the same 0..16 range and show a mirrored sprite instead.
        Assert.Equal(new Vector2(0f, 0f), quads[0].Uv0);
        Assert.Equal(new Vector2(1f, 0f), quads[1].Uv0);
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

    [Fact]
    public void EnsureCapacityGrowsAnEmptyGeometryBeforeWriting()
    {
        // Regression: a freshly constructed DynamicGeometry has empty arrays, so the first dropped item of a session
        // wrote out of bounds and killed the render thread.
        var geometry = new Sharpcraft.Framework.Rendering.DynamicGeometry();
        Assert.Empty(geometry.Vertices);
        Assert.Empty(geometry.Indices);

        DroppedItemMesher.EnsureCapacity(geometry, vertexFloats: 0, indexCount: 0);
        Assert.True(geometry.Vertices.Length >= 4 * DroppedItemMesher.VertexStride);
        Assert.True(geometry.Indices.Length >= 6);

        // A second call that already fits must not shrink anything.
        var vertexLength = geometry.Vertices.Length;
        DroppedItemMesher.EnsureCapacity(geometry, vertexFloats: 0, indexCount: 0);
        Assert.Equal(vertexLength, geometry.Vertices.Length);
    }

    [Fact]
    public void AGeneratedSpriteGetsSideFacesAroundItsOpaquePixels()
    {
        // Reference behaviour: every opaque pixel whose neighbour in a direction is transparent extrudes one quad, so a
        // fully opaque 16x16 sprite gets 16 quads per side and a flat item stops looking like paper.
        var sideFaces = new List<ItemModelQuad>();
        GeneratedItemGeometry.AppendSideFaces(
            sideFaces, "item/apple", 16, 16, (x, y) =>
                x >= 0 && y >= 0 && x < 16 && y < 16 ? (byte)255 : (byte)0);

        Assert.Equal(64, sideFaces.Count);
        Assert.Equal(16, sideFaces.Count(quad => quad.Direction == ModelFaceDirection.Up));
        Assert.Equal(16, sideFaces.Count(quad => quad.Direction == ModelFaceDirection.Down));
        Assert.Equal(16, sideFaces.Count(quad => quad.Direction == ModelFaceDirection.East));
        Assert.Equal(16, sideFaces.Count(quad => quad.Direction == ModelFaceDirection.West));
        // The extrusion is one pixel deep, running from z 7.5 to 8.5 model pixels, so every side quad spans 1/16 block.
        Assert.All(sideFaces, quad =>
        {
            var zs = new[] { quad.Corner0.Z, quad.Corner1.Z, quad.Corner2.Z, quad.Corner3.Z };
            Assert.Equal(1f / 16f, zs.Max() - zs.Min(), 5);
        });
    }

    [Fact]
    public void ASingleOpaquePixelExtrudesFourSideFaces()
    {
        var sideFaces = new List<ItemModelQuad>();
        GeneratedItemGeometry.AppendSideFaces(
            sideFaces, "item/dot", 16, 16, (x, y) => x == 4 && y == 4 ? (byte)255 : (byte)0);

        Assert.Equal(4, sideFaces.Count);
        Assert.Contains(sideFaces, quad => quad.Direction == ModelFaceDirection.Up);
        Assert.Contains(sideFaces, quad => quad.Direction == ModelFaceDirection.West);

        // The pixel sits at sprite cell (4,4); sprite Y grows downward while model Y grows upward, so its model row is
        // 16 - 4 - 1 = 11 px. Every one of the four quads must live inside that single cell's footprint.
        foreach (var quad in sideFaces)
        {
            for (var corner = 0; corner < 4; corner++)
            {
                var point = quad.Corner(corner);
                Assert.InRange(point.X, 4f / 16f - 1e-4f, 5f / 16f + 1e-4f);
                Assert.InRange(point.Z, 7.5f / 16f - 1e-4f, 8.5f / 16f + 1e-4f);
                if (quad.Direction == ModelFaceDirection.Up)
                {
                    Assert.Equal(12f / 16f, point.Y, 4);
                }
                else if (quad.Direction == ModelFaceDirection.Down)
                {
                    Assert.Equal(11f / 16f, point.Y, 4);
                }
                else
                {
                    Assert.InRange(point.Y, 11f / 16f - 1e-4f, 12f / 16f + 1e-4f);
                }
            }
        }
    }

    [Fact]
    public void ATransparentSpriteExtrudesNothing()
    {
        var sideFaces = new List<ItemModelQuad>();
        GeneratedItemGeometry.AppendSideFaces(sideFaces, "item/empty", 16, 16, (_, _) => 0);
        Assert.Empty(sideFaces);
    }

    [Fact]
    public void ConstantTintDecodesArgbToUnitRgb()
    {
        // oak_leaves declares tints [{ type: minecraft:constant, value: -12012264 }].
        var tint = ItemTintSource.Parse(System.Text.Json.JsonDocument.Parse(
            """{"type":"minecraft:constant","value":-12012264}""").RootElement);

        Assert.NotNull(tint);
        Assert.Equal(ItemTintKind.Constant, tint!.Value.Kind);
        var color = tint.Value.Evaluate(grass: null);
        Assert.Equal((( -12012264 >> 16) & 0xFF) / 255f, color.X, 6);
        Assert.Equal(((-12012264 >> 8) & 0xFF) / 255f, color.Y, 6);
        Assert.Equal((-12012264 & 0xFF) / 255f, color.Z, 6);
    }

    [Fact]
    public void GrassTintSamplesTheColormapWithTheReferenceFormula()
    {
        // Fill a 256x256 map with a per-index colour, then check the reference index maths: x = (1-temp)*255,
        // y = (1-temp*rain)*255, index = y<<8 | x.
        var rgba = new byte[256 * 256 * 4];
        for (var i = 0; i < 256 * 256; i++)
        {
            rgba[(i * 4) + 0] = (byte)(i & 0xFF);
            rgba[(i * 4) + 1] = (byte)((i >> 8) & 0xFF);
            rgba[(i * 4) + 2] = 0x40;
            rgba[(i * 4) + 3] = 0xFF;
        }

        var map = new GrassColorMap(rgba);
        // temp 0.5, downfall 1.0 -> rain 0.5, x = 127, y = 127 -> index 0x7F7F. The probe texture stores the index as
        // low byte in red and high byte in green, so reassemble it from the sampled colour.
        var expectedIndex = (127 << 8) | 127;
        var sampled = map.Get(0.5d, 1d);
        Assert.Equal(expectedIndex, (((sampled >> 8) & 0xFF) << 8) | ((sampled >> 16) & 0xFF));
        Assert.Equal(sampled, map.DefaultGrassColor);

        var grassTint = ItemTintSource.Parse(System.Text.Json.JsonDocument.Parse(
            """{"type":"minecraft:grass","temperature":0.5,"downfall":1.0}""").RootElement);
        Assert.NotNull(grassTint);
        Assert.Equal(ItemTintKind.Grass, grassTint!.Value.Kind);
        var color = grassTint.Value.Evaluate(map);
        Assert.Equal(0x7F / 255f, color.X, 6);
        Assert.Equal(0x7F / 255f, color.Y, 6);
        Assert.Equal(0x40 / 255f, color.Z, 6);
        // Without a colormap the reference default pink is used rather than an invented colour.
        Assert.Equal(GrassColorMap.DefaultColor, unchecked((int)0xFFFF00FF));
    }

    [Fact]
    public void FacesWithoutATintIndexStayWhite()
    {
        var resolution = new ItemModelResolution(
            "minecraft:item/plain",
            new Dictionary<string, string>(),
            new Dictionary<ItemDisplayContext, ItemDisplayTransform>(),
            Tints: [new ItemTintSource(ItemTintKind.Constant, unchecked((int)0xFF00FF00), 0d, 0d)]);

        Assert.Equal(System.Numerics.Vector3.One, resolution.TintFor(-1, grass: null));
        Assert.Equal(System.Numerics.Vector3.One, resolution.TintFor(9, grass: null));
        Assert.Equal(0f, resolution.TintFor(0, grass: null).X, 6);
        Assert.Equal(1f, resolution.TintFor(0, grass: null).Y, 6);
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
