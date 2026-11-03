// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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

    private static IReadOnlyList<EntityGeometryLayer> Layers(int typeId, int variant = 0)
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        return mesher.BuildLayers([Entity(typeId, variant)], sprite, 64, 64, variants: EntityVariantTable.Builtin);
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
