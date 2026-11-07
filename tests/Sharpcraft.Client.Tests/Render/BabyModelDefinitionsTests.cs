// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-701: proves the DSL-baked baby pig/cow/chicken parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsTests
{
    [Fact]
    public void BabyPigPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 19f, 0.5f, -3.5f, -3f, -4.5f, 7f, 6f, 9f, 0, 0),
            PlayerModelDefinition.FromVanilla("head", 0f, 19f, -2f, -3.5f, -5f, -5f, 7f, 6f, 6f, 0, 15, grow: 0.025f),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -1.975f, -6f, 3f, 2f, 1f, 6, 27, grow: 0.015f),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 22f, -3f, -1f, 0f, -1f, 2f, 2f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 22f, -3f, -1f, 0f, -1f, 2f, 2f, 2f, 23, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 0, 4),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 23, 4)
        };
        AssertPartsMatch(expected, BabyModelDefinitions.Pig.Parts);
    }

    [Fact]
    public void BabyCowPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 13.569f, -5.1667f, -3f, -4.569f, -4.8333f, 6f, 6f, 5f, 0, 18),
            PlayerModelDefinition.FromVanillaChild("ear_right", "head", 0f, 0f, 0f, 3f, -5.569f, -3.8333f, 1f, 2f, 1f, 8, 29),
            PlayerModelDefinition.FromVanillaChild("ear_left", "head", 0f, 0f, 0f, -4f, -5.569f, -3.8333f, 1f, 2f, 1f, 4, 29, mirror: true),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -2f, -1.569f, -5.8333f, 4f, 3f, 1f, 12, 29),
            PlayerModelDefinition.FromVanilla("body", 3f, 19f, -5f, -7f, -7f, -1f, 8f, 6f, 12f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 18f, -3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 22, 18),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 18f, -3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 34, 18),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 18f, 3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 22, 27),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 18f, 3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 34, 27)
        };
        AssertPartsMatch(expected, BabyModelDefinitions.Cow.Parts);
    }

    [Fact]
    public void BabyChickenPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 20.25f, -1.25f, -2f, -2.25f, -0.75f, 4f, 4f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("beak", "body", 0f, 0f, 0f, -1f, -0.25f, -1.75f, 2f, 1f, 1f, 10, 8),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 22f, 0.5f, -0.5f, 0f, 0f, 1f, 2f, 0f, 2, 2),
            PlayerModelDefinition.FromVanillaChild("left_foot", "left_leg", 0f, 0f, 0f, -0.5f, 2f, -1f, 1f, 0f, 1f, 0, 1),
            PlayerModelDefinition.FromVanilla("right_leg", -1f, 22f, 0.5f, -0.5f, 0f, 0f, 1f, 2f, 0f, 0, 2),
            PlayerModelDefinition.FromVanillaChild("right_foot", "right_leg", 0f, 0f, 0f, -0.5f, 2f, -1f, 1f, 0f, 1f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_wing", 2f, 20f, 0f, 0f, 0f, -1f, 1f, 0f, 2f, 6, 8),
            PlayerModelDefinition.FromVanilla("left_wing", -2f, 20f, 0f, -1f, 0f, -1f, 1f, 0f, 2f, 4, 8)
        };
        AssertPartsMatch(expected, BabyModelDefinitions.Chicken.Parts);
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
