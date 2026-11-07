// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-687: proves the DSL-baked Phantom/Ravager/PolarBear/Silverfish parts are value-identical to the legacy records and
/// that their nested order, emissive metadata and model scale remain unchanged.
/// </summary>
public sealed class PhantomRavagerPolarBearSilverfishModelDefinitionTests
{
    [Fact]
    public void PhantomPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -3f, -2f, -8f, 5f, 3f, 9f, 0, 8, rotationX: 0.1f),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 1f, -7f, -4f, -2f, -5f, 7f, 3f, 5f, 0, 0, rotationX: 0.2f),
            PlayerModelDefinition.FromVanillaChild("tail_base", "body", 0f, -2f, 1f, -2f, 0f, 0f, 3f, 2f, 6f, 3, 20),
            PlayerModelDefinition.FromVanillaChild("tail_tip", "tail_base", 0f, 0.5f, 6f, -1f, 0f, 0f, 1f, 1f, 6f, 4, 29),
            PlayerModelDefinition.FromVanillaChild("left_wing_base", "body", 2f, -2f, -8f, 0f, 0f, 0f, 6f, 2f, 9f, 23, 12, rotationZ: 0.1f),
            PlayerModelDefinition.FromVanillaChild("left_wing_tip", "left_wing_base", 6f, 0f, 0f, 0f, 0f, 0f, 13f, 1f, 9f, 16, 24, rotationZ: 0.1f),
            PlayerModelDefinition.FromVanillaChild("right_wing_base", "body", -3f, -2f, -8f, -6f, 0f, 0f, 6f, 2f, 9f, 23, 12, mirror: true, rotationZ: -0.1f),
            PlayerModelDefinition.FromVanillaChild("right_wing_tip", "right_wing_base", -6f, 0f, 0f, -13f, 0f, 0f, 13f, 1f, 9f, 16, 24, mirror: true, rotationZ: -0.1f)
        };
        AssertPartsMatch(expected, PhantomModelDefinition.Definition.Parts);
        Assert.Equal("entity/phantom/phantom_eyes", PhantomModelDefinition.Definition.EmissiveTexturePath);
    }

    [Fact]
    public void RavagerPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("neck", 0f, -7f, 5.5f, -5f, -1f, -18f, 10f, 10f, 18f, 68, 73),
            PlayerModelDefinition.FromVanillaChild("head", "neck", 0f, 16f, -17f, -8f, -20f, -14f, 16f, 20f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_snout", "head", 0f, 0f, 0f, -2f, -6f, -18f, 4f, 8f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_horn", "head", -10f, -14f, -8f, 0f, -14f, -2f, 2f, 14f, 4f, 74, 55, rotationX: 1.0995574f),
            PlayerModelDefinition.FromVanillaChild("left_horn", "head", 8f, -14f, -8f, 0f, -14f, -2f, 2f, 14f, 4f, 74, 55, mirror: true, rotationX: 1.0995574f),
            PlayerModelDefinition.FromVanillaChild("mouth", "head", 0f, -2f, 2f, -8f, 0f, -16f, 16f, 3f, 16f, 0, 36),
            PlayerModelDefinition.FromVanilla("body", 0f, 1f, 2f, -7f, -10f, -7f, 14f, 16f, 20f, 0, 55, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanillaChild("body_lower", "body", 0f, 0f, 0f, -6f, 6f, -7f, 12f, 13f, 18f, 0, 91),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -8f, -13f, 18f, -4f, 0f, -4f, 8f, 37f, 8f, 96, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 8f, -13f, 18f, -4f, 0f, -4f, 8f, 37f, 8f, 96, 0, mirror: true),
            PlayerModelDefinition.FromVanilla("right_front_leg", -8f, -13f, -5f, -4f, 0f, -4f, 8f, 37f, 8f, 64, 0),
            PlayerModelDefinition.FromVanilla("left_front_leg", 8f, -13f, -5f, -4f, 0f, -4f, 8f, 37f, 8f, 64, 0, mirror: true)
        };
        AssertPartsMatch(expected, RavagerModelDefinition.Definition.Parts);
    }

    [Fact]
    public void PolarBearPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 10f, -16f, -3.5f, -3f, -3f, 7f, 7f, 7f, 0, 0),
            PlayerModelDefinition.FromVanilla("mouth", 0f, 10f, -16f, -2.5f, 1f, -6f, 5f, 3f, 3f, 0, 44),
            PlayerModelDefinition.FromVanilla("right_ear", 0f, 10f, -16f, -4.5f, -4f, -1f, 2f, 2f, 1f, 26, 0),
            PlayerModelDefinition.FromVanilla("left_ear", 0f, 10f, -16f, 2.5f, -4f, -1f, 2f, 2f, 1f, 26, 0, mirror: true),
            PlayerModelDefinition.FromVanilla("body", -2f, 9f, 12f, -5f, -13f, -7f, 14f, 14f, 11f, 0, 19, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("body_upper", -2f, 9f, 12f, -4f, -25f, -7f, 12f, 12f, 10f, 39, 0, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -4.5f, 14f, 6f, -2f, 0f, -2f, 4f, 10f, 8f, 50, 22),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 4.5f, 14f, 6f, -2f, 0f, -2f, 4f, 10f, 8f, 50, 22),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3.5f, 14f, -8f, -2f, 0f, -2f, 4f, 10f, 6f, 50, 40),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3.5f, 14f, -8f, -2f, 0f, -2f, 4f, 10f, 6f, 50, 40)
        };
        AssertPartsMatch(expected, PolarBearModelDefinition.Definition.Parts);
        Assert.Equal(1.2f, PolarBearModelDefinition.Definition.Scale);
    }

    [Fact]
    public void SilverfishPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            PlayerModelDefinition.FromVanilla("segment0", 0f, 22f, -3.5f, -1.5f, 0f, -1f, 3f, 2f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("segment1", 0f, 21f, -1.5f, -2f, 0f, -1f, 4f, 3f, 2f, 0, 4),
            PlayerModelDefinition.FromVanilla("segment2", 0f, 20f, 1f, -3f, 0f, -1.5f, 6f, 4f, 3f, 0, 9),
            PlayerModelDefinition.FromVanilla("segment3", 0f, 21f, 4f, -1.5f, 0f, -1.5f, 3f, 3f, 3f, 0, 16),
            PlayerModelDefinition.FromVanilla("segment4", 0f, 22f, 6.5f, -1f, 0f, -1.5f, 2f, 2f, 3f, 0, 22),
            PlayerModelDefinition.FromVanilla("segment5", 0f, 23f, 8.5f, -1f, 0f, -1f, 2f, 1f, 2f, 11, 0),
            PlayerModelDefinition.FromVanilla("segment6", 0f, 23f, 10.5f, -0.5f, 0f, -1f, 1f, 1f, 2f, 13, 4),
            PlayerModelDefinition.FromVanilla("layer0", 0f, 16f, 1f, -5f, 0f, -1.5f, 10f, 8f, 3f, 20, 0),
            PlayerModelDefinition.FromVanilla("layer1", 0f, 20f, 6.5f, -3f, 0f, -1.5f, 6f, 4f, 3f, 20, 11),
            PlayerModelDefinition.FromVanilla("layer2", 0f, 19f, -1.5f, -3f, 0f, -1.5f, 6f, 5f, 2f, 20, 18)
        };
        AssertPartsMatch(expected, SilverfishModelDefinition.Definition.Parts);
    }

    [Fact]
    public void NestedOrderAndMetadataRemain()
    {
        AssertNames(PhantomModelDefinition.Definition.Parts,
            ["body", "head", "tail_base", "tail_tip", "left_wing_base", "left_wing_tip", "right_wing_base", "right_wing_tip"]);
        AssertNames(RavagerModelDefinition.Definition.Parts,
            ["neck", "head", "head_snout", "right_horn", "left_horn", "mouth", "body", "body_lower", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg"]);
        Assert.Equal(8, PhantomModelDefinition.Definition.Parts.Count);
        Assert.Equal(12, RavagerModelDefinition.Definition.Parts.Count);
        Assert.Equal(10, PolarBearModelDefinition.Definition.Parts.Count);
        Assert.Equal(10, SilverfishModelDefinition.Definition.Parts.Count);
    }

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
