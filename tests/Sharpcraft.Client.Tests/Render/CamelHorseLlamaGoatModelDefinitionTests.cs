// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-685: proves the DSL-baked Camel/Horse/Llama/Goat parts are value-identical to the legacy
/// FromVanilla/FromVanillaChild records (including WithHorseMarkings copylayers), that equine/llama coat metadata is
/// unchanged, and that the Camel nested tree and the reordered Horse base parts keep their order and parent links.
/// </summary>
public sealed class CamelHorseLlamaGoatModelDefinitionTests
{
    [Fact]
    public void CamelPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedCamelParts(), CamelModelDefinition.Definition.Parts);
    }

    [Fact]
    public void HorsePartsMatchLegacyFromVanillaRecords()
    {
        var expected = EntityModelLayerHelpers.WithHorseMarkings(ExpectedHorseBaseParts(), "entity/horse/horse_markings_", false);
        AssertPartsMatch(expected, HorseModelDefinition.Definition.Parts);
        AssertPartsMatch(expected, HorseModelDefinition.Donkey.Parts);
        AssertPartsMatch(expected, HorseModelDefinition.Mule.Parts);
        AssertPartsMatch(expected, HorseModelDefinition.ZombieHorse.Parts);
        AssertPartsMatch(expected, HorseModelDefinition.SkeletonHorse.Parts);
        foreach (var key in HorseModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, HorseModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void LlamaPartsMatchLegacyFromVanillaRecords()
    {
        var expected = ExpectedLlamaParts();
        AssertPartsMatch(expected, LlamaModelDefinition.Definition.Parts);
        foreach (var key in LlamaModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, LlamaModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void GoatPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedGoatParts(), GoatModelDefinition.Definition.Parts);
    }

    [Fact]
    public void HorseMarkingsCopylayerSelectorsWork()
    {
        var parts = HorseModelDefinition.Definition.Parts;
        Assert.Equal(60, parts.Count);
        var keys = new[] { "white", "whitefield", "whitedots", "blackdots" };
        var ids = new[] { 1, 2, 3, 4 };
        for (var m = 0; m < keys.Length; m++)
        {
            for (var i = 0; i < 12; i++)
            {
                var basePart = parts[i];
                var copy = parts[12 + m * 12 + i];
                Assert.Equal("marking_" + keys[m] + "_" + basePart.Name, copy.Name);
                Assert.Equal("entity/horse/horse_markings_" + keys[m], copy.TexturePath);
                Assert.Equal(basePart.Grow + 0.05f, copy.Grow);
                Assert.Equal(basePart.PoseSource ?? basePart.Name, copy.PoseSource);
                Assert.NotNull(copy.VisibleWhen);
                Assert.True(copy.VisibleWhen!(HorseState(ids[m])));
                Assert.False(copy.VisibleWhen!(HorseState(0)));
            }
        }
    }

    [Fact]
    public void VariantMetadataIsUnchanged()
    {
        // Camel.
        Assert.Equal("camel", CamelModelDefinition.Definition.Name);
        Assert.Equal(128, CamelModelDefinition.Definition.TextureWidth);
        Assert.Equal(128, CamelModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/camel/camel", CamelModelDefinition.Definition.TexturePath);
        Assert.Equal(13, CamelModelDefinition.Definition.Parts.Count);

        // Horse: 5 equine definitions + 7 coat variants.
        Assert.Equal("horse", HorseModelDefinition.Definition.Name);
        Assert.Equal(64, HorseModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, HorseModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/horse/horse_brown", HorseModelDefinition.Definition.TexturePath);
        Assert.Equal(60, HorseModelDefinition.Definition.Parts.Count);
        Assert.Equal("donkey", HorseModelDefinition.Donkey.Name);
        Assert.Equal("entity/horse/donkey", HorseModelDefinition.Donkey.TexturePath);
        Assert.Equal(0.87f, HorseModelDefinition.Donkey.Scale);
        Assert.Equal("mule", HorseModelDefinition.Mule.Name);
        Assert.Equal("entity/horse/mule", HorseModelDefinition.Mule.TexturePath);
        Assert.Equal(0.92f, HorseModelDefinition.Mule.Scale);
        Assert.Equal("zombie_horse", HorseModelDefinition.ZombieHorse.Name);
        Assert.Equal("entity/horse/horse_zombie", HorseModelDefinition.ZombieHorse.TexturePath);
        Assert.Equal("skeleton_horse", HorseModelDefinition.SkeletonHorse.Name);
        Assert.Equal("entity/horse/horse_skeleton", HorseModelDefinition.SkeletonHorse.TexturePath);
        Assert.Equal(HorseModelDefinition.VariantKeys.Count, HorseModelDefinition.Variants.Count);
        Assert.Same(HorseModelDefinition.Definition, HorseModelDefinition.Variants["brown"]);
        var files = new (string Key, string File)[]
        {
            ("white", "horse_white"), ("creamy", "horse_creamy"), ("chestnut", "horse_chestnut"),
            ("black", "horse_black"), ("gray", "horse_gray"), ("dark_brown", "horse_darkbrown")
        };
        foreach (var (key, file) in files)
        {
            var variant = HorseModelDefinition.Variants[key];
            Assert.Equal("horse_" + key, variant.Name);
            Assert.Equal("entity/horse/" + file, variant.TexturePath);
            Assert.Equal(60, variant.Parts.Count);
        }

        // Llama: 4 coats.
        Assert.Equal("llama", LlamaModelDefinition.Definition.Name);
        Assert.Equal(128, LlamaModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, LlamaModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/llama/llama_brown", LlamaModelDefinition.Definition.TexturePath);
        Assert.Equal(11, LlamaModelDefinition.Definition.Parts.Count);
        Assert.Equal(LlamaModelDefinition.VariantKeys.Count, LlamaModelDefinition.Variants.Count);
        Assert.Same(LlamaModelDefinition.Definition, LlamaModelDefinition.Variants["brown"]);
        foreach (var key in LlamaModelDefinition.VariantKeys)
        {
            if (key == "brown") continue;
            var variant = LlamaModelDefinition.Variants[key];
            Assert.Equal("llama_" + key, variant.Name);
            Assert.Equal("entity/llama/llama_" + key, variant.TexturePath);
            Assert.Equal(11, variant.Parts.Count);
        }

        // Goat.
        Assert.Equal("goat", GoatModelDefinition.Definition.Name);
        Assert.Equal(64, GoatModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, GoatModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/goat/goat", GoatModelDefinition.Definition.TexturePath);
        Assert.Equal(12, GoatModelDefinition.Definition.Parts.Count);
    }

    [Fact]
    public void CamelHorseKeepOrderAndParentLinks()
    {
        AssertNamesAndParentsPrefix(CamelModelDefinition.Definition.Parts,
            ["body", "hump", "tail", "head", "head_lower", "head_upper", "head_snout", "left_ear", "right_ear", "left_hind_leg", "right_hind_leg", "left_front_leg", "right_front_leg"],
            [null, "body", "body", "body", "head", "head", "head", "head", "head", null, null, null, null]);
        AssertNamesAndParentsPrefix(HorseModelDefinition.Definition.Parts,
            ["body", "head_parts", "head", "mane", "upper_mouth", "left_ear", "right_ear", "left_hind_leg", "right_hind_leg", "left_front_leg", "right_front_leg", "tail"],
            [null, null, "head_parts", "head_parts", "head_parts", "head", "head", null, null, null, null, "body"]);
    }

    private static EntityRenderState HorseState(int markings) =>
        new(1, HorseModelDefinition.HorseEntityTypeId, 0f, 0f, 0f, 0f, 0f, Markings: markings);

    private static EntityModelPart[] ExpectedCamelParts() =>
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 4f, 9.5f, -7.5f, -12f, -23.5f, 15f, 12f, 27f, 0, 25),
        PlayerModelDefinition.FromVanillaChild("hump", "body", 0f, -12f, -10f, -4.5f, -5f, -5.5f, 9f, 5f, 11f, 74, 0),
        PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -9f, 3.5f, -1.5f, 0f, 0f, 3f, 14f, 0f, 122, 0),
        PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -3f, -19.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("head_lower", "head", 0f, 0f, 0f, -3.5f, -7f, -15f, 7f, 8f, 19f, 60, 24),
        PlayerModelDefinition.FromVanillaChild("head_upper", "head", 0f, 0f, 0f, -3.5f, -21f, -15f, 7f, 14f, 7f, 21, 0),
        PlayerModelDefinition.FromVanillaChild("head_snout", "head", 0f, 0f, 0f, -2.5f, -21f, -21f, 5f, 5f, 6f, 50, 0),
        PlayerModelDefinition.FromVanillaChild("left_ear", "head", 2.5f, -21f, -9.5f, -0.5f, 0.5f, -1f, 3f, 1f, 2f, 45, 0),
        PlayerModelDefinition.FromVanillaChild("right_ear", "head", -2.5f, -21f, -9.5f, -2.5f, 0.5f, -1f, 3f, 1f, 2f, 67, 0),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 4.9f, 1f, 9.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 58, 16),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -4.9f, 1f, 9.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 94, 16),
        PlayerModelDefinition.FromVanilla("left_front_leg", 4.9f, 1f, -10.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 0, 0),
        PlayerModelDefinition.FromVanilla("right_front_leg", -4.9f, 1f, -10.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 0, 26)
    ];

    private static EntityModelPart[] ExpectedHorseBaseParts() =>
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 11f, 5f, -5f, -8f, -17f, 10f, 10f, 22f, 0, 32, grow: 0.05f),
        PlayerModelDefinition.FromVanilla("head_parts", 0f, 4f, -12f, -2.05f, -6f, -2f, 4f, 12f, 7f, 0, 35,
            rotationX: -MathF.PI / 6f),
        PlayerModelDefinition.FromVanillaChild("head", "head_parts", 0f, 0f, 0f, -3f, -11f, -2f, 6f, 5f, 7f, 0, 13,
            grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("mane", "head_parts", 0f, 0f, 0f, -1f, -11f, 5.01f, 2f, 16f, 2f, 56, 36,
            grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("upper_mouth", "head_parts", 0f, 0f, 0f, -2f, -11f, -7f, 4f, 5f, 5f, 0, 25,
            grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 0.55f, -13f, 4f, 2f, 3f, 1f, 19, 16,
            grow: -0.001f),
        PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2.55f, -13f, 4f, 2f, 3f, 1f, 19, 16,
            grow: -0.001f),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 4f, 14f, 7f, -3f, -1.01f, -1f, 4f, 11f, 4f, 48, 21, mirror: true),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -4f, 14f, 7f, -1f, -1.01f, -1f, 4f, 11f, 4f, 48, 21),
        PlayerModelDefinition.FromVanilla("left_front_leg", 4f, 14f, -10f, -3f, -1.01f, -1.9f, 4f, 11f, 4f, 48, 21, mirror: true),
        PlayerModelDefinition.FromVanilla("right_front_leg", -4f, 14f, -10f, -1f, -1.01f, -1.9f, 4f, 11f, 4f, 48, 21),
        PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -5f, 2f, -1.5f, 0f, 0f, 3f, 14f, 4f, 42, 36,
            rotationX: MathF.PI / 6f, grow: 0.05f)
    ];

    private static EntityModelPart[] ExpectedLlamaParts() =>
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 7f, -6f, -2f, -14f, -10f, 4f, 4f, 9f, 0, 0),
        PlayerModelDefinition.FromVanilla("neck", 0f, 7f, -6f, -4f, -16f, -6f, 8f, 18f, 6f, 0, 14),
        PlayerModelDefinition.FromVanilla("left_ear", 0f, 7f, -6f, -4f, -19f, -4f, 3f, 3f, 2f, 17, 0),
        PlayerModelDefinition.FromVanilla("right_ear", 0f, 7f, -6f, 1f, -19f, -4f, 3f, 3f, 2f, 17, 0),
        PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 29, 0, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("right_chest", -8.5f, 3f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 28, rotationY: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("left_chest", 5.5f, 3f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 41, rotationY: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -3.5f, 10f, 6f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 3.5f, 10f, 6f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29),
        PlayerModelDefinition.FromVanilla("right_front_leg", -3.5f, 10f, -5f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29),
        PlayerModelDefinition.FromVanilla("left_front_leg", 3.5f, 10f, -5f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29)
    ];

    private static EntityModelPart[] ExpectedGoatParts() =>
    [
        PlayerModelDefinition.FromVanilla("right_ear", 1f, 14f, 0f, -6f, -11f, -10f, 3f, 2f, 1f, 2, 61),
        PlayerModelDefinition.FromVanilla("left_ear", 1f, 14f, 0f, 2f, -11f, -10f, 3f, 2f, 1f, 2, 61, mirror: true),
        PlayerModelDefinition.FromVanilla("goatee", 1f, 14f, 0f, -0.5f, -3f, -14f, 0f, 7f, 5f, 23, 52),
        PlayerModelDefinition.FromVanilla("nose", 1f, 6f, -8f, -3f, -4f, -8f, 5f, 7f, 10f, 34, 46, rotationX: -0.9599f),
        PlayerModelDefinition.FromVanilla("left_horn", 1f, 14f, 0f, -0.01f, -16f, -10f, 2f, 7f, 2f, 12, 55),
        PlayerModelDefinition.FromVanilla("right_horn", 1f, 14f, 0f, -2.99f, -16f, -10f, 2f, 7f, 2f, 12, 55),
        PlayerModelDefinition.FromVanilla("body_lower", 0f, 24f, 0f, -4f, -17f, -7f, 9f, 11f, 16f, 1, 1),
        PlayerModelDefinition.FromVanilla("body_upper", 0f, 24f, 0f, -5f, -18f, -8f, 11f, 14f, 11f, 0, 28),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 1f, 14f, 4f, 0f, 4f, 0f, 3f, 6f, 3f, 36, 29),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 14f, 4f, 0f, 4f, 0f, 3f, 6f, 3f, 49, 29),
        PlayerModelDefinition.FromVanilla("left_front_leg", 1f, 14f, -6f, 0f, 0f, 0f, 3f, 10f, 3f, 49, 2),
        PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 14f, -6f, 0f, 0f, 0f, 3f, 10f, 3f, 35, 2)
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
