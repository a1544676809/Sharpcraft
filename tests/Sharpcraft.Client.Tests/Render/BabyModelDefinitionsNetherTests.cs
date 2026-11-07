// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-694: proves the DSL-baked baby piglin/hoglin parts are value-identical to the legacy records.</summary>
public sealed class BabyModelDefinitionsNetherTests
{
    [Fact]
    public void BabyPiglinPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 18f, -0.5f, -3f, -3f, -1f, 6f, 5f, 3f, 0, 13),
            PlayerModelDefinition.FromVanilla("head", 0f, 15f, 0f, -4.5f, -6f, -3.5f, 9f, 6f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_snout", "head", 0f, 0f, 0f, -1.5f, -3f, -4.5f, 3f, 3f, 1f, 21, 30),
            PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 4.2f, -4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear_r1", "left_ear", 1f, 1.75f, 0f, -0.5f, -3f, -2f, 1f, 6f, 4f, 0, 21, rotationZ: -0.6109f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -4.2f, -4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear_r1", "right_ear", -1f, 1.75f, 0f, -0.5f, -3f, -2f, 1f, 6f, 4f, 18, 13, rotationZ: 0.6109f),
            PlayerModelDefinition.FromVanilla("left_arm", 4f, 15f, 0f, -1f, 0f, -1.5f, 2f, 5f, 3f, 28, 13),
            PlayerModelDefinition.FromVanilla("right_arm", -4f, 15f, 0f, -1f, 0f, -1.5f, 2f, 5f, 3f, 10, 30),
            PlayerModelDefinition.FromVanilla("right_leg", -1.5f, 20f, 0f, -1.5f, 0f, -1.5f, 3f, 4f, 3f, 22, 23),
            PlayerModelDefinition.FromVanilla("left_leg", 1.5f, 20f, 0f, -1.5f, 0f, -1.5f, 3f, 4f, 3f, 10, 23)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsNether.Piglin.Parts);
        AssertPartsMatch(expected, BabyModelDefinitionsNether.ZombifiedPiglin.Parts);
    }

    [Fact]
    public void BabyHoglinPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 13f, -7f, -5f, -2.2605f, -10.547f, 10f, 4f, 12f, 0, 0, rotationX: -0.8727f),
            PlayerModelDefinition.FromVanillaChild("head_right", "head", 0f, 0f, 0f, -7f, -4.0981f, -8.4879f, 2f, 5f, 2f, 44, 29),
            PlayerModelDefinition.FromVanillaChild("head_left", "head", 0f, 0f, 0f, 5f, -4.0981f, -8.4879f, 2f, 5f, 2f, 52, 29),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -5f, -1f, -1.5f, -5.1f, -0.5f, -2f, 6f, 1f, 4f, 32, 5, rotationZ: -0.8727f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 5f, -1f, -1.5f, -0.9f, -0.5f, -2f, 6f, 1f, 4f, 32, 0, mirror: true, rotationZ: 0.8727f),
            PlayerModelDefinition.FromVanilla("body", 0f, 24f, 0f, -4f, -14f, -7f, 8f, 8f, 14f, 0, 16, grow: 0.02f),
            PlayerModelDefinition.FromVanillaChild("body_mane", "body", 0f, 0f, 0f, 0f, -18f, -8f, 0f, 6f, 11f, 24, 39, grow: 0.02f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 18f, 4.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 0, 47),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 18f, 4.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 12, 47),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 18f, -4.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 0, 38),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 18f, -4.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 12, 38)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsNether.Hoglin.Parts);
        AssertPartsMatch(expected, BabyModelDefinitionsNether.Zoglin.Parts);
    }

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
