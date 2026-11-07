// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-697: proves the DSL-baked baby horse/llama/camel/donkey parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsEquineTests
{
    [Fact]
    public void BabyHorsePartsMatchLegacyRecords()
    {
        var baseParts = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 12.5f, 0f, -4f, -3.5f, -7f, 8f, 7f, 14f, 0, 13),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -1f, 7f, -1.5f, -1.5f, -1f, 3f, 3f, 8f, 24, 34, rotationX: -0.7418f),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 2.4f, 16f, 5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 12, 46),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -2.4f, 16f, 5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 0, 46),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 2.4f, 16f, -5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 12, 34),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -2.4f, 16f, -5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 0, 34),
            LegacyModelPartFactory.FromVanilla("head_parts", 0f, 10f, -6f, -2f, -6f, -2f, 4f, 8f, 4f, 30, 0, rotationX: -0.6109f),
            LegacyModelPartFactory.FromVanillaChild("head", "head_parts", 0f, -6.0516f, -0.2951f, -3f, -3.9484f, -6.705f, 6f, 4f, 9f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 2f, -4.2484f, 1.9451f, -1f, -2.5f, -0.8f, 2f, 3f, 1f, 0, 4, rotationZ: 0.2618f),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -2f, -4.2484f, 1.645f, -1f, -2.5f, -0.5f, 2f, 3f, 1f, 0, 0, rotationZ: -0.2618f)
        };
        var expected = EntityModelLayerHelpers.WithHorseMarkings(baseParts, "entity/horse/horse_markings_", true);
        AssertPartsMatch(expected, BabyModelDefinitionsEquine.Horse.Parts);
        foreach (var key in HorseModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, BabyModelDefinitionsEquine.Variants[(HorseModelDefinition.HorseEntityTypeId, key)].Parts);
        }
    }

    [Fact]
    public void BabyLlamaPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 12f, -4f, -3f, -9f, -4f, 6f, 11f, 4f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -7f, -7f, 3f, 3f, 3f, 0, 15),
            LegacyModelPartFactory.FromVanillaChild("ear_right", "head", 0f, 0f, 0f, 0.5f, -11f, -3f, 2f, 2f, 2f, 20, 4),
            LegacyModelPartFactory.FromVanillaChild("ear_left", "head", 0f, 0f, 0f, -2.5f, -11f, -3f, 2f, 2f, 2f, 20, 0),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -2.5f, 16.5f, 4.5f, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f, 0, 45),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 2.5f, 16.5f, 4.5f, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f, 12, 45),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -2.5f, 16.5f, -3.5f, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f, 0, 34),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 2.5f, 16.5f, -3.5f, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f, 12, 34),
            LegacyModelPartFactory.FromVanilla("body", 0f, 14f, 2.5f, -4f, -3f, -8.5f, 8f, 6f, 13f, 0, 15),
            LegacyModelPartFactory.FromVanilla("right_chest", -8.5f, 4f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 28, rotationY: -MathF.PI / 2f),
            LegacyModelPartFactory.FromVanilla("left_chest", 5.5f, 4f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 41, rotationY: -MathF.PI / 2f)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsEquine.Llama.Parts);
    }

    [Fact]
    public void BabyCamelPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 7f, 0f, -4.5f, -4f, -8f, 9f, 8f, 16f, 0, 14),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -1.5f, 8.05f, -1.5f, -0.5f, 0f, 3f, 9f, 0f, 50, 38),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, 1f, -7.5f, -2.5f, -3f, -7.5f, 5f, 5f, 7f, 20, 0),
            LegacyModelPartFactory.FromVanillaChild("head_upper", "head", 0f, 0f, 0f, -2.5f, -12f, -7.5f, 5f, 9f, 5f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("muzzle", "head", 0f, 0f, 0f, -2.5f, -12f, -10.5f, 5f, 4f, 3f, 0, 14),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -2.5f, -11f, -4f, -3f, -0.5f, -1f, 3f, 1f, 2f, 37, 0),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 2.5f, -11f, -4f, 0f, -0.5f, -1f, 3f, 1f, 2f, 47, 0),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -3f, 11.5f, -5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 36, 14),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 3f, 11.5f, -5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 48, 14),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 3f, 11.5f, 5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 12, 38),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -3f, 11.5f, 5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 0, 38)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsEquine.Camel.Parts);
    }

    [Fact]
    public void BabyDonkeyPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 1f, 14f, 0f, -5f, -3f, -7f, 8f, 6f, 14f, 0, 13),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -1.5f, 6.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("tail_r1", "tail", 0f, 0f, 0f, -2.5f, -1f, -0.5f, 3f, 3f, 8f, 24, 33, rotationX: -0.7418f),
            LegacyModelPartFactory.FromVanillaChild("left_hind_leg", "body", 2.25f, 3.5f, 5.25f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 12, 44),
            LegacyModelPartFactory.FromVanillaChild("right_hind_leg", "body", -2.4f, 3.5f, 5.4f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 0, 44),
            LegacyModelPartFactory.FromVanillaChild("left_front_leg", "body", 2.4f, 3.5f, -5.3f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 12, 33),
            LegacyModelPartFactory.FromVanillaChild("right_front_leg", "body", -2.4f, 3.5f, -5.4f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 0, 33),
            LegacyModelPartFactory.FromVanillaChild("head_parts", "body", 0f, -3f, -5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("neck_r1", "head_parts", 0f, 0f, 0f, -3f, -6f, -3f, 4f, 8f, 4f, 30, 9, rotationX: 0.3927f),
            LegacyModelPartFactory.FromVanillaChild("head", "head_parts", 0f, -5f, -3f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("head_r1", "head", 0f, -1f, 1f, -4f, -3.6f, -8.4f, 6f, 4f, 9f, 0, 0, rotationX: 0.3927f),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 2f, -3.5f, -1f, -2f, -6.5f, -0.3f, 2f, 7f, 1f, 0, 0, rotationX: 0.48f, rotationZ: 0.48f),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -2f, -3.5f, -1f, -2f, -6.5f, -0.3f, 2f, 7f, 1f, 22, 0, mirror: true, rotationX: 0.48f, rotationZ: -0.48f),
            LegacyModelPartFactory.FromVanillaChild("right_chest", "body", -1f, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("left_chest", "body", -1f, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsEquine.Donkey.Parts);
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
