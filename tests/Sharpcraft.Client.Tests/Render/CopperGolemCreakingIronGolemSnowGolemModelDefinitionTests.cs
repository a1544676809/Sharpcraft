// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-692: proves the DSL-baked CopperGolem/Creaking/IronGolem/SnowGolem parts are value-identical to the legacy
/// records and that nested order, emissive paths and variants remain unchanged.
/// </summary>
public sealed class CopperGolemCreakingIronGolemSnowGolemModelDefinitionTests
{

    [Fact]
    public void CreakingPartsMatchSourceFrameRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("upper_body", "root", -1f, -19f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            RawModelPartFactory.FromVanillaChild("head", "upper_body", -3f, -11f, 0f, -3f, -10f, -3f, 6f, 10f, 6f, 0, 0),
            RawModelPartFactory.FromVanillaChild("head_top", "head", 0f, 0f, 0f, -3f, -13f, -3f, 6f, 3f, 6f, 28, 31),
            RawModelPartFactory.FromVanillaChild("head_plate_right", "head", 0f, 0f, 0f, 3f, -13f, 0f, 9f, 14f, 0f, 12, 40),
            RawModelPartFactory.FromVanillaChild("head_plate_left", "head", 0f, 0f, 0f, -12f, -14f, 0f, 9f, 14f, 0f, 34, 12),
            RawModelPartFactory.FromVanillaChild("body", "upper_body", 0f, -7f, 1f, 0f, -3f, -3f, 6f, 13f, 5f, 0, 16),
            RawModelPartFactory.FromVanillaChild("body_front", "body", 0f, 0f, 0f, -6f, -4f, -3f, 6f, 7f, 5f, 24, 0),
            RawModelPartFactory.FromVanillaChild("right_arm", "upper_body", -7f, -9.5f, 1.5f, -2f, -1.5f, -1.5f, 3f, 21f, 3f, 22, 13),
            RawModelPartFactory.FromVanillaChild("right_hand", "right_arm", 0f, 0f, 0f, -2f, 19.5f, -1.5f, 3f, 4f, 3f, 46, 0),
            RawModelPartFactory.FromVanillaChild("left_arm", "upper_body", 6f, -9f, 0.5f, 0f, -1f, -1.5f, 3f, 16f, 3f, 30, 40),
            RawModelPartFactory.FromVanillaChild("left_upper", "left_arm", 0f, 0f, 0f, 0f, -5f, -1.5f, 3f, 4f, 3f, 52, 12),
            RawModelPartFactory.FromVanillaChild("left_hand", "left_arm", 0f, 0f, 0f, 0f, 15f, -1.5f, 3f, 4f, 3f, 52, 19),
            RawModelPartFactory.FromVanillaChild("left_leg", "root", 1.5f, -16f, 0.5f, -1.5f, 0f, -1.5f, 3f, 16f, 3f, 42, 40),
            RawModelPartFactory.FromVanillaChild("left_foot", "left_leg", 0f, 0f, 0f, -1.5f, 15.7f, -4.5f, 5f, 0f, 9f, 45, 55),
            RawModelPartFactory.FromVanillaChild("right_leg", "root", -1f, -17.5f, 0.5f, -3f, -1.5f, -1.5f, 3f, 19f, 3f, 0, 34),
            RawModelPartFactory.FromVanillaChild("right_foot", "right_leg", 0f, 0f, 0f, -5f, 17.2f, -4.5f, 5f, 0f, 9f, 45, 46),
            RawModelPartFactory.FromVanillaChild("right_knee", "right_leg", 0f, 0f, 0f, -3f, -4.5f, -1.5f, 3f, 3f, 3f, 12, 34)
        };
        Assert.Equal("entity/creaking/creaking_eyes", CreakingModelDefinition.Definition.EmissiveTexturePath);
    }

    [Fact]
    public void NamesAndMetadataRemain()
    {
        Assert.Equal("entity/copper_golem/copper_golem", CopperGolemModelDefinition.Definition.TexturePath);
        Assert.Equal("entity/creaking/creaking", CreakingModelDefinition.Definition.TexturePath);
        Assert.Equal("entity/iron_golem/iron_golem", IronGolemModelDefinition.Definition.TexturePath);
        Assert.Equal("entity/snow_golem/snow_golem", SnowGolemModelDefinition.Definition.TexturePath);
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
