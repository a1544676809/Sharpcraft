// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-699: proves the DSL-baked baby cat/wolf/fox/rabbit parts match the legacy records and collar layers.</summary>
public sealed class BabyModelDefinitionsExtraTests
{
    [Fact]
    public void BabyCatPartsMatchLegacyRecords()
    {
        var baseParts = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 20f, -3.125f, -2.5f, -3f, -2.875f, 5f, 4f, 4f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2f, -4f, -0.875f, 1f, 1f, 2f, 18, 0),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 1f, -4f, -0.875f, 1f, 1f, 2f, 24, 0),
            LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1.5f, -1f, -3.875f, 3f, 2f, 1f, 18, 3),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 1f, 22f, -1.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 18, 18),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -1f, 22f, -1.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 12, 18),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 1f, 22f, 2.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 18, 22),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -1f, 22f, 2.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 12, 22),
            LegacyModelPartFactory.FromVanilla("body", 0f, 20.5f, 0.5f, -2f, -1.5f, -3.5f, 4f, 3f, 7f, 0, 8),
            LegacyModelPartFactory.FromVanilla("tail1", 0f, 19.107f, 3.9151f, -0.5f, -0.107f, 0.0849f, 1f, 1f, 5f, 0, 18, rotationX: 0.567232f),
            LegacyModelPartFactory.FromVanilla("tail2", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0)
        };
        var expected = EntityModelLayerHelpers.WithCollar(baseParts, "entity/cat/cat_collar_baby");
        AssertPartsMatch(expected, BabyModelDefinitionsExtra.Cat.Parts);
    }

    [Fact]
    public void BabyWolfPartsMatchLegacyRecords()
    {
        var baseParts = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 18.25f, -4f, -2.99f, -3.25f, -3f, 6f, 5f, 5f, 0, 12, grow: 0.025f),
            LegacyModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -0.24f, -5f, 3f, 2f, 2f, 17, 12),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -2f, -4.25f, -0.5f, -1f, -1f, -0.5f, 2f, 2f, 1f, 0, 5),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 2f, -4.25f, -0.5f, -1f, -1f, -0.5f, 2f, 2f, 1f, 20, 5),
            LegacyModelPartFactory.FromVanilla("body", 0f, 19f, 0f, -3f, -2f, -4f, 6f, 4f, 8f, 0, 0),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -1.5f, 21f, 3f, -1f, 0f, -1f, 2f, 3f, 2f, 0, 22),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 1.5f, 21f, 3f, -1f, 0f, -1f, 2f, 3f, 2f, 8, 22),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -1.5f, 21f, -3f, -1f, 0f, -1f, 2f, 3f, 2f, 0, 0),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 1.5f, 21f, -3f, -1f, 0f, -1f, 2f, 3f, 2f, 20, 0),
            LegacyModelPartFactory.FromVanilla("tail", 0f, 19f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: 0.5236f),
            LegacyModelPartFactory.FromVanillaChild("tail_r1", "tail", 0f, -0.6f, 0.2f, -1f, -5.7f, -1f, 2f, 6f, 2f, 22, 16, rotationX: -3.1f)
        };
        var expected = EntityModelLayerHelpers.WithCollar(baseParts, "entity/wolf/wolf_collar_baby");
        AssertPartsMatch(expected, BabyModelDefinitionsExtra.Wolf.Parts);
        Assert.NotNull(BabyModelDefinitionsExtra.Wolf.TextureSelector);
    }

    [Fact]
    public void BabyFoxPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 18.125f, 0.125f, -3f, -2.125f, -5.125f, 6f, 5f, 5f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1f, 0.875f, -7.125f, 2f, 2f, 2f, 18, 20),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -3f, -4.125f, -4.125f, 2f, 2f, 1f, 22, 8),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 1f, -4.125f, -4.125f, 2f, 2f, 1f, 22, 11),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -1.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 4),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 1.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 0),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -1.5f, 22f, 0f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 4),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 1.5f, 22f, 0f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 0),
            LegacyModelPartFactory.FromVanilla("body", 0f, 20f, 2f, -2.5f, -2f, -3f, 5f, 4f, 6f, 0, 10),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -0.5f, 3f, -1.5f, -1.48f, -1f, 3f, 3f, 6f, 0, 20)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsExtra.Fox.Parts);
    }

    [Fact]
    public void BabyRabbitPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 23f, 1.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("body_r1", "body", 0f, -2f, -1.6f, -2f, -2f, -3f, 4f, 3f, 6f, 0, 8, rotationX: -0.5236f),
            LegacyModelPartFactory.FromVanillaChild("tail", "body", 0f, -2.2f, 2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("tail_r1", "tail", -0.1f, 0f, 0f, -1.4f, -2.0268f, -1.0177f, 3f, 3f, 3f, 0, 21, rotationX: -0.5236f),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -5f, -2.6f, -2.5f, -3f, -3f, 5f, 4f, 4f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("right_ear", "head", -1.5f, -3.5f, -0.5f, -1f, -3.5f, -0.5f, 2f, 4f, 1f, 18, 0),
            LegacyModelPartFactory.FromVanillaChild("left_ear", "head", 1.5f, -3.5f, -0.5f, -1f, -3.5f, -0.5f, 2f, 4f, 1f, 24, 0),
            LegacyModelPartFactory.FromVanillaChild("frontlegs", "body", 0f, -2.5f, -2.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("left_front_leg", "frontlegs", 1f, 1f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: 0.3927f),
            LegacyModelPartFactory.FromVanillaChild("left_front_leg_r1", "left_front_leg", 0f, 1f, 0f, -0.5f, -1.5f, -0.5f, 1f, 3f, 1f, 18, 8, rotationX: -0.3927f),
            LegacyModelPartFactory.FromVanillaChild("right_front_leg", "frontlegs", -1f, 1f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: 0.3927f),
            LegacyModelPartFactory.FromVanillaChild("right_front_leg_r1", "right_front_leg", 0f, 1f, 0f, -0.5f, -1.5f, -0.5f, 1f, 3f, 1f, 14, 8, rotationX: -0.3927f),
            LegacyModelPartFactory.FromVanilla("backlegs", 0f, 23f, 2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("left_hind_leg", "backlegs", 1.5f, 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationY: 3.1416f),
            LegacyModelPartFactory.FromVanillaChild("left_haunch", "left_hind_leg", 1f, 0f, 0.5f, -2f, -0.5f, 0f, 2f, 1f, 3f, 10, 17, rotationY: -0.7854f),
            LegacyModelPartFactory.FromVanillaChild("right_hind_leg", "backlegs", -1.5f, 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationY: 3.1416f),
            LegacyModelPartFactory.FromVanillaChild("right_haunch", "right_hind_leg", 0.5f, 0f, -0.9f, -2f, -0.5f, 0f, 2f, 1f, 3f, 0, 17, rotationY: 0.7854f)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsExtra.Rabbit.Parts);
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
