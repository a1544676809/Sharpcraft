// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-261: trim/glint/crackiness must reuse raw armor geometry, not legacy 24px equipment trees.</summary>
public sealed class RawArmorMaterialLayerTests
{
    [Theory]
    [InlineData("equipment_HumanoidArmor_5", "trim_HumanoidArmor_5", "glint_HumanoidArmor_5", 159, 64, 32)]
    [InlineData("equipment_HumanoidLeggings_3", "trim_HumanoidLeggings_3", "glint_HumanoidLeggings_3", 159, 64, 32)]
    public void PlayerTrimAndGlintMatchRawArmorPointwise(
        string armorName, string trimName, string glintName, int typeId, int width, int height)
    {
        var model = EntityModelResolver.Resolve(typeId);
        var armor = model.RenderLayers!.Single(layer => layer.Name == armorName).Model!;
        var trim = model.RenderLayers!.Single(layer => layer.Name == trimName).Model!;
        var glint = model.RenderLayers!.Single(layer => layer.Name == glintName).Model!;

        Assert.True(armor.RawSourceFrame);
        Assert.True(trim.RawSourceFrame);
        Assert.True(glint.RawSourceFrame);
        Assert.Equal(0f, trim.BaselinePx);
        Assert.Equal(0f, glint.BaselinePx);
        AssertPointwiseEqual(armor, trim, width, height, typeId, trimName);
        AssertPointwiseEqual(armor, glint, width, height, typeId, glintName);
    }

    [Fact]
    public void WolfCrackinessMatchesRawArmorPointwise()
    {
        var model = EntityModelResolver.Resolve(WolfModelDefinition.WolfEntityTypeId);
        var armor = model.RenderLayers!.Single(layer => layer.Name == "equipment_WolfBody_6").Model!;
        var crackiness = model.RenderLayers!.Single(layer => layer.Name == "crackiness").Model!;

        Assert.True(armor.RawSourceFrame);
        Assert.True(crackiness.RawSourceFrame);
        Assert.Equal(0f, crackiness.BaselinePx);
        AssertPointwiseEqual(armor, crackiness, 64, 32, WolfModelDefinition.WolfEntityTypeId, "crackiness");
    }

    [Fact]
    public void LegacyArmorMaterialLayersKeepLegacyBaseline()
    {
        var model = EntityModelResolver.Resolve(HumanoidMobModelDefinition.StrayEntityTypeId);
        var armor = model.RenderLayers!.Single(layer => layer.Name == "equipment_HumanoidArmor_5").Model!;
        var trim = model.RenderLayers!.Single(layer => layer.Name == "trim_HumanoidArmor_5").Model!;
        var glint = model.RenderLayers!.Single(layer => layer.Name == "glint_HumanoidArmor_5").Model!;
        Assert.False(armor.RawSourceFrame);
        Assert.False(trim.RawSourceFrame);
        Assert.False(glint.RawSourceFrame);
        Assert.Equal(24f, trim.BaselinePx);
        Assert.Equal(24f, glint.BaselinePx);
    }

    private static void AssertPointwiseEqual(
        EntityModelDefinition armor,
        EntityModelDefinition material,
        int width,
        int height,
        int typeId,
        string materialName)
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(width, height, new byte[width * height * 4])
        });
        var texture = atlas.Get("entity/equipment/humanoid/iron")!;
        var mesher = new EntityModelMesher();
        var state = new EntityRenderState(1, typeId, 0f, 0f, 0f, 0f, 0f);
        var armorGeometry = mesher.BuildModel(armor, state, texture, width, height, modelScale: 1f);
        var materialGeometry = mesher.BuildModel(material, state, texture, width, height, modelScale: 1f);
        Assert.Equal(armorGeometry.VertexFloats, materialGeometry.VertexFloats);
        var count = armorGeometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        for (var i = 0; i < count; i++)
        {
            var a = VertexFieldReader.Position(armorGeometry.Vertices, i);
            var b = VertexFieldReader.Position(materialGeometry.Vertices, i);
            Assert.True(MathF.Abs(a.X - b.X) <= 1e-5f, $"{materialName} v{i} x");
            Assert.True(MathF.Abs(a.Y - b.Y) <= 1e-5f, $"{materialName} v{i} y");
            Assert.True(MathF.Abs(a.Z - b.Z) <= 1e-5f, $"{materialName} v{i} z");
        }
    }
}
