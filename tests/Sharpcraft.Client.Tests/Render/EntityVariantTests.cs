// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Variant resolution (docs/analysis/render/entity-variants.md). Registry-backed values are positional, so the tests
/// pin both the vanilla bootstrap order and the "server sent a different order" case.
/// </summary>
public sealed class EntityVariantTests
{
    private static DynamicRegistries Registry(string path, params string[] entryNames)
    {
        var registries = new DynamicRegistries();
        var entries = new List<DynamicRegistries.Entry>(entryNames.Length);
        foreach (var name in entryNames)
        {
            entries.Add(new DynamicRegistries.Entry(new Identifier("minecraft", name), null));
        }

        registries.Apply(new Identifier("minecraft", path), entries);
        return registries;
    }

    [Fact]
    public void ThePigBootstrapOrderMapsValuesToModelTypes()
    {
        var table = EntityVariantTable.Builtin;

        Assert.Equal("pig", table.Resolve(PigModelDefinition.PigEntityTypeId, 0).Name);
        Assert.Equal("pig_warm", table.Resolve(PigModelDefinition.PigEntityTypeId, 1).Name);
        Assert.Equal("pig_cold", table.Resolve(PigModelDefinition.PigEntityTypeId, 2).Name);
    }

    [Fact]
    public void TheColdPigCarriesTheOuterBodyLayerThatTheWarmPigDoesNot()
    {
        var warm = EntityModelResolver.TryResolveVariant(PigModelDefinition.PigEntityTypeId, "warm")!;
        var cold = EntityModelResolver.TryResolveVariant(PigModelDefinition.PigEntityTypeId, "cold")!;

        Assert.All(warm.Parts, part => Assert.Null(part.OverlayLayers));
        var coldBody = Assert.Single(cold.Parts, part => part.Name == "body");
        var layer = Assert.Single(coldBody.OverlayLayers!);
        Assert.Equal(0.5f, layer.Grow);
        Assert.Equal(28, layer.TextureX);
        Assert.Equal(32, layer.TextureY);
    }

    [Fact]
    public void TheWarmCowAddsFourHeadBoxesAndTheColdCowAddsRotatedHorns()
    {
        var normal = EntityModelResolver.TryResolveVariant(CowModelDefinition.CowEntityTypeId, "temperate")!;
        var warm = EntityModelResolver.TryResolveVariant(CowModelDefinition.CowEntityTypeId, "warm")!;
        var cold = EntityModelResolver.TryResolveVariant(CowModelDefinition.CowEntityTypeId, "cold")!;

        Assert.Equal(10, normal.Parts.Count);
        Assert.Equal(12, warm.Parts.Count);
        Assert.Equal(2, warm.Parts.Count(part => part.Mirror && part.Name.Contains("ear", StringComparison.Ordinal)));

        Assert.All(cold.Parts, part => Assert.True(part.OverlayLayers is null || part.Name == "body"));
        var coldBody = Assert.Single(cold.Parts, part => part.Name == "body");
        Assert.Equal(0.5f, Assert.Single(coldBody.OverlayLayers!).Grow);
        Assert.Contains(cold.Parts, part => part.Name == "right_horn" && part.RotationX != 0f);
        Assert.Contains(cold.Parts, part => part.Name == "left_horn" && part.RotationX != 0f);
    }

    [Fact]
    public void TheColdChickenAddsAHeadLayerAndABodyFin()
    {
        var cold = EntityModelResolver.TryResolveVariant(ChickenModelDefinition.ChickenEntityTypeId, "cold")!;

        Assert.Equal(10, cold.Parts.Count);
        Assert.Contains(cold.Parts, part => part.Name == "cold_head_layer");
        Assert.Contains(cold.Parts, part => part.Name == "cold_body_fin" && part.Width == 0f);
    }

    [Fact]
    public void EveryCatVariantHasItsOwnTexture()
    {
        var textures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in CatModelDefinition.VariantKeys)
        {
            var definition = EntityModelResolver.ResolveVariantStrict(CatModelDefinition.CatEntityTypeId, key);
            Assert.True(textures.Add(definition.TexturePath!), $"duplicate texture for {key}");
            Assert.Equal(0.8f, definition.Scale);
        }

