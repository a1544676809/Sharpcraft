// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-683: proves the DSL-baked Villager/ZombieVillager/Witch/Illager parts are value-identical to the legacy
/// FromVanilla/FromVanillaChild records (mirrorX:false roots, hat tiers, grow, overlays, arm-pose VisibleWhen), that
/// variant metadata is unchanged, that Illager arm visibility follows the ArmPose state, and that nested order/parent
/// links are preserved.
/// </summary>
public sealed class VillagerIllagerModelDefinitionTests
{
    [Fact]
    public void VillagerPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedVillagerParts(), VillagerModelDefinition.Definition.Parts);
        AssertPartsMatch(ExpectedVillagerParts(), VillagerModelDefinition.WanderingTrader.Parts);
    }

    [Fact]
    public void ZombieVillagerPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedZombieVillagerParts(), ZombieVillagerModelDefinition.Definition.Parts);
    }

    [Fact]
    public void WitchPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedWitchParts(), WitchModelDefinition.Definition.Parts);
    }

    [Fact]
    public void IllagerPartsMatchLegacyFromVanillaRecords()
    {
        AssertPartsMatch(ExpectedIllagerParts(), IllagerModelDefinition.Vindicator.Parts);
        AssertPartsMatch(ExpectedIllagerParts(), IllagerModelDefinition.Evoker.Parts);
        AssertPartsMatch(ExpectedIllagerParts(), IllagerModelDefinition.Pillager.Parts);
        AssertPartsMatch(ExpectedIllagerParts(), IllagerModelDefinition.Illusioner.Parts);
    }

    [Fact]
    public void VariantMetadataIsUnchanged()
    {
        // Villager + WanderingTrader.
        Assert.Equal("villager", VillagerModelDefinition.Definition.Name);
        Assert.Equal(64, VillagerModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, VillagerModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/villager/villager", VillagerModelDefinition.Definition.TexturePath);
        Assert.Equal(0.9375f, VillagerModelDefinition.Definition.Scale);
        Assert.Equal(12, VillagerModelDefinition.Definition.Parts.Count);
        Assert.Equal("wandering_trader", VillagerModelDefinition.WanderingTrader.Name);
        Assert.Equal("entity/wandering_trader/wandering_trader", VillagerModelDefinition.WanderingTrader.TexturePath);
        Assert.Equal(0.9375f, VillagerModelDefinition.WanderingTrader.Scale);
        Assert.Equal(12, VillagerModelDefinition.WanderingTrader.Parts.Count);

        Assert.Equal(VillagerModelDefinition.VariantKeys.Count, VillagerModelDefinition.Variants.Count);
        foreach (var key in VillagerModelDefinition.VariantKeys)
        {
            var variant = VillagerModelDefinition.Variants[key];
            Assert.Equal("villager_" + key, variant.Name);
            Assert.Equal("entity/villager/villager", variant.TexturePath);
            Assert.Equal(0.9375f, variant.Scale);
            Assert.Equal(24, variant.Parts.Count); // 12 base + 12 copylayer
        }

        // ZombieVillager definition + variants.
        Assert.Equal("zombie_villager", ZombieVillagerModelDefinition.Definition.Name);
        Assert.Equal(64, ZombieVillagerModelDefinition.Definition.TextureWidth);
        Assert.Equal(64, ZombieVillagerModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/zombie_villager/zombie_villager", ZombieVillagerModelDefinition.Definition.TexturePath);
        Assert.Equal(8, ZombieVillagerModelDefinition.Definition.Parts.Count);
        Assert.Equal(VillagerModelDefinition.VariantKeys.Count, ZombieVillagerModelDefinition.Variants.Count);
        foreach (var key in VillagerModelDefinition.VariantKeys)
        {
            var variant = ZombieVillagerModelDefinition.Variants[key];
            Assert.Equal("zombie_villager_" + key, variant.Name);
            Assert.Equal("entity/zombie_villager/zombie_villager", variant.TexturePath);
            Assert.Equal(1f, variant.Scale);
            Assert.Equal(16, variant.Parts.Count); // 8 base + 8 copylayer
        }

        // Witch.
        Assert.Equal("witch", WitchModelDefinition.Definition.Name);
        Assert.Equal(64, WitchModelDefinition.Definition.TextureWidth);
        Assert.Equal(128, WitchModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/witch/witch", WitchModelDefinition.Definition.TexturePath);
        Assert.Equal(0.9375f, WitchModelDefinition.Definition.Scale);
        Assert.Equal(15, WitchModelDefinition.Definition.Parts.Count);

        // Illager: 4 shared-geometry definitions.
        foreach (var (definition, name) in new[]
        {
            (IllagerModelDefinition.Vindicator, "vindicator"),
            (IllagerModelDefinition.Evoker, "evoker"),
            (IllagerModelDefinition.Pillager, "pillager"),
            (IllagerModelDefinition.Illusioner, "illusioner")
        })
        {
            Assert.Equal(name, definition.Name);
            Assert.Equal(64, definition.TextureWidth);
            Assert.Equal(64, definition.TextureHeight);
            Assert.Equal("entity/illager/" + name, definition.TexturePath);
            Assert.Equal(11, definition.Parts.Count);
        }
    }

    [Fact]
    public void IllagerArmPoseVisibility()
    {
        var crossed = new EntityRenderState(1, IllagerModelDefinition.VindicatorEntityTypeId, 0f, 0f, 0f, 0f, 0f,
            ArmPose: (int)IllagerArmPose.Crossed);
        var neutral = new EntityRenderState(1, IllagerModelDefinition.VindicatorEntityTypeId, 0f, 0f, 0f, 0f, 0f,
            ArmPose: (int)IllagerArmPose.Neutral);
        foreach (var definition in new[]
        {
            IllagerModelDefinition.Vindicator, IllagerModelDefinition.Evoker,
            IllagerModelDefinition.Pillager, IllagerModelDefinition.Illusioner
        })
        {
            foreach (var name in new[] { "arms_right", "arms_cross", "arms_left" })
            {
                var part = definition.Parts.Single(p => p.Name == name);
                Assert.NotNull(part.VisibleWhen);
                Assert.True(part.VisibleWhen!(crossed));
                Assert.False(part.VisibleWhen!(neutral));
            }

            foreach (var name in new[] { "right_arm", "left_arm" })
            {
                var part = definition.Parts.Single(p => p.Name == name);
                Assert.NotNull(part.VisibleWhen);
                Assert.False(part.VisibleWhen!(crossed));
                Assert.True(part.VisibleWhen!(neutral));
            }

            foreach (var name in new[] { "head", "nose", "body", "body_outer", "right_leg", "left_leg" })
            {
                Assert.Null(definition.Parts.Single(p => p.Name == name).VisibleWhen);
            }
        }
    }

    [Fact]
    public void VillagerWitchKeepOrderAndParentLinks()
    {
        AssertNamesAndParents(VillagerModelDefinition.Definition.Parts,
            ["head", "hat", "hat_rim", "nose", "body", "jacket", "arms", "arms_left", "arms_right", "arms_middle", "right_leg", "left_leg"],
            [null, "head", "hat", "head", null, "body", null, "arms", "arms", "arms", null, null]);
        AssertNamesAndParents(WitchModelDefinition.Definition.Parts,
            ["head", "nose", "mole", "held_potion", "hat", "hat2", "hat3", "hat4", "body", "jacket", "arms", "arms_left", "arms_right", "right_leg", "left_leg"],
            [null, "head", "nose", "nose", "head", "hat", "hat2", "hat3", null, "body", null, "arms", "arms", null, null]);
    }

    private static EntityModelPart[] ExpectedVillagerParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0, mirrorX: false),
        LegacyModelPartFactory.FromVanillaChild("hat", "head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 32, 0, grow: 0.51f),
        LegacyModelPartFactory.FromVanillaChild("hat_rim", "hat", 0f, 0f, 0f, -8f, -8f, -6f, 16f, 16f, 1f, 30, 47,
            rotationX: -MathF.PI / 2f),
        LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0),
        LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20, mirrorX: false),
        LegacyModelPartFactory.FromVanillaChild("jacket", "body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.5f),
        LegacyModelPartFactory.FromVanilla("arms", 0f, 3f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, mirrorX: false, rotationX: 0.75f),
        LegacyModelPartFactory.FromVanillaChild("arms_left", "arms", 0f, 0f, 0f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22),
        LegacyModelPartFactory.FromVanillaChild("arms_right", "arms", 0f, 0f, 0f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22, mirror: true),
        LegacyModelPartFactory.FromVanillaChild("arms_middle", "arms", 0f, 0f, 0f, -4f, 2f, -2f, 8f, 4f, 4f, 40, 38),
        LegacyModelPartFactory.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
        LegacyModelPartFactory.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, mirror: true)
    ];

    private static EntityModelPart[] ExpectedZombieVillagerParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0,
            overlays: [new("hat", 0.5f, 32, 0)]),
        LegacyModelPartFactory.FromVanilla("nose", 0f, 0f, 0f, -1f, -3f, -6f, 2f, 4f, 2f, 24, 0),
        LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20),
        LegacyModelPartFactory.FromVanilla("body_outer", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.05f),
        LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 44, 22, mirror: true),
        LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 44, 22),
        LegacyModelPartFactory.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
        LegacyModelPartFactory.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22)
    ];

    private static EntityModelPart[] ExpectedWitchParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0, mirrorX: false),
        LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0),
        LegacyModelPartFactory.FromVanillaChild("mole", "nose", 0f, -2f, 0f, 0f, 3f, -6.75f, 1f, 1f, 1f, 0, 0, grow: -0.25f),
        LegacyModelPartFactory.FromVanillaChild("held_potion", "nose", 0f, 0f, -8f, -4f, -4f, 0f, 8f, 8f, 0f, 0, 0,
            texturePath: "item/potion") with
        {
            TextureWidth = 16,
            TextureHeight = 16,
            VisibleWhen = static state => state.IsHoldingPotion
        },
        LegacyModelPartFactory.FromVanillaChild("hat", "head", -5f, -10.03125f, -5f, 0f, 0f, 0f, 10f, 2f, 10f, 0, 64),
        LegacyModelPartFactory.FromVanillaChild("hat2", "hat", 1.75f, -4f, 2f, 0f, 0f, 0f, 7f, 4f, 7f, 0, 76,
            rotationX: -0.05235988f, rotationZ: 0.02617994f),
        LegacyModelPartFactory.FromVanillaChild("hat3", "hat2", 1.75f, -4f, 2f, 0f, 0f, 0f, 4f, 4f, 4f, 0, 87,
            rotationX: -0.10471976f, rotationZ: 0.05235988f),
        LegacyModelPartFactory.FromVanillaChild("hat4", "hat3", 1.75f, -2f, 2f, 0f, 0f, 0f, 1f, 2f, 1f, 0, 95,
            grow: 0.25f, rotationX: -MathF.PI / 15f, rotationZ: 0.10471976f),
        LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20, mirrorX: false),
        LegacyModelPartFactory.FromVanillaChild("jacket", "body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.5f),
        LegacyModelPartFactory.FromVanilla("arms", 0f, 3f, -1f, -4f, -2f, -2f, 8f, 4f, 4f, 40, 38, mirrorX: false, rotationX: 0.75f),
        LegacyModelPartFactory.FromVanillaChild("arms_left", "arms", 0f, 0f, 0f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22),
        LegacyModelPartFactory.FromVanillaChild("arms_right", "arms", 0f, 0f, 0f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22, mirror: true),
        LegacyModelPartFactory.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
        LegacyModelPartFactory.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, mirror: true)
    ];

    private static EntityModelPart[] ExpectedIllagerParts() =>
    [
        LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0,
            overlays: [new("hat", 0.45f, 32, 0)]),
        LegacyModelPartFactory.FromVanilla("nose", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0),
        LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20),
        LegacyModelPartFactory.FromVanilla("body_outer", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.5f),
        LegacyModelPartFactory.FromVanilla("arms_right", 0f, 3f, -1f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22,
            rotationX: 0.75f) with { VisibleWhen = static state => state.ArmPose == (int)IllagerArmPose.Crossed },
        LegacyModelPartFactory.FromVanilla("arms_cross", 0f, 3f, -1f, -4f, 2f, -2f, 8f, 4f, 4f, 40, 38,
            rotationX: 0.75f) with { VisibleWhen = static state => state.ArmPose == (int)IllagerArmPose.Crossed },
        LegacyModelPartFactory.FromVanilla("arms_left", 0f, 3f, -1f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22,
            mirror: true, rotationX: 0.75f) with { VisibleWhen = static state => state.ArmPose == (int)IllagerArmPose.Crossed },
        LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 46)
            with { VisibleWhen = static state => state.ArmPose != (int)IllagerArmPose.Crossed },
        LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 46, mirror: true)
            with { VisibleWhen = static state => state.ArmPose != (int)IllagerArmPose.Crossed },
        LegacyModelPartFactory.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
        LegacyModelPartFactory.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, mirror: true)
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
