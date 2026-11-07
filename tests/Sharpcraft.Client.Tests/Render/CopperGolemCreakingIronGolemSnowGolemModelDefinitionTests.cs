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
    public void CopperGolemPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("body", 0f, 19f, 0f, -4f, -6f, -3f, 8f, 6f, 6f, 0, 15),
            LegacyModelPartFactory.FromVanillaChild("head", "body", 0f, -6f, 0f, -4f, -5f, -5f, 8f, 5f, 10f, 0, 0, grow: 0.015f),
            LegacyModelPartFactory.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1f, -2f, -6f, 2f, 3f, 2f, 56, 0),
            LegacyModelPartFactory.FromVanillaChild("antenna", "head", 0f, 0f, 0f, -1f, -9f, -1f, 2f, 4f, 2f, 37, 8, grow: -0.015f),
            LegacyModelPartFactory.FromVanillaChild("antenna_tip", "head", 0f, 0f, 0f, -2f, -13f, -2f, 4f, 4f, 4f, 37, 0, grow: -0.015f),
            LegacyModelPartFactory.FromVanillaChild("right_arm", "body", -4f, -6f, 0f, -3f, -1f, -2f, 3f, 10f, 4f, 36, 16),
            LegacyModelPartFactory.FromVanillaChild("left_arm", "body", 4f, -6f, 0f, 0f, -1f, -2f, 3f, 10f, 4f, 50, 16),
            LegacyModelPartFactory.FromVanilla("right_leg", 0f, 19f, 0f, -4f, 0f, -2f, 4f, 5f, 4f, 0, 27),
            LegacyModelPartFactory.FromVanilla("left_leg", 0f, 19f, 0f, 0f, 0f, -2f, 4f, 5f, 4f, 16, 27)
        };
        AssertPartsMatch(expected, CopperGolemModelDefinition.Definition.Parts);
        foreach (var key in CopperGolemModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, CopperGolemModelDefinition.Variants[key].Parts);
        }
    }

    [Fact]
    public void CreakingPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("upper_body", "root", -1f, -19f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("head", "upper_body", -3f, -11f, 0f, -3f, -10f, -3f, 6f, 10f, 6f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("head_top", "head", 0f, 0f, 0f, -3f, -13f, -3f, 6f, 3f, 6f, 28, 31),
            LegacyModelPartFactory.FromVanillaChild("head_plate_right", "head", 0f, 0f, 0f, 3f, -13f, 0f, 9f, 14f, 0f, 12, 40),
            LegacyModelPartFactory.FromVanillaChild("head_plate_left", "head", 0f, 0f, 0f, -12f, -14f, 0f, 9f, 14f, 0f, 34, 12),
            LegacyModelPartFactory.FromVanillaChild("body", "upper_body", 0f, -7f, 1f, 0f, -3f, -3f, 6f, 13f, 5f, 0, 16),
            LegacyModelPartFactory.FromVanillaChild("body_front", "body", 0f, 0f, 0f, -6f, -4f, -3f, 6f, 7f, 5f, 24, 0),
            LegacyModelPartFactory.FromVanillaChild("right_arm", "upper_body", -7f, -9.5f, 1.5f, -2f, -1.5f, -1.5f, 3f, 21f, 3f, 22, 13),
            LegacyModelPartFactory.FromVanillaChild("right_hand", "right_arm", 0f, 0f, 0f, -2f, 19.5f, -1.5f, 3f, 4f, 3f, 46, 0),
            LegacyModelPartFactory.FromVanillaChild("left_arm", "upper_body", 6f, -9f, 0.5f, 0f, -1f, -1.5f, 3f, 16f, 3f, 30, 40),
            LegacyModelPartFactory.FromVanillaChild("left_upper", "left_arm", 0f, 0f, 0f, 0f, -5f, -1.5f, 3f, 4f, 3f, 52, 12),
            LegacyModelPartFactory.FromVanillaChild("left_hand", "left_arm", 0f, 0f, 0f, 0f, 15f, -1.5f, 3f, 4f, 3f, 52, 19),
            LegacyModelPartFactory.FromVanillaChild("left_leg", "root", 1.5f, -16f, 0.5f, -1.5f, 0f, -1.5f, 3f, 16f, 3f, 42, 40),
            LegacyModelPartFactory.FromVanillaChild("left_foot", "left_leg", 0f, 0f, 0f, -1.5f, 15.7f, -4.5f, 5f, 0f, 9f, 45, 55),
            LegacyModelPartFactory.FromVanillaChild("right_leg", "root", -1f, -17.5f, 0.5f, -3f, -1.5f, -1.5f, 3f, 19f, 3f, 0, 34),
            LegacyModelPartFactory.FromVanillaChild("right_foot", "right_leg", 0f, 0f, 0f, -5f, 17.2f, -4.5f, 5f, 0f, 9f, 45, 46),
            LegacyModelPartFactory.FromVanillaChild("right_knee", "right_leg", 0f, 0f, 0f, -3f, -4.5f, -1.5f, 3f, 3f, 3f, 12, 34)
        };
        AssertPartsMatch(expected, CreakingModelDefinition.Definition.Parts);
        Assert.Equal("entity/creaking/creaking_eyes", CreakingModelDefinition.Definition.EmissiveTexturePath);
    }

    [Fact]
    public void IronGolemPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, -7f, -2f, -4f, -12f, -5.5f, 8f, 10f, 8f, 0, 0),
            LegacyModelPartFactory.FromVanilla("nose", 0f, -7f, -2f, -1f, -5f, -7.5f, 2f, 4f, 2f, 24, 0),
            LegacyModelPartFactory.FromVanilla("body", 0f, -7f, 0f, -9f, -2f, -6f, 18f, 12f, 11f, 0, 40),
            LegacyModelPartFactory.FromVanilla("lower_body", 0f, -7f, 0f, -4.5f, 10f, -3f, 9f, 5f, 6f, 0, 70, grow: 0.5f),
            LegacyModelPartFactory.FromVanilla("right_arm", 0f, -7f, 0f, -13f, -2.5f, -3f, 4f, 30f, 6f, 60, 21),
            LegacyModelPartFactory.FromVanilla("left_arm", 0f, -7f, 0f, 9f, -2.5f, -3f, 4f, 30f, 6f, 60, 58, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_leg", -4f, 11f, 0f, -3.5f, -3f, -3f, 6f, 16f, 5f, 37, 0),
            LegacyModelPartFactory.FromVanilla("left_leg", 5f, 11f, 0f, -3.5f, -3f, -3f, 6f, 16f, 5f, 60, 0, mirror: true)
        };
        AssertPartsMatch(expected, IronGolemModelDefinition.Definition.Parts);
    }

    [Fact]
    public void SnowGolemPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 4f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, grow: -0.5f),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, 6f, 1f, -1f, 0f, -1f, 12f, 2f, 2f, 32, 0, grow: -0.5f, rotationZ: 1f),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, 6f, -1f, -1f, 0f, -1f, 12f, 2f, 2f, 32, 0, grow: -0.5f, rotationY: MathF.PI, rotationZ: 1f),
            LegacyModelPartFactory.FromVanilla("upper_body", 0f, 13f, 0f, -5f, -10f, -5f, 10f, 10f, 10f, 0, 16, grow: -0.5f),
            LegacyModelPartFactory.FromVanilla("lower_body", 0f, 24f, 0f, -6f, -12f, -6f, 12f, 12f, 12f, 0, 36, grow: -0.5f)
        };
        AssertPartsMatch(expected, SnowGolemModelDefinition.Definition.Parts);
    }

    [Fact]
    public void NamesAndMetadataRemain()
    {
        Assert.Equal(9, CopperGolemModelDefinition.Definition.Parts.Count);
        Assert.Equal(18, CreakingModelDefinition.Definition.Parts.Count);
        Assert.Equal(8, IronGolemModelDefinition.Definition.Parts.Count);
        Assert.Equal(5, SnowGolemModelDefinition.Definition.Parts.Count);
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
