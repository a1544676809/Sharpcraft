// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-690: proves the DSL-baked Creeper/Spider/WitherBoss parts are value-identical to the source-frame records
/// (reference y-down literals, raw bake) and that mirrorX/rotation and model metadata remain unchanged.
/// </summary>
public sealed class CreeperSpiderWitherModelDefinitionTests
{

    [Fact]
    public void SpiderPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            RawModelPartFactory.FromVanilla("head", 0f, 15f, -3f, -4f, -4f, -8f, 8f, 8f, 8f, 32, 4, mirrorX: false),
            RawModelPartFactory.FromVanilla("body0", 0f, 15f, 0f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 0, mirrorX: false),
            RawModelPartFactory.FromVanilla("body1", 0f, 15f, 9f, -5f, -4f, -6f, 10f, 8f, 12f, 0, 12, mirrorX: false),
            Leg("right_hind_leg", -4f, 2f, false, MathF.PI / 4f, -MathF.PI / 4f),
            Leg("left_hind_leg", 4f, 2f, true, -MathF.PI / 4f, MathF.PI / 4f),
            Leg("right_middle_hind_leg", -4f, 1f, false, MathF.PI / 8f, -0.58119464f),
            Leg("left_middle_hind_leg", 4f, 1f, true, -MathF.PI / 8f, 0.58119464f),
            Leg("right_middle_front_leg", -4f, 0f, false, -MathF.PI / 8f, -0.58119464f),
            Leg("left_middle_front_leg", 4f, 0f, true, MathF.PI / 8f, 0.58119464f),
            Leg("right_front_leg", -4f, -1f, false, -MathF.PI / 4f, -MathF.PI / 4f),
            Leg("left_front_leg", 4f, -1f, true, MathF.PI / 4f, MathF.PI / 4f)
        };
        AssertPartsMatch(expected, SpiderModelDefinition.Definition.Parts);
        Assert.Equal("entity/spider/spider_eyes", SpiderModelDefinition.Definition.EmissiveTexturePath);
    }

    [Fact]
    public void NamesAndCountsRemain()
    {
        AssertNames(CreeperModelDefinition.Definition.Parts, ["head", "body", "left_hind_leg", "right_hind_leg", "left_front_leg", "right_front_leg"]);
        AssertNames(SpiderModelDefinition.Definition.Parts,
            ["head", "body0", "body1", "right_hind_leg", "left_hind_leg", "right_middle_hind_leg", "left_middle_hind_leg", "right_middle_front_leg", "left_middle_front_leg", "right_front_leg", "left_front_leg"]);
        AssertNames(WitherBossModelDefinition.Definition.Parts, ["shoulders", "ribcage", "rib_1", "rib_2", "rib_3", "tail", "center_head", "right_head", "left_head"]);
    }

    private static EntityModelPart Leg(string name, float pivotX, float pivotZ, bool mirror, float rotationY, float rotationZ) =>
        RawModelPartFactory.FromVanilla(name, pivotX, 15f, pivotZ, mirror ? -1f : -15f, -1f, -1f, 16f, 2f, 2f, 18, 0,
            mirror: mirror, mirrorX: false, rotationY: rotationY, rotationZ: rotationZ);

    private static void AssertNames(IReadOnlyList<EntityModelPart> parts, string[] names)
    {
        Assert.Equal(names.Length, parts.Count);
        for (var i = 0; i < names.Length; i++)
        {
            Assert.Equal(names[i], parts[i].Name);
        }
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
