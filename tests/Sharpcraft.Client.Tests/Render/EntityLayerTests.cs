// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Per-render-type entity layer split (docs/decisions/0021).</summary>
public sealed class EntityLayerTests
{
    private static EntityRenderState Entity(int typeId, int variant = 0) =>
        new(1, typeId, 0f, 0f, 0f, 0f, 0f, VariantId: variant);

    private static (Vector3 Min, Vector3 Max) Bounds(DynamicGeometry geometry)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i < geometry.VertexFloats; i += 14)
        {
            var point = new Vector3(geometry.Vertices[i], geometry.Vertices[i + 1], geometry.Vertices[i + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }

    private static IReadOnlyList<EntityGeometryLayer> Layers(int typeId, int variant = 0)
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        return mesher.BuildLayers([Entity(typeId, variant)], sprite, 64, 64, variants: EntityVariantTable.Builtin);
    }

    [Fact]
    public void CaveSpiderScalesTheSpiderGeometryUniformly()
    {
        var sprite = new SpriteRect(0, 0, 64, 32, 1);
        Assert.Equal(0.7f, EntityModelResolver.Resolve(CaveSpiderModelDefinition.CaveSpiderEntityTypeId).Scale);
        var spider = new EntityModelMesher().Build([Entity(SpiderModelDefinition.SpiderEntityTypeId)], sprite, 64, 32);
        var a = Bounds(spider);
        var cave = new EntityModelMesher().Build([Entity(CaveSpiderModelDefinition.CaveSpiderEntityTypeId)], sprite, 64, 32);
        var b = Bounds(cave);

        var sizeA = a.Max - a.Min;
        var sizeB = b.Max - b.Min;

        Assert.Equal(sizeA.X * 0.7f, sizeB.X, 2);
        Assert.Equal(sizeA.Y * 0.7f, sizeB.Y, 2);
        Assert.Equal(sizeA.Z * 0.7f, sizeB.Z, 2);
    }

    [Fact]
    public void AModelWithoutOverlaysHasOnlyTheCutoutLayer()
    {
        var layers = Layers(CowModelDefinition.CowEntityTypeId);

        var layer = Assert.Single(layers);
        Assert.Equal(EntityGeometryLayerKind.Cutout, layer.Kind);
    }

    [Fact]
    public void PlayerOverlaysBecomeATranslucentLayer()
    {
        var layers = Layers(EntityPlaceholderMesher.PlayerEntityTypeId);

        Assert.Equal(2, layers.Count);
        Assert.Equal(EntityGeometryLayerKind.Cutout, layers[0].Kind);
        Assert.Equal(EntityGeometryLayerKind.Translucent, layers[1].Kind);
    }

    [Fact]
    public void DrownedOuterAndSlimeOuterBecomeTranslucentLayers()
    {
        var drowned = Layers(HumanoidMobModelDefinition.DrownedEntityTypeId);
        var slime = Layers(SlimeModelDefinition.SlimeEntityTypeId);

        Assert.Contains(drowned, layer => layer.Kind == EntityGeometryLayerKind.Translucent);
        Assert.Contains(slime, layer => layer.Kind == EntityGeometryLayerKind.Translucent);
    }

    [Fact]
    public void AnEyesModelGetsAnEmissiveLayerUsingTheEyesTexture()
    {
        var atlas = EntitySkinAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/enderman/enderman"] = new TextureData(64, 32, new byte[64 * 32 * 4]),
            ["entity/enderman/enderman_eyes"] = new TextureData(64, 32, new byte[64 * 32 * 4])
        });

        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 32, 1);
        var layers = mesher.BuildLayers(
            [Entity(HumanoidMobModelDefinition.EndermanEntityTypeId)], sprite, 64, 32, skinAtlas: atlas);

        var eyes = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Emissive);
        Assert.True(eyes.Geometry.IndexCount > 0);
        Assert.Contains(layers, layer => layer.Kind == EntityGeometryLayerKind.Cutout);
    }

    [Fact]
    public void AModelWithoutEyesTextureHasNoEmissiveLayer()
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var layers = mesher.BuildLayers([Entity(CowModelDefinition.CowEntityTypeId)], sprite, 64, 64);

        Assert.DoesNotContain(layers, layer => layer.Kind == EntityGeometryLayerKind.Emissive);
    }

    [Fact]
    public void TheMergedBuildMatchesTheSumOfTheLayers()
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var model = Entity(EntityPlaceholderMesher.PlayerEntityTypeId);

        var layers = mesher.BuildLayers([model], sprite, 64, 64, variants: EntityVariantTable.Builtin);
        var merged = mesher.Build([model], sprite, 64, 64, variants: EntityVariantTable.Builtin);

        Assert.Equal(layers.Sum(layer => layer.Geometry.VertexFloats), merged.VertexFloats);
        Assert.Equal(layers.Sum(layer => layer.Geometry.IndexCount), merged.IndexCount);
    }
}
