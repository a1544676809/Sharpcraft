// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-684: proves the DSL-baked Cat/Wolf/Fox/Panda parts are value-identical to the legacy FromVanilla/FromVanillaChild
/// records (including WithCollar copylayer results), that variant metadata and all selectors/scales are unchanged, and
/// that the Fox tail (child of body, listed last) and Wolf nested head/tail keep their order and parent links.
/// </summary>
public sealed class CatWolfFoxPandaModelDefinitionTests
{
    [Fact]
    public void CatPartsMatchLegacyFromVanillaRecords()
    {
        var expected = EntityModelLayerHelpers.WithCollar(ExpectedCatBaseParts(), "entity/cat/cat_collar");
        AssertPartsMatch(expected, CatModelDefinition.Definition.Parts);
        AssertPartsMatch(expected, CatModelDefinition.Ocelot.Parts);
    }

    [Fact]
    public void WolfPartsMatchLegacyFromVanillaRecords()
    {
        var expected = EntityModelLayerHelpers.WithCollar(ExpectedWolfBaseParts(), "entity/wolf/wolf_collar");
        AssertPartsMatch(expected, WolfModelDefinition.Definition.Parts);
        foreach (var key in WolfModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, WolfModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void FoxPartsMatchLegacyFromVanillaRecords()
    {
        var expected = ExpectedFoxParts();
        AssertPartsMatch(expected, FoxModelDefinition.Definition.Parts);
        AssertPartsMatch(expected, FoxModelDefinition.Variants["snow"].Parts);
    }

    [Fact]
    public void PandaPartsMatchLegacyFromVanillaRecords()
    {
        var expected = ExpectedPandaParts();
        AssertPartsMatch(expected, PandaModelDefinition.Definition.Parts);
        foreach (var key in PandaModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, PandaModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void CollarCopylayerSelectorsWork()
    {
        AssertCollarCopylayers(CatModelDefinition.Definition.Parts, 11, "entity/cat/cat_collar");
        AssertCollarCopylayers(WolfModelDefinition.Definition.Parts, 13, "entity/wolf/wolf_collar");
    }

    [Fact]
    public void VariantMetadataAndSelectorsAreUnchanged()
    {
        // Cat: 11 variants, 0.8 scale, Ocelot.
        Assert.Equal("cat", CatModelDefinition.Definition.Name);
        Assert.Equal(64, CatModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, CatModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/cat/cat_tabby", CatModelDefinition.Definition.TexturePath);
        Assert.Equal(0.8f, CatModelDefinition.Definition.Scale);
        Assert.Equal(22, CatModelDefinition.Definition.Parts.Count);
        Assert.Equal("ocelot", CatModelDefinition.Ocelot.Name);
        Assert.Equal("entity/cat/ocelot", CatModelDefinition.Ocelot.TexturePath);
        Assert.Equal(1f, CatModelDefinition.Ocelot.Scale);
        Assert.Equal(22, CatModelDefinition.Ocelot.Parts.Count);
        Assert.Equal(CatModelDefinition.VariantKeys.Count, CatModelDefinition.Variants.Count);
        Assert.Same(CatModelDefinition.Definition, CatModelDefinition.Variants["tabby"]);
        foreach (var key in CatModelDefinition.VariantKeys)
        {
            if (key == "tabby") continue;
            var variant = CatModelDefinition.Variants[key];
            Assert.Equal("cat_" + key, variant.Name);
            Assert.Equal("entity/cat/cat_" + key, variant.TexturePath);
            Assert.Equal(0.8f, variant.Scale);
            Assert.Equal(22, variant.Parts.Count);
        }

        // Wolf: tame/angry selector, 9 variants.
        Assert.Equal("wolf", WolfModelDefinition.Definition.Name);
        Assert.Equal(64, WolfModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, WolfModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/wolf/wolf", WolfModelDefinition.Definition.TexturePath);
        Assert.Equal(26, WolfModelDefinition.Definition.Parts.Count);
        Assert.NotNull(WolfModelDefinition.Definition.TextureSelector);
        Assert.Equal("entity/wolf/wolf_tame", WolfModelDefinition.Definition.TextureSelector!(WolfState(isTame: true, isAngry: false)));
        Assert.Equal("entity/wolf/wolf_angry", WolfModelDefinition.Definition.TextureSelector!(WolfState(isTame: false, isAngry: true)));
        Assert.Equal("entity/wolf/wolf", WolfModelDefinition.Definition.TextureSelector!(WolfState(isTame: false, isAngry: false)));
        Assert.Equal(new[] { "entity/wolf/wolf_tame", "entity/wolf/wolf_angry" }, WolfModelDefinition.Definition.AdditionalTexturePaths);
        Assert.Equal(WolfModelDefinition.VariantKeys.Count, WolfModelDefinition.Variants.Count);
        Assert.Same(WolfModelDefinition.Definition, WolfModelDefinition.Variants["pale"]);
        var spotted = WolfModelDefinition.Variants["spotted"];
        Assert.Equal("wolf_spotted", spotted.Name);
        Assert.Equal("entity/wolf/wolf_spotted", spotted.TexturePath);
        Assert.Equal(26, spotted.Parts.Count);
        Assert.NotNull(spotted.TextureSelector);
        Assert.Equal("entity/wolf/wolf_spotted_tame", spotted.TextureSelector!(WolfState(isTame: true, isAngry: false)));
        Assert.Equal("entity/wolf/wolf_spotted", spotted.TextureSelector!(WolfState(isTame: false, isAngry: false)));

        // Fox: sleeping selector, red/snow variants.
        Assert.Equal("fox", FoxModelDefinition.Definition.Name);
        Assert.Equal(48, FoxModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, FoxModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/fox/fox", FoxModelDefinition.Definition.TexturePath);
        Assert.Equal(10, FoxModelDefinition.Definition.Parts.Count);
        Assert.NotNull(FoxModelDefinition.Definition.TextureSelector);
        Assert.Equal("entity/fox/fox_sleep", FoxModelDefinition.Definition.TextureSelector!(FoxState(true)));
        Assert.Equal("entity/fox/fox", FoxModelDefinition.Definition.TextureSelector!(FoxState(false)));
        Assert.Equal(new[] { "entity/fox/fox_sleep" }, FoxModelDefinition.Definition.AdditionalTexturePaths);
        Assert.Equal(2, FoxModelDefinition.Variants.Count);
        Assert.Same(FoxModelDefinition.Definition, FoxModelDefinition.Variants["red"]);
        var snow = FoxModelDefinition.Variants["snow"];
        Assert.Equal("fox_snow", snow.Name);
        Assert.Equal("entity/fox/fox_snow", snow.TexturePath);
        Assert.Equal(10, snow.Parts.Count);
        Assert.NotNull(snow.TextureSelector);
        Assert.Equal("entity/fox/fox_snow_sleep", snow.TextureSelector!(FoxState(true)));
        Assert.Equal("entity/fox/fox_snow", snow.TextureSelector!(FoxState(false)));
        Assert.Equal(new[] { "entity/fox/fox_snow_sleep" }, snow.AdditionalTexturePaths);

        // Panda: 7 variants.
        Assert.Equal("panda", PandaModelDefinition.Definition.Name);
        Assert.Equal(64, PandaModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, PandaModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/panda/panda", PandaModelDefinition.Definition.TexturePath);
        Assert.Equal(9, PandaModelDefinition.Definition.Parts.Count);
        Assert.Equal(PandaModelDefinition.VariantKeys.Count, PandaModelDefinition.Variants.Count);
        Assert.Same(PandaModelDefinition.Definition, PandaModelDefinition.Variants["normal"]);
        foreach (var key in PandaModelDefinition.VariantKeys)
        {
            if (key == "normal") continue;
            var variant = PandaModelDefinition.Variants[key];
            Assert.Equal("panda_" + key, variant.Name);
            Assert.Equal("entity/panda/panda_" + key, variant.TexturePath);
            Assert.Equal(9, variant.Parts.Count);
        }
    }

    [Fact]
    public void FoxWolfKeepOrderAndParentLinks()
    {
        AssertNamesAndParentsPrefix(FoxModelDefinition.Definition.Parts,
            ["head", "right_ear", "left_ear", "nose", "body", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg", "tail"],
            [null, "head", "head", "head", null, null, null, null, null, "body"]);
        AssertNamesAndParentsPrefix(WolfModelDefinition.Definition.Parts,
            ["head", "real_head", "right_ear", "left_ear", "snout", "body", "upper_body", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg", "tail", "real_tail"],
            [null, "head", "head", "head", "head", null, null, null, null, null, null, null, "tail"]);
    }

    private static EntityRenderState WolfState(bool isTame, bool isAngry) =>
        new(1, WolfModelDefinition.WolfEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsTame: isTame, IsAngry: isAngry);

    private static EntityRenderState FoxState(bool isSleeping) =>
        new(1, FoxModelDefinition.FoxEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsSleeping: isSleeping);

    private static void AssertCollarCopylayers(IReadOnlyList<EntityModelPart> parts, int baseCount, string collarPath)
    {
        Assert.Equal(baseCount * 2, parts.Count);
        var withCollar = new EntityRenderState(1, 1, 0f, 0f, 0f, 0f, 0f, HasCollar: true);
        var withoutCollar = new EntityRenderState(1, 1, 0f, 0f, 0f, 0f, 0f);
        var tinted = new EntityRenderState(1, 1, 0f, 0f, 0f, 0f, 0f, CollarTint: new Vector3(0.2f, 0.4f, 0.6f));
        for (var i = 0; i < baseCount; i++)
        {
            var basePart = parts[i];
            var copy = parts[baseCount + i];
            Assert.Equal("copylayer_" + basePart.Name, copy.Name);
            Assert.Equal(collarPath, copy.TexturePath);
            Assert.Equal(basePart.Grow + 0.1f, copy.Grow);
            Assert.Equal(basePart.PoseSource ?? basePart.Name, copy.PoseSource);
            Assert.NotNull(copy.VisibleWhen);
            Assert.NotNull(copy.TintSelector);
            Assert.True(copy.VisibleWhen!(withCollar));
            Assert.False(copy.VisibleWhen!(withoutCollar));
            Assert.Equal(Vector3.One, copy.TintSelector!(withCollar));
            Assert.Equal(new Vector3(0.2f, 0.4f, 0.6f), copy.TintSelector!(tinted));
        }
    }

    private static EntityModelPart[] ExpectedCatBaseParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head_main", 0f, 15f, -9f, -2.5f, -2f, -3f, 5f, 4f, 5f, 0, 0),
        LegacyModelPartFactory.FromVanilla("nose", 0f, 15f, -9f, -1.5f, -0.001f, -4f, 3f, 2f, 2f, 0, 24),
        LegacyModelPartFactory.FromVanilla("ear1", 0f, 15f, -9f, -2f, -3f, 0f, 1f, 1f, 2f, 0, 10),
        LegacyModelPartFactory.FromVanilla("ear2", 0f, 15f, -9f, 1f, -3f, 0f, 1f, 1f, 2f, 6, 10),
        LegacyModelPartFactory.FromVanilla("body", 0f, 12f, -10f, -2f, 3f, -8f, 4f, 16f, 6f, 20, 0, rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanilla("tail1", 0f, 15f, 8f, -0.5f, 0f, 0f, 1f, 8f, 1f, 0, 15, rotationX: -0.9f),
        LegacyModelPartFactory.FromVanilla("tail2", 0f, 20f, 14f, -0.5f, 0f, 0f, 1f, 8f, 1f, 4, 15),
        LegacyModelPartFactory.FromVanilla("left_hind_leg", 1.1f, 18f, 5f, -1f, 0f, 1f, 2f, 6f, 2f, 8, 13),
        LegacyModelPartFactory.FromVanilla("right_hind_leg", -1.1f, 18f, 5f, -1f, 0f, 1f, 2f, 6f, 2f, 8, 13),
        LegacyModelPartFactory.FromVanilla("left_front_leg", 1.2f, 14.1f, -5f, -1f, 0f, 0f, 2f, 10f, 2f, 40, 0),
        LegacyModelPartFactory.FromVanilla("right_front_leg", -1.2f, 14.1f, -5f, -1f, 0f, 0f, 2f, 10f, 2f, 40, 0)
    ];

    private static EntityModelPart[] ExpectedWolfBaseParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", -1f, 13.5f, -7f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("real_head", "head", 0f, 0f, 0f, -2f, -3f, -2f, 6f, 6f, 4f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2f, -5f, 0f, 2f, 2f, 1f, 16, 14),
        LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 2f, -5f, 0f, 2f, 2f, 1f, 16, 14),
        LegacyModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -0.5f, -0.001f, -5f, 3f, 3f, 4f, 0, 10),
        LegacyModelPartFactory.FromVanilla("body", 0f, 14f, 2f, -3f, -2f, -3f, 6f, 9f, 6f, 18, 14, rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanilla("upper_body", -1f, 14f, -3f, -3f, -3f, -3f, 8f, 6f, 7f, 21, 0, rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanilla("right_hind_leg", -2.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
        LegacyModelPartFactory.FromVanilla("left_hind_leg", 0.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
        LegacyModelPartFactory.FromVanilla("right_front_leg", -2.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
        LegacyModelPartFactory.FromVanilla("left_front_leg", 0.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
        LegacyModelPartFactory.FromVanilla("tail", -1f, 12f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0,
            rotationX: -MathF.PI / 5f),
        LegacyModelPartFactory.FromVanillaChild("real_tail", "tail", 0f, 0f, 0f, 0f, 0f, -1f, 2f, 8f, 2f, 9, 18)
    ];

    private static EntityModelPart[] ExpectedFoxParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", -1f, 16.5f, -3f, -3f, -2f, -5f, 8f, 6f, 6f, 1, 5),
        LegacyModelPartFactory.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -3f, -4f, -4f, 2f, 2f, 1f, 8, 1),
        LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 3f, -4f, -4f, 2f, 2f, 1f, 15, 1),
        LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1f, 2.01f, -8f, 4f, 2f, 3f, 6, 18),
        LegacyModelPartFactory.FromVanilla("body", 0f, 16f, -6f, -3f, 3.999f, -3.5f, 6f, 11f, 6f, 24, 15, rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanilla("right_hind_leg", -5f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24, grow: 0.001f),
        LegacyModelPartFactory.FromVanilla("left_hind_leg", -1f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24, grow: 0.001f),
        LegacyModelPartFactory.FromVanilla("right_front_leg", -5f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24, grow: 0.001f),
        LegacyModelPartFactory.FromVanilla("left_front_leg", -1f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24, grow: 0.001f),
        LegacyModelPartFactory.FromVanillaChild("tail", "body", -4f, 15f, -1f, 2f, 0f, -1f, 4f, 9f, 5f, 30, 0,
            rotationX: -0.05235988f)
    ];

    private static EntityModelPart[] ExpectedPandaParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", 0f, 11.5f, -17f, -6.5f, -5f, -4f, 13f, 10f, 9f, 0, 6),
        LegacyModelPartFactory.FromVanilla("nose", 0f, 11.5f, -17f, -3.5f, 0f, -6f, 7f, 5f, 2f, 45, 16),
        LegacyModelPartFactory.FromVanilla("left_ear", 0f, 11.5f, -17f, 3.5f, -8f, -1f, 5f, 4f, 1f, 52, 25),
        LegacyModelPartFactory.FromVanilla("right_ear", 0f, 11.5f, -17f, -8.5f, -8f, -1f, 5f, 4f, 1f, 52, 25),
        LegacyModelPartFactory.FromVanilla("body", 0f, 10f, 0f, -9.5f, -13f, -6.5f, 19f, 26f, 13f, 0, 25, rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanilla("right_hind_leg", -5.5f, 15f, 9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0),
        LegacyModelPartFactory.FromVanilla("left_hind_leg", 5.5f, 15f, 9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0),
        LegacyModelPartFactory.FromVanilla("right_front_leg", -5.5f, 15f, -9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0),
        LegacyModelPartFactory.FromVanilla("left_front_leg", 5.5f, 15f, -9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0)
    ];

    private static void AssertNamesAndParentsPrefix(IReadOnlyList<EntityModelPart> parts, string[] names, string?[] parents)
    {
        Assert.True(parts.Count >= names.Length);
        Assert.Equal(names.Length, parents.Length);
        for (var i = 0; i < names.Length; i++)
        {
            Assert.Equal(names[i], parts[i].Name);
            Assert.Equal(parents[i], parts[i].Parent);
        }
    }

    /// <summary>Value-wise array comparison via the shared oracle (record equality is not valid for overlay lists).</summary>
    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
