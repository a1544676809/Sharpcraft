// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-674: proves the DSL-baked baby humanoid parts (including nested child parts) are value-identical to the legacy
/// FromVanilla/FromVanillaChild records, that the drowned outer copies and variant copy layers are preserved, and that
/// the baby zombie part order and parent links stay intact.
/// </summary>
public sealed class BabyModelDefinitionsHumanoidTests
{
    [Fact]
    public void BabyZombiePartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedZombieParts(), BabyModelDefinitionsHumanoid.Zombie.Parts);
    }

    [Fact]
    public void BabyZombieVillagerPartsMatchLegacyFromVanillaRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 18.75f, 0f, -2f, -2.75f, -1.5f, 4f, 5f, 3f, 0, 15),
            PlayerModelDefinition.FromVanillaChild("body_overlay", "body", 0f, 0f, 0f, -2f, -2.75f, -1.5f, 4f, 6f, 3f, 16, 22, grow: 0.1f),
            PlayerModelDefinition.FromVanilla("head", 0f, 16f, 0f, -4f, -8f, -3.5f, 8f, 8f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, -4f, 0f, -4f, -4f, -3.5f, 8f, 8f, 7f, 0, 31, grow: 0.3f),
            PlayerModelDefinition.FromVanillaChild("hat_rim", "head", 0f, -4.5f, 0f, -7f, -0.5f, -6f, 14f, 1f, 12f, 0, 46),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -1f, -4f, -1f, -1f, -0.5f, 2f, 2f, 1f, 23, 0),
            PlayerModelDefinition.FromVanilla("right_arm", -3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 24, 15),
            PlayerModelDefinition.FromVanilla("left_arm", 3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 16, 15),
            PlayerModelDefinition.FromVanilla("right_leg", -1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 8, 23),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 0, 23)
        };

        AssertPartsMatch(expected, BabyModelDefinitionsHumanoid.ZombieVillager.Parts);
    }

    [Fact]
    public void BabyVillagerPartsMatchLegacyFromVanillaRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("arms", 0f, 17.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_hand", "arms", -3f, 1.4025f, -0.9599f, -1f, -2.4925f, -1.8401f, 2f, 4f, 2f, 36, 15, rotationX: -1.0472f),
            PlayerModelDefinition.FromVanillaChild("right_hand_2", "right_hand", 0f, 0f, 0f, 5f, -2.4925f, -1.8401f, 2f, 4f, 2f, 16, 15),
            PlayerModelDefinition.FromVanillaChild("middlearm_r1", "arms", 0f, 0.9024f, -1.8175f, -2f, -0.9924f, -0.9825f, 4f, 2f, 2f, 24, 17, rotationX: -1.0472f),
            PlayerModelDefinition.FromVanilla("right_leg", -1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 8, 23),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 0, 23),
            PlayerModelDefinition.FromVanilla("head", 0f, 16f, 0f, -4f, -8f, -3.5f, 8f, 8f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, -4f, 0f, -4f, -4f, -3.5f, 8f, 8f, 7f, 0, 30, grow: 0.3f),
            PlayerModelDefinition.FromVanillaChild("hat_rim", "head", 0f, -4.5f, 0f, -7f, -0.5f, -6f, 14f, 1f, 12f, 0, 45),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -2f, -4f, -1f, 0f, -0.5f, 2f, 2f, 1f, 23, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 18.75f, 0f, -2f, -2.75f, -1.5f, 4f, 5f, 3f, 0, 15),
            PlayerModelDefinition.FromVanilla("bb_main", 0.5f, 24f, 0f, -2.5f, -8f, -1.5f, 4f, 6f, 3f, 16, 21, grow: 0.2f)
        };

        AssertPartsMatch(expected, BabyModelDefinitionsHumanoid.Villager.Parts);
    }

    [Fact]
    public void BabyDrownedPartsKeepTheOuterLayerCopies()
    {
        var expectedZombie = ExpectedZombieParts();
        var expected = new List<EntityModelPart>(expectedZombie);
        foreach (var source in expectedZombie)
        {
            if (source.Name == "hat")
            {
                continue;
            }

            expected.Add(source with
            {
                Name = "outer_" + source.Name,
                TexturePath = "entity/zombie/drowned_outer_layer_baby",
                Kind = EntityGeometryLayerKind.Translucent
            });
        }

        AssertPartsMatch(expected.ToArray(), BabyModelDefinitionsHumanoid.Drowned.Parts);
    }

    [Fact]
    public void BabyVariantsCopyTheVillagerAndZombieVillagerParts()
    {
        foreach (var key in VillagerModelDefinition.VariantKeys)
        {
            var villager = BabyModelDefinitionsHumanoid.Variants[(143, key)];
            var zombieVillager = BabyModelDefinitionsHumanoid.Variants[(157, key)];

            Assert.Equal("baby_villager_" + key, villager.Name);
            Assert.Equal("baby_zombie_villager_" + key, zombieVillager.Name);
            Assert.Equal(BabyModelDefinitionsHumanoid.Villager.Parts.Count * 2, villager.Parts.Count);
            Assert.Equal(BabyModelDefinitionsHumanoid.ZombieVillager.Parts.Count * 2, zombieVillager.Parts.Count);

            AssertVariantCopies(BabyModelDefinitionsHumanoid.Villager.Parts, villager.Parts, "entity/villager/baby/" + key);
            AssertVariantCopies(BabyModelDefinitionsHumanoid.ZombieVillager.Parts, zombieVillager.Parts, "entity/zombie_villager/baby/" + key);
        }
    }

    [Fact]
    public void BabyZombiePartsKeepParentLinksAndOrder()
    {
        var parts = BabyModelDefinitionsHumanoid.Zombie.Parts;
        var expectedNames = new[] { "body", "head", "head_overlay", "hat", "right_arm", "left_arm", "right_leg", "left_leg" };
        Assert.Equal(expectedNames.Length, parts.Count);
        var names = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            names[i] = parts[i].Name;
        }

        Assert.Equal(expectedNames, names);
        Assert.Null(parts[0].Parent);
        Assert.Null(parts[1].Parent);
        Assert.Equal("head", parts[2].Parent);
        Assert.Equal("head", parts[3].Parent);
        Assert.Null(parts[4].Parent);
        Assert.Null(parts[5].Parent);
        Assert.Null(parts[6].Parent);
        Assert.Null(parts[7].Parent);
    }

    private static EntityModelPart[] ExpectedZombieParts() =>
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 17.5f, 0f, -2f, -2.5f, -1f, 4f, 5f, 2f, 16, 16),
        PlayerModelDefinition.FromVanilla("head", 0f, 15.25f, 0f, -3f, -6.25f, -3f, 6f, 6f, 6f, 3, 3),
        PlayerModelDefinition.FromVanillaChild("head_overlay", "head", 0f, 0f, 0f, -3f, -6.15f, -3f, 6f, 6f, 6f, 35, 3, grow: 0.25f),
        PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanilla("right_arm", -3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 36, 16),
        PlayerModelDefinition.FromVanilla("left_arm", 3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 28, 16),
        PlayerModelDefinition.FromVanilla("right_leg", -1f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 8, 16),
        PlayerModelDefinition.FromVanilla("left_leg", 1f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 0, 16)
    ];

    private static void AssertVariantCopies(IReadOnlyList<EntityModelPart> baseParts, IReadOnlyList<EntityModelPart> variantParts, string expectedTexturePath)
    {
        var count = baseParts.Count;
        for (var i = 0; i < count; i++)
        {
            ModelPartAssertions.AssertEqual(baseParts[i], variantParts[i]);
        }

        for (var i = 0; i < count; i++)
        {
            var copy = variantParts[count + i];
            Assert.Equal("copylayer_" + baseParts[i].Name, copy.Name);
            Assert.Equal(expectedTexturePath, copy.TexturePath);
            Assert.Equal(baseParts[i].PoseSource ?? baseParts[i].Name, copy.PoseSource);
            Assert.NotNull(copy.VisibleWhen);
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