        Assert.Equal(11, textures.Count);
    }

    [Fact]
    public void TheHorseCoatsUseTheDarkbrownFileNameAndDefaultToWhite()
    {
        var table = EntityVariantTable.Builtin;

        Assert.Equal("horse_white", table.Resolve(HorseModelDefinition.HorseEntityTypeId, 0).Name);
        var darkBrown = table.Resolve(HorseModelDefinition.HorseEntityTypeId, 6);
        Assert.Equal("entity/horse/horse_darkbrown", darkBrown.TexturePath);

        // Markings live in the high byte and must not shift the coat.
        Assert.Equal("horse_dark_brown", table.Resolve(HorseModelDefinition.HorseEntityTypeId, 6 | (2 << 8)).Name);
    }

    [Fact]
    public void TheLlamaCoatNamesMatchTheEnumOrder()
    {
        var table = EntityVariantTable.Builtin;

        Assert.Equal("llama_creamy", table.Resolve(LlamaModelDefinition.LlamaEntityTypeId, 0).Name);
        Assert.Equal("llama_gray", table.Resolve(LlamaModelDefinition.LlamaEntityTypeId, 3).Name);
    }

    [Fact]
    public void AServerSentRegistryOrderWinsOverTheBootstrapOrder()
    {
        // A datapack may send cold first. Vanilla indexes by position, so value 0 must then be cold.
        var registries = Registry("pig_variant", "cold", "warm", "temperate");
        var table = EntityVariantTable.FromRegistries(registries);

        Assert.Equal("pig_cold", table.Resolve(PigModelDefinition.PigEntityTypeId, 0).Name);
        Assert.Equal("pig", table.Resolve(PigModelDefinition.PigEntityTypeId, 2).Name);
    }

    [Fact]
    public void AnUnknownVariantFallsBackToTheDefaultAndIsRecorded()
    {
        var table = EntityVariantTable.Builtin;

        var definition = table.Resolve(PigModelDefinition.PigEntityTypeId, 99);

        Assert.Equal("pig", definition.Name);
        Assert.Contains("101:99", table.UnknownVariants);
    }

    [Fact]
    public void TheStrictPathThrowsForAnUnknownVariant()
    {
        var table = EntityVariantTable.Builtin;

        Assert.Throws<KeyNotFoundException>(() => table.ResolveStrict(PigModelDefinition.PigEntityTypeId, 99));
        Assert.Throws<KeyNotFoundException>(() => EntityModelResolver.ResolveVariantStrict(PigModelDefinition.PigEntityTypeId, "nope"));
    }

    [Fact]
    public void TheColdCowHornsSitAtTheVanillaPositionAfterTheRotationSignConversion()
    {
        // ColdCowModel adds two head children: right_horn at head-local (-4.5,-2.5,-3.5) with xRot = +90 degrees,
        // left_horn at (5.5,-2.5,-5.0). The client bakes the reference's y-down/X-mirrored space into the part, which
        // negates X and Y, so a reference xRot becomes -xRot here. Recomputing the rotated boxes by hand in that space
        // gives these room-space extents (model pixels, feet at the origin, 1 block = 16 px scaled by ModelScale).
        const float px = (1f / 16f) * EntityPlaceholderMesher.ModelScale;
        AssertColdHorn("right_horn", minX: 4f, maxX: 6f, px);
        AssertColdHorn("left_horn", minX: -6f, maxX: -4f, px);
    }

    private static void AssertColdHorn(string part, float minX, float maxX, float px)
    {
        var mesher = new EntityModelMesher();
        var entity = new EntityRenderState(1, CowModelDefinition.CowEntityTypeId, 0f, 0f, 0f, 0f, 0f, VariantId: 2);
        var sprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, 64, 64, 1);
        var geometry = mesher.Build(
            [entity], sprite, 64, 64, onlyPart: part, variants: EntityVariantTable.Builtin);

        var min = new System.Numerics.Vector3(float.MaxValue);
        var max = new System.Numerics.Vector3(float.MinValue);
        for (var offset = 0; offset < geometry.VertexFloats; offset += 14)
        {
            var point = new System.Numerics.Vector3(
                geometry.Vertices[offset], geometry.Vertices[offset + 1], geometry.Vertices[offset + 2]);
            min = System.Numerics.Vector3.Min(min, point);
            max = System.Numerics.Vector3.Max(max, point);
        }

        Assert.Equal(minX * px, min.X, 4);
        Assert.Equal(maxX * px, max.X, 4);
        Assert.Equal(22f * px, min.Y, 4);
        Assert.Equal(24f * px, max.Y, 4);
        Assert.Equal(-16f * px, min.Z, 4);
        Assert.Equal(-10f * px, max.Z, 4);
    }

    [Fact]
    public void EveryVariantDefinitionIsExposedForTheSkinAtlas()
    {
        var textures = EntityModelResolver.Definitions
            .Select(definition => definition.TexturePath)
            .Where(path => path is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("entity/pig/pig_cold", textures);
        Assert.Contains("entity/cow/cow_warm", textures);
        Assert.Contains("entity/chicken/chicken_cold", textures);
        Assert.Contains("entity/cat/cat_all_black", textures);
        Assert.Contains("entity/horse/horse_darkbrown", textures);
        Assert.Contains("entity/llama/llama_gray", textures);
    }
}
