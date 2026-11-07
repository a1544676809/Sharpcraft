// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-680: proves the DSL-baked aquatic parts are value-identical to the legacy FromVanilla/FromVanillaChild records,
/// that tropical-fish pattern copies keep their selectors and texture paths, that all variant names/paths/counts and
/// salmon scales are unchanged, and that salmon/dolphin child order and parent links are preserved.
/// </summary>
public sealed class AquaticModelDefinitionTests
{
    [Fact]
    public void CodPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedCodParts(), CodModelDefinition.Definition.Parts);
    }

    [Fact]
    public void SalmonPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedSalmonParts(), SalmonModelDefinition.Definition.Parts);
    }

    [Fact]
    public void DolphinPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedDolphinParts(), DolphinModelDefinition.Definition.Parts);
    }

    [Fact]
    public void PufferfishPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedPufferfishSmallParts(), PufferfishModelDefinition.Definition.Parts);
        AssertPartsMatch(ExpectedPufferfishMediumParts(), PufferfishModelDefinition.Variants["medium"].Parts);
        AssertPartsMatch(ExpectedPufferfishLargeParts(), PufferfishModelDefinition.Variants["large"].Parts);
    }

    [Fact]
    public void TropicalFishPartsMatchLegacyFromVanillaRecords()
    {
        // The base body parts are the first half of each variant's parts (the rest are pattern copies).
        AssertPrefixMatch(ExpectedTropicalSmallParts(), TropicalFishModelDefinition.Definition.Parts);
        AssertPrefixMatch(ExpectedTropicalLargeParts(), TropicalFishModelDefinition.Variants["flopper"].Parts);
    }

    [Fact]
    public void TropicalPatternCopiesKeepSelectors()
    {
        var definition = TropicalFishModelDefinition.Definition;
        Assert.Equal("tropical_fish", definition.Name);
        Assert.Equal("entity/fish/tropical_a", definition.TexturePath);
        Assert.Equal(10, definition.Parts.Count);
        var baseParts = ExpectedTropicalSmallParts();
        for (var i = 0; i < baseParts.Length; i++)
        {
            var basePart = definition.Parts[i];
            var copy = definition.Parts[baseParts.Length + i];
            Assert.Equal("pattern_" + baseParts[i].Name, copy.Name);
            Assert.Equal("entity/fish/tropical_a_pattern_1", copy.TexturePath);
            Assert.NotNull(copy.TintSelector);
            Assert.Null(basePart.TintSelector);

            var neutral = new EntityRenderState(1, TropicalFishModelDefinition.TropicalFishEntityTypeId, 0f, 0f, 0f, 0f, 0f);
            Assert.Equal(Vector3.One, copy.TintSelector!(neutral));
            var tinted = new EntityRenderState(1, TropicalFishModelDefinition.TropicalFishEntityTypeId, 0f, 0f, 0f, 0f, 0f,
                PatternTint: new Vector3(0.2f, 0.4f, 0.6f));
            Assert.Equal(new Vector3(0.2f, 0.4f, 0.6f), copy.TintSelector!(tinted));
        }
    }

    [Fact]
    public void VariantsKeepNamesPathsCountsAndSalmonScales()
    {
        // Cod (no variants).
        Assert.Equal("cod", CodModelDefinition.Definition.Name);
        Assert.Equal(32, CodModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, CodModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/fish/cod", CodModelDefinition.Definition.TexturePath);
        Assert.Equal(7, CodModelDefinition.Definition.Parts.Count);

        // Salmon: 3 synced size variants.
        Assert.Equal(3, SalmonModelDefinition.Variants.Count);
        Assert.Same(SalmonModelDefinition.Definition, SalmonModelDefinition.Variants["medium"]);
        Assert.Equal(1f, SalmonModelDefinition.Definition.Scale);
        Assert.Equal("salmon_small", SalmonModelDefinition.Variants["small"].Name);
        Assert.Equal(0.5f, SalmonModelDefinition.Variants["small"].Scale);
        Assert.Equal("salmon_large", SalmonModelDefinition.Variants["large"].Name);
        Assert.Equal(1.5f, SalmonModelDefinition.Variants["large"].Scale);
        foreach (var key in SalmonModelDefinition.VariantKeys)
        {
            var variant = SalmonModelDefinition.Variants[key];
            Assert.Equal("entity/fish/salmon", variant.TexturePath);
            Assert.Equal(8, variant.Parts.Count);
            Assert.Equal(32, variant.TextureWidth);
            Assert.Equal(32, variant.TextureHeight);
        }

        // Pufferfish: 3 puff states with 6/11/13 parts.
        Assert.Equal(3, PufferfishModelDefinition.Variants.Count);
        Assert.Same(PufferfishModelDefinition.Definition, PufferfishModelDefinition.Variants["small"]);
        Assert.Equal("pufferfish_medium", PufferfishModelDefinition.Variants["medium"].Name);
        Assert.Equal(11, PufferfishModelDefinition.Variants["medium"].Parts.Count);
        Assert.Equal("pufferfish_large", PufferfishModelDefinition.Variants["large"].Name);
        Assert.Equal(13, PufferfishModelDefinition.Variants["large"].Parts.Count);
        foreach (var key in PufferfishModelDefinition.VariantKeys)
        {
            Assert.Equal("entity/fish/pufferfish", PufferfishModelDefinition.Variants[key].TexturePath);
        }

        // Tropical fish: 12 registry variants, small shapes 10 parts, large shapes 12 parts.
        Assert.Equal(TropicalFishModelDefinition.VariantKeys.Count, TropicalFishModelDefinition.Variants.Count);
        Assert.Same(TropicalFishModelDefinition.Definition, TropicalFishModelDefinition.Variants["kob"]);
        for (var index = 0; index < TropicalFishModelDefinition.VariantKeys.Count; index++)
        {
            var key = TropicalFishModelDefinition.VariantKeys[index];
            var variant = TropicalFishModelDefinition.Variants[key];
            var large = index >= 6;
            Assert.Equal(index == 0 ? "tropical_fish" : "tropical_fish_" + key, variant.Name);
            Assert.Equal(large ? "entity/fish/tropical_b" : "entity/fish/tropical_a", variant.TexturePath);
            Assert.Equal(large ? 12 : 10, variant.Parts.Count);
            Assert.Equal(32, variant.TextureWidth);
            Assert.Equal(32, variant.TextureHeight);
        }
    }

    [Fact]
    public void SalmonAndDolphinKeepOrderAndParentLinks()
    {
        AssertNamesAndParents(SalmonModelDefinition.Definition.Parts,
            ["body_front", "body_back", "head", "back_fin", "top_front_fin", "top_back_fin", "right_fin", "left_fin"],
            [null, null, null, "body_back", "body_front", "body_back", null, null]);
        AssertNamesAndParents(DolphinModelDefinition.Definition.Parts,
            ["body", "back_fin", "left_fin", "right_fin", "tail", "tail_fin", "head", "nose"],
            [null, "body", "body", "body", "body", "tail", "body", "head"]);
    }

    private static EntityModelPart[] ExpectedCodParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 22f, 0f, -1f, -2f, 0f, 2f, 4f, 7f, 0, 0),
        LegacyModelPartFactory.FromVanilla("head", 0f, 22f, 0f, -1f, -2f, -3f, 2f, 4f, 3f, 11, 0),
        LegacyModelPartFactory.FromVanilla("nose", 0f, 22f, -3f, -1f, -2f, -1f, 2f, 3f, 1f, 0, 0),
        LegacyModelPartFactory.FromVanilla("right_fin", -1f, 23f, 0f, -2f, 0f, -1f, 2f, 0f, 2f, 22, 1,
            rotationZ: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_fin", 1f, 23f, 0f, 0f, 0f, -1f, 2f, 0f, 2f, 22, 4,
            rotationZ: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("tail_fin", 0f, 22f, 7f, 0f, -2f, 0f, 0f, 4f, 4f, 22, 3),
        LegacyModelPartFactory.FromVanilla("top_fin", 0f, 20f, 0f, 0f, -1f, -1f, 0f, 1f, 6f, 20, -6)
    ];

    private static EntityModelPart[] ExpectedSalmonParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body_front", 0f, 20f, -7.2f, -1.5f, -2.5f, 0f, 3f, 5f, 8f, 0, 0),
        LegacyModelPartFactory.FromVanilla("body_back", 0f, 20f, 0.8f, -1.5f, -2.5f, 0f, 3f, 5f, 8f, 0, 13),
        LegacyModelPartFactory.FromVanilla("head", 0f, 20f, -7.2f, -1f, -2f, -3f, 2f, 4f, 3f, 22, 0),
        LegacyModelPartFactory.FromVanillaChild("back_fin", "body_back", 0f, 0f, 8f, 0f, -2.5f, 0f, 0f, 5f, 6f, 20, 10),
        LegacyModelPartFactory.FromVanillaChild("top_front_fin", "body_front", 0f, -4.5f, 5f, 0f, 0f, 0f, 0f, 2f, 3f, 2, 1),
        LegacyModelPartFactory.FromVanillaChild("top_back_fin", "body_back", 0f, -4.5f, -1f, 0f, 0f, 0f, 0f, 2f, 4f, 0, 2),
        LegacyModelPartFactory.FromVanilla("right_fin", -1.5f, 21.5f, -7.2f, -2f, 0f, 0f, 2f, 0f, 2f, 28, 0,
            rotationZ: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_fin", 1.5f, 21.5f, -7.2f, 0f, 0f, 0f, 2f, 0f, 2f, 0, 0,
            rotationZ: -MathF.PI / 4f)
    ];

    private static EntityModelPart[] ExpectedDolphinParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 22f, -5f, -4f, -7f, 0f, 8f, 7f, 13f, 22, 0),
        LegacyModelPartFactory.FromVanillaChild("back_fin", "body", 0f, 0f, 0f, -0.5f, 0f, 8f, 1f, 4f, 5f, 51, 0,
            rotationX: MathF.PI / 3f),
        LegacyModelPartFactory.FromVanillaChild("left_fin", "body", 2f, -2f, 4f, -0.5f, -4f, 0f, 1f, 4f, 7f, 48, 20,
            mirror: true, rotationX: MathF.PI / 3f, rotationZ: MathF.PI * 2f / 3f),
        LegacyModelPartFactory.FromVanillaChild("right_fin", "body", -2f, -2f, 4f, -0.5f, -4f, 0f, 1f, 4f, 7f, 48, 20,
            rotationX: MathF.PI / 3f, rotationZ: -MathF.PI * 2f / 3f),
        LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -2.5f, 11f, -2f, -2.5f, 0f, 4f, 5f, 11f, 0, 19,
            rotationX: -0.10471976f),
        LegacyModelPartFactory.FromVanillaChild("tail_fin", "tail", 0f, 0f, 9f, -5f, -0.5f, 0f, 10f, 1f, 6f, 19, 20),
        LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -4f, -3f, -4f, -3f, -3f, 8f, 7f, 6f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1f, 2f, -7f, 2f, 2f, 4f, 0, 13)
    ];

    private static EntityModelPart[] ExpectedPufferfishSmallParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 23f, 0f, -1.5f, -2f, -1.5f, 3f, 2f, 3f, 0, 27),
        LegacyModelPartFactory.FromVanilla("right_eye", 0f, 20f, 0f, -1.5f, 0f, -1.5f, 1f, 1f, 1f, 24, 6),
        LegacyModelPartFactory.FromVanilla("left_eye", 0f, 20f, 0f, 0.5f, 0f, -1.5f, 1f, 1f, 1f, 28, 6),
        LegacyModelPartFactory.FromVanilla("back_fin", 0f, 22f, 1.5f, -1.5f, 0f, 0f, 3f, 0f, 3f, -3, 0),
        LegacyModelPartFactory.FromVanilla("right_fin", -1.5f, 22f, -1.5f, -1f, 0f, 0f, 1f, 0f, 2f, 25, 0),
        LegacyModelPartFactory.FromVanilla("left_fin", 1.5f, 22f, -1.5f, 0f, 0f, 0f, 1f, 0f, 2f, 25, 0)
    ];

    private static EntityModelPart[] ExpectedPufferfishMediumParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 22f, 0f, -2.5f, -5f, -2.5f, 5f, 5f, 5f, 12, 22),
        LegacyModelPartFactory.FromVanilla("right_blue_fin", -2.5f, 18f, -1.5f, -2f, 0f, 0f, 2f, 0f, 2f, 24, 0),
        LegacyModelPartFactory.FromVanilla("left_blue_fin", 2.5f, 18f, -1.5f, 0f, 0f, 0f, 2f, 0f, 2f, 24, 3),
        LegacyModelPartFactory.FromVanilla("top_front_fin", 0f, 17f, -2.5f, -2.5f, -1f, 0f, 5f, 1f, 0f, 19, 17,
            rotationX: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("top_back_fin", 0f, 17f, 2.5f, -2.5f, -1f, 0f, 5f, 1f, 0f, 11, 17,
            rotationX: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("right_front_fin", -2.5f, 22f, -2.5f, -1f, -5f, 0f, 1f, 5f, 0f, 5, 17,
            rotationY: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("right_back_fin", -2.5f, 22f, 2.5f, -1f, -5f, 0f, 1f, 5f, 0f, 9, 17,
            rotationY: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_back_fin", 2.5f, 22f, 2.5f, 0f, -5f, 0f, 1f, 5f, 0f, 1, 17,
            rotationY: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_front_fin", 2.5f, 22f, -2.5f, 0f, -5f, 0f, 1f, 5f, 0f, 1, 17,
            rotationY: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("bottom_back_fin", -2.5f, 22f, 2.5f, 0f, 0f, 0f, 5f, 1f, 0f, 18, 20,
            rotationX: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("bottom_front_fin", 0f, 22f, -2.5f, -2.5f, 0f, 0f, 5f, 1f, 1f, 17, 19,
            rotationX: MathF.PI / 4f)
    ];

    private static EntityModelPart[] ExpectedPufferfishLargeParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 22f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
        LegacyModelPartFactory.FromVanilla("right_blue_fin", -4f, 15f, -2f, -2f, 0f, -1f, 2f, 1f, 2f, 24, 0),
        LegacyModelPartFactory.FromVanilla("left_blue_fin", 4f, 15f, -2f, 0f, 0f, -1f, 2f, 1f, 2f, 24, 3),
        LegacyModelPartFactory.FromVanilla("top_front_fin", 0f, 14f, -4f, -4f, -1f, 0f, 8f, 1f, 0f, 15, 17,
            rotationX: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("top_middle_fin", 0f, 14f, 0f, -4f, -1f, 0f, 8f, 1f, 1f, 14, 16),
        LegacyModelPartFactory.FromVanilla("top_back_fin", 0f, 14f, 4f, -4f, -1f, 0f, 8f, 1f, 0f, 23, 18,
            rotationX: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("right_front_fin", -4f, 22f, -4f, -1f, -8f, 0f, 1f, 8f, 0f, 5, 17,
            rotationY: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_front_fin", 4f, 22f, -4f, 0f, -8f, 0f, 1f, 8f, 0f, 1, 17,
            rotationY: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("bottom_front_fin", 0f, 22f, -4f, -4f, 0f, 0f, 8f, 1f, 0f, 15, 20,
            rotationX: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("bottom_middle_fin", 0f, 22f, 0f, -4f, 0f, 0f, 8f, 1f, 0f, 15, 20),
        LegacyModelPartFactory.FromVanilla("bottom_back_fin", 0f, 22f, 4f, -4f, 0f, 0f, 8f, 1f, 0f, 15, 20,
            rotationX: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("right_back_fin", -4f, 22f, 4f, -1f, -8f, 0f, 1f, 8f, 0f, 9, 17,
            rotationY: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_back_fin", 4f, 22f, 4f, 0f, -8f, 0f, 1f, 8f, 0f, 9, 17,
            rotationY: MathF.PI / 4f)
    ];

    private static EntityModelPart[] ExpectedTropicalSmallParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 22f, 0f, -1f, -1.5f, -3f, 2f, 3f, 6f, 0, 0),
        LegacyModelPartFactory.FromVanilla("tail", 0f, 22f, 3f, 0f, -1.5f, 0f, 0f, 3f, 6f, 22, -6),
        LegacyModelPartFactory.FromVanilla("right_fin", -1f, 22.5f, 0f, -2f, -1f, 0f, 2f, 2f, 0f, 2, 16, rotationY: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_fin", 1f, 22.5f, 0f, 0f, -1f, 0f, 2f, 2f, 0f, 2, 12, rotationY: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("top_fin", 0f, 20.5f, -3f, 0f, -3f, 0f, 0f, 3f, 6f, 10, -5)
    ];

    private static EntityModelPart[] ExpectedTropicalLargeParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 19f, 0f, -1f, -3f, -3f, 2f, 6f, 6f, 0, 20),
        LegacyModelPartFactory.FromVanilla("tail", 0f, 19f, 3f, 0f, -3f, 0f, 0f, 6f, 5f, 21, 16),
        LegacyModelPartFactory.FromVanilla("right_fin", -1f, 20f, 0f, -2f, 0f, 0f, 2f, 2f, 0f, 2, 16, rotationY: -MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("left_fin", 1f, 20f, 0f, 0f, 0f, 0f, 2f, 2f, 0f, 2, 12, rotationY: MathF.PI / 4f),
        LegacyModelPartFactory.FromVanilla("top_fin", 0f, 16f, -3f, 0f, -4f, 0f, 0f, 4f, 6f, 20, 11),
        LegacyModelPartFactory.FromVanilla("bottom_fin", 0f, 22f, -3f, 0f, 0f, 0f, 0f, 4f, 6f, 20, 21)
    ];

    private static void AssertNamesAndParents(IReadOnlyList<EntityModelPart> parts, string[] names, string?[] parents)
    {
        Assert.Equal(names.Length, parts.Count);
        Assert.Equal(parents.Length, parts.Count);
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

    /// <summary>Compares the leading base parts of a definition that also contains pattern copies.</summary>
    private static void AssertPrefixMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.True(actual.Count >= expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
