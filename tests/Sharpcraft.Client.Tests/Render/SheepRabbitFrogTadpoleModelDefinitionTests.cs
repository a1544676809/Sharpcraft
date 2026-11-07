// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-677: proves the DSL-baked sheep / rabbit / frog / tadpole parts are value-identical to the legacy
/// FromVanilla/FromVanillaChild records (rotations, grow, wool texture paths, deep nested chains and zero-size pivot
/// groups), that the rabbit/frog child order and parent links are preserved, and that the variant dictionaries keep
/// their names, texture paths and counts.
/// </summary>
public sealed class SheepRabbitFrogTadpoleModelDefinitionTests
{
    [Fact]
    public void SheepPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedSheepParts(), SheepModelDefinition.Definition.Parts);
    }

    [Fact]
    public void RabbitPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedRabbitParts(), RabbitModelDefinition.Definition.Parts);
    }

    [Fact]
    public void FrogPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedFrogParts(), FrogModelDefinition.Definition.Parts);
    }

    [Fact]
    public void TadpolePartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedTadpoleParts(), TadpoleModelDefinition.Definition.Parts);
    }

    [Fact]
    public void RabbitKeepsNestedChildOrderAndParentLinks()
    {
        AssertNamesAndParents(RabbitModelDefinition.Definition.Parts,
            ["body", "tail", "head", "left_ear", "right_ear", "frontlegs", "right_front_leg", "left_front_leg", "backlegs", "right_hind_leg", "right_haunch", "left_hind_leg", "left_haunch"],
            [null, "body", "body", "head", "head", "body", "frontlegs", "frontlegs", null, "backlegs", "right_hind_leg", "backlegs", "left_hind_leg"]);
    }

    [Fact]
    public void FrogKeepsNestedChildOrderAndParentLinks()
    {
        AssertNamesAndParents(FrogModelDefinition.Definition.Parts,
            ["root", "body", "body_layer", "head", "head_layer", "eyes", "right_eye", "left_eye", "croaking_body", "tongue", "left_arm", "left_hand", "right_arm", "right_hand", "left_leg", "left_foot", "right_leg", "right_foot"],
            [null, "root", "body", "body", "head", "head", "eyes", "eyes", "body", "body", "body", "left_arm", "body", "right_arm", "root", "left_leg", "root", "right_leg"]);
    }

    [Fact]
    public void VariantsKeepNamesPathsAndCounts()
    {
        // Sheep / Tadpole (no variant dictionaries).
        Assert.Equal("sheep", SheepModelDefinition.Definition.Name);
        Assert.Equal(64, SheepModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, SheepModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/sheep/sheep", SheepModelDefinition.Definition.TexturePath);
        Assert.Equal(12, SheepModelDefinition.Definition.Parts.Count);
        Assert.Equal("tadpole", TadpoleModelDefinition.Definition.Name);
        Assert.Equal(16, TadpoleModelDefinition.Definition.TextureWidth);
        Assert.Equal(16, TadpoleModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/tadpole/tadpole", TadpoleModelDefinition.Definition.TexturePath);
        Assert.Equal(2, TadpoleModelDefinition.Definition.Parts.Count);

        // Rabbit: 7 registry variants, all sharing the 13-part geometry.
        var rabbitFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brown"] = "rabbit_brown",
            ["white"] = "rabbit_white",
            ["black"] = "rabbit_black",
            ["white_splotched"] = "rabbit_white_splotched",
            ["gold"] = "rabbit_gold",
            ["salt"] = "rabbit_salt",
            ["evil"] = "rabbit_caerbannog"
        };
        Assert.Equal(RabbitModelDefinition.VariantKeys.Count, RabbitModelDefinition.Variants.Count);
        Assert.Same(RabbitModelDefinition.Definition, RabbitModelDefinition.Variants["brown"]);
        foreach (var key in RabbitModelDefinition.VariantKeys)
        {
            var variant = RabbitModelDefinition.Variants[key];
            Assert.Equal(key == "brown" ? "rabbit" : "rabbit_" + key, variant.Name);
            Assert.Equal("entity/rabbit/" + rabbitFiles[key], variant.TexturePath);
            Assert.Equal(13, variant.Parts.Count);
            Assert.Equal(64, variant.TextureWidth);
            Assert.Equal(64, variant.TextureHeight);
        }

        // Frog: 3 registry variants, all sharing the 18-part geometry.
        Assert.Equal(3, FrogModelDefinition.Variants.Count);
        Assert.Same(FrogModelDefinition.Definition, FrogModelDefinition.Variants["temperate"]);
        foreach (var key in FrogModelDefinition.VariantKeys)
        {
            var variant = FrogModelDefinition.Variants[key];
            Assert.Equal(key == "temperate" ? "frog" : "frog_" + key, variant.Name);
            Assert.Equal("entity/frog/frog_" + key, variant.TexturePath);
            Assert.Equal(18, variant.Parts.Count);
            Assert.Equal(48, variant.TextureWidth);
            Assert.Equal(48, variant.TextureHeight);
        }
    }

    private static EntityModelPart[] ExpectedSheepParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", 0f, 6f, -8f, -3f, -4f, -6f, 6f, 6f, 8f, 0, 0),
        LegacyModelPartFactory.FromVanilla("body", 0f, 5f, 2f, -4f, -10f, -7f, 8f, 16f, 6f, 28, 8, rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanilla("right_hind_leg", -3f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        LegacyModelPartFactory.FromVanilla("left_hind_leg", 3f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
        LegacyModelPartFactory.FromVanilla("right_front_leg", -3f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        LegacyModelPartFactory.FromVanilla("left_front_leg", 3f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
        LegacyModelPartFactory.FromVanilla("wool_head", 0f, 6f, -8f, -3f, -4f, -4f, 6f, 6f, 6f, 0, 0,
            grow: 0.6f, texturePath: "entity/sheep/sheep_wool"),
        LegacyModelPartFactory.FromVanilla("wool_body", 0f, 5f, 2f, -4f, -10f, -7f, 8f, 16f, 6f, 28, 8,
            rotationX: -MathF.PI / 2f, grow: 1.75f, texturePath: "entity/sheep/sheep_wool"),
        LegacyModelPartFactory.FromVanilla("wool_right_hind_leg", -3f, 12f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
            grow: 0.5f, texturePath: "entity/sheep/sheep_wool"),
        LegacyModelPartFactory.FromVanilla("wool_left_hind_leg", 3f, 12f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
            mirror: true, grow: 0.5f, texturePath: "entity/sheep/sheep_wool"),
        LegacyModelPartFactory.FromVanilla("wool_right_front_leg", -3f, 12f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
            grow: 0.5f, texturePath: "entity/sheep/sheep_wool"),
        LegacyModelPartFactory.FromVanilla("wool_left_front_leg", 3f, 12f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
            mirror: true, grow: 0.5f, texturePath: "entity/sheep/sheep_wool")
    ];

    private static EntityModelPart[] ExpectedRabbitParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 23f, 4f, -4f, -6f, -9f, 8f, 6f, 10f, 0, 0, rotationX: 0.3927f),
        LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -4.9916f, 0.0125f, -2f, -3.0084f, -1.0125f, 4f, 4f, 4f, 20, 16),
        LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -5.2929f, -8.1213f, -2.5f, -3f, -4f, 5f, 5f, 5f, 0, 16,
            rotationX: 0.3927f),
        LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 1.5f, -3.7071f, -0.8787f, -1f, -4.2929f, -0.1213f, 2f, 5f, 1f, 32, 0),
        LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -1.5f, -3.7071f, -0.8787f, -1f, -4.2929f, -0.1213f, 2f, 5f, 1f, 26, 0),
        LegacyModelPartFactory.FromVanillaChild("frontlegs", "body", 0f, -1.5349f, -6.3108f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("right_front_leg", "frontlegs", -2f, 1.9239f, 0.3827f, -0.9f, -1f, -0.9f, 2f, 4f, 2f, 36, 18,
            rotationX: 0.3927f),
        LegacyModelPartFactory.FromVanillaChild("left_front_leg", "frontlegs", 2f, 1.9239f, 0.4827f, -1f, -1f, -1f, 2f, 4f, 2f, 44, 18,
            rotationX: 0.3927f),
        LegacyModelPartFactory.FromVanilla("backlegs", 0f, 23f, 4f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("right_hind_leg", "backlegs", -3f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("right_haunch", "right_hind_leg", 0f, -0.5f, 0f, -1f, 0f, -5f, 2f, 1f, 6f, 20, 24,
            rotationY: 0.3927f),
        LegacyModelPartFactory.FromVanillaChild("left_hind_leg", "backlegs", 3f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("left_haunch", "left_hind_leg", 0f, -0.5f, 0f, -1f, 0f, -5f, 2f, 1f, 6f, 36, 24,
            rotationY: -0.3927f)
    ];

    private static EntityModelPart[] ExpectedFrogParts() =>
    [
        LegacyModelPartFactory.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("body", "root", 0f, -2f, 4f, -3.5f, -2f, -8f, 7f, 3f, 9f, 3, 1),
        LegacyModelPartFactory.FromVanillaChild("body_layer", "body", 0f, 0f, 0f, -3.5f, -1f, -8f, 7f, 0f, 9f, 23, 22),
        LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -2f, -1f, -3.5f, -2f, -7f, 7f, 3f, 9f, 0, 13),
        LegacyModelPartFactory.FromVanillaChild("head_layer", "head", 0f, 0f, 0f, -3.5f, -1f, -7f, 7f, 0f, 9f, 23, 13),
        LegacyModelPartFactory.FromVanillaChild("eyes", "head", -0.5f, 0f, 2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("right_eye", "eyes", -1.5f, -3f, -6.5f, -1.5f, -1f, -1.5f, 3f, 2f, 3f, 0, 0),
        LegacyModelPartFactory.FromVanillaChild("left_eye", "eyes", 2.5f, -3f, -6.5f, -1.5f, -1f, -1.5f, 3f, 2f, 3f, 0, 5),
        LegacyModelPartFactory.FromVanillaChild("croaking_body", "body", 0f, -1f, -5f, -3.5f, -0.1f, -2.9f, 7f, 2f, 3f, 26, 5,
            grow: -0.1f),
        LegacyModelPartFactory.FromVanillaChild("tongue", "body", 0f, -1.01f, 1f, -2f, 0f, -7.1f, 4f, 0f, 7f, 17, 13),
        LegacyModelPartFactory.FromVanillaChild("left_arm", "body", 4f, -1f, -6.5f, -1f, 0f, -1f, 2f, 3f, 3f, 0, 32),
        LegacyModelPartFactory.FromVanillaChild("left_hand", "left_arm", 0f, 3f, -1f, -4f, 0.01f, -4f, 8f, 0f, 8f, 18, 40),
        LegacyModelPartFactory.FromVanillaChild("right_arm", "body", -4f, -1f, -6.5f, -1f, 0f, -1f, 2f, 3f, 3f, 0, 38),
        LegacyModelPartFactory.FromVanillaChild("right_hand", "right_arm", 0f, 3f, 0f, -4f, 0.01f, -5f, 8f, 0f, 8f, 2, 40),
        LegacyModelPartFactory.FromVanillaChild("left_leg", "root", 3.5f, -3f, 4f, -1f, 0f, -2f, 3f, 3f, 4f, 14, 25),
        LegacyModelPartFactory.FromVanillaChild("left_foot", "left_leg", 2f, 3f, 0f, -4f, 0.01f, -4f, 8f, 0f, 8f, 2, 32),
        LegacyModelPartFactory.FromVanillaChild("right_leg", "root", -3.5f, -3f, 4f, -2f, 0f, -2f, 3f, 3f, 4f, 0, 25),
        LegacyModelPartFactory.FromVanillaChild("right_foot", "right_leg", -2f, 3f, 0f, -4f, 0.01f, -4f, 8f, 0f, 8f, 18, 32)
    ];

    private static EntityModelPart[] ExpectedTadpoleParts() =>
    [
        LegacyModelPartFactory.FromVanilla("body", 0f, 22f, -3f, -1.5f, -1f, 0f, 3f, 2f, 3f, 0, 0),
        LegacyModelPartFactory.FromVanilla("tail", 0f, 22f, 0f, 0f, -1f, 0f, 0f, 2f, 7f, 0, 0)
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
}
