// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>T-695: proves the DSL-baked baby goat/panda/polar bear/turtle parts match the legacy records.</summary>
public sealed class BabyModelDefinitionsMiscTests
{
    [Fact]
    public void BabyGoatPartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("left_hind_leg", 1.5f, 19.5f, 3f, -1f, -0.5f, -1f, 2f, 5f, 2f, 29, 12),
            RawModelPartFactory.FromVanilla("right_hind_leg", -1.5f, 19.5f, 3f, -1f, -0.5f, -1f, 2f, 5f, 2f, 21, 12),
            RawModelPartFactory.FromVanilla("right_front_leg", -1.5f, 19.5f, -2f, -1f, -0.5f, -1f, 2f, 5f, 2f, 21, 5),
            RawModelPartFactory.FromVanilla("left_front_leg", 1.5f, 19.5f, -2f, -1f, -0.5f, -1f, 2f, 5f, 2f, 29, 5),
            RawModelPartFactory.FromVanilla("body", 0f, 17.8f, 0f, -3f, -2.3f, -4.5f, 6f, 5f, 9f, 0, 10),
            RawModelPartFactory.FromVanillaChild("body_lower", "body", 0f, 0f, 0f, -2.5f, -2.2f, -4f, 5f, 4f, 8f, 0, 24),
            RawModelPartFactory.FromVanilla("head", 0f, 15.5f, -3f, -2f, -3.8126f, -5.1548f, 4f, 4f, 6f, 0, 0, rotationX: 0.4363f),
            RawModelPartFactory.FromVanillaChild("right_horn", "head", -1.5f, -1.5f, -1f, 0f, -4.5f, 0f, 1f, 2f, 1f, 24, 0, mirror: true, rotationX: -0.3927f),
            RawModelPartFactory.FromVanillaChild("left_horn", "head", -1.5f, -1.5f, -1f, 2f, -4.5f, 0f, 1f, 2f, 1f, 24, 0, mirror: true, rotationX: -0.3927f),
            RawModelPartFactory.FromVanillaChild("right_ear", "head", -1.7f, -2.3126f, 0.1452f, -2f, -0.5f, -0.5f, 2f, 1f, 1f, 0, 12, mirror: true, rotationY: -0.5236f),
            RawModelPartFactory.FromVanillaChild("left_ear", "head", 1.7f, -2.3126f, 0.1452f, 0f, -0.5f, -0.5f, 2f, 1f, 1f, 0, 12, rotationY: 0.5236f),
            RawModelPartFactory.FromVanillaChild("head_main", "head", 0f, -1.3126f, -1.1548f, -2f, -2.5f, -4f, 4f, 4f, 6f, 0, 0)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsMisc.Goat.Parts);
    }

    [Fact]
    public void BabyPandaPartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("body", 0f, 18.5f, 2.5f, -4.5f, -3.5f, -5.5f, 9f, 7f, 11f, 0, 11),
            RawModelPartFactory.FromVanilla("head", 0f, 19f, -3f, -3.5f, -3f, -5f, 7f, 6f, 5f, 0, 0),
            RawModelPartFactory.FromVanillaChild("mouth", "head", 0f, 0f, 0f, -2f, 1f, -6f, 4f, 2f, 1f, 24, 6),
            RawModelPartFactory.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -4.5f, -4f, -3.5f, 3f, 3f, 1f, 24, 0),
            RawModelPartFactory.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 1.5f, -4f, -3.5f, 3f, 3f, 1f, 33, 0),
            RawModelPartFactory.FromVanilla("right_hind_leg", -3f, 22f, 6.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 0, 34),
            RawModelPartFactory.FromVanilla("left_hind_leg", 3f, 22f, 6.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 12, 34),
            RawModelPartFactory.FromVanilla("right_front_leg", -3f, 22f, -1.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 0, 29),
            RawModelPartFactory.FromVanilla("left_front_leg", 3f, 22f, -1.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 12, 29)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsMisc.Panda.Parts);
        foreach (var key in PandaModelDefinition.VariantKeys)
        {
            if (key != "normal")
            {
                AssertPartsMatch(expected, BabyModelDefinitionsMisc.Variants[(97, key)].Parts);
            }
        }
    }

    [Fact]
    public void BabyPolarBearPartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("body", 0f, 17.5f, 0f, -4f, -3.5f, -6f, 8f, 7f, 12f, 0, 9),
            RawModelPartFactory.FromVanilla("head", 0f, 18.625f, -5.75f, -3f, -2.625f, -4.25f, 6f, 5f, 4f, 0, 0),
            RawModelPartFactory.FromVanillaChild("snout", "head", 0f, 0f, 0f, -2f, 0.375f, -6.25f, 4f, 2f, 2f, 20, 3),
            RawModelPartFactory.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -4f, -3.625f, -2.75f, 2f, 2f, 1f, 20, 0),
            RawModelPartFactory.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 2f, -3.625f, -2.75f, 2f, 2f, 1f, 26, 0),
            RawModelPartFactory.FromVanilla("right_hind_leg", -2.5f, 21.5f, 4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 0, 34),
            RawModelPartFactory.FromVanilla("left_hind_leg", 2.5f, 21.5f, 4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 12, 34),
            RawModelPartFactory.FromVanilla("right_front_leg", -2.5f, 21.5f, -4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 0, 28),
            RawModelPartFactory.FromVanilla("left_front_leg", 2.5f, 21.5f, -4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 12, 28)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsMisc.PolarBear.Parts);
    }

    [Fact]
    public void BabyTurtlePartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("body", 0f, 22.9f, 1f, -2f, -1f, -2f, 4f, 2f, 4f, 0, 0),
            RawModelPartFactory.FromVanilla("head", 0f, 22.9f, -1f, -1.5f, -2f, -3f, 3f, 3f, 3f, 0, 6),
            RawModelPartFactory.FromVanilla("right_hind_leg", -2f, 23.9f, 2.5f, -2f, 0f, -0.5f, 2f, 0f, 1f, -1, 0),
            RawModelPartFactory.FromVanilla("left_hind_leg", 2f, 23.9f, 2.5f, 0f, 0f, -0.5f, 2f, 0f, 1f, -1, 1),
            RawModelPartFactory.FromVanilla("right_front_leg", -2f, 23.9f, -0.5f, -2f, 0f, -0.5f, 2f, 0f, 1f, 8, 6),
            RawModelPartFactory.FromVanilla("left_front_leg", 2f, 23.9f, -0.5f, 0f, 0f, -0.5f, 2f, 0f, 1f, 8, 7)
        };
        AssertPartsMatch(expected, BabyModelDefinitionsMisc.Turtle.Parts);
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
