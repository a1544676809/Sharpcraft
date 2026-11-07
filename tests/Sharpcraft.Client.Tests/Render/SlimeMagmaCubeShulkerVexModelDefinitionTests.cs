// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-688: proves the DSL-baked Slime/MagmaCube/Shulker/Vex parts are value-identical to the legacy records and that
/// translucent kinds, variants, selectors and order remain unchanged.
/// </summary>
public sealed class SlimeMagmaCubeShulkerVexModelDefinitionTests
{
    [Fact]
    public void SlimePartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("cube", 0f, 0f, 0f, -3f, 17f, -3f, 6f, 6f, 6f, 0, 16),
            LegacyModelPartFactory.FromVanilla("right_eye", 0f, 0f, 0f, -3.25f, 18f, -3.5f, 2f, 2f, 2f, 32, 0),
            LegacyModelPartFactory.FromVanilla("left_eye", 0f, 0f, 0f, 1.25f, 18f, -3.5f, 2f, 2f, 2f, 32, 4),
            LegacyModelPartFactory.FromVanilla("mouth", 0f, 0f, 0f, 0f, 21f, -3.5f, 1f, 1f, 1f, 32, 8),
            LegacyModelPartFactory.FromVanilla("outer_cube", 0f, 0f, 0f, -4f, 16f, -4f, 8f, 8f, 8f, 0, 0, kind: EntityGeometryLayerKind.Translucent)
        };
        AssertPartsMatch(expected, SlimeModelDefinition.Definition.Parts);
    }

    [Fact]
    public void MagmaCubePartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("cube0", 0f, 0f, 0f, -4f, 16f, -4f, 8f, 1f, 8f, 0, 0),
            LegacyModelPartFactory.FromVanilla("cube1", 0f, 0f, 0f, -4f, 17f, -4f, 8f, 1f, 8f, 0, 9),
            LegacyModelPartFactory.FromVanilla("cube2", 0f, 0f, 0f, -4f, 18f, -4f, 8f, 1f, 8f, 0, 18),
            LegacyModelPartFactory.FromVanilla("cube3", 0f, 0f, 0f, -4f, 19f, -4f, 8f, 1f, 8f, 0, 27),
            LegacyModelPartFactory.FromVanilla("cube4", 0f, 0f, 0f, -4f, 20f, -4f, 8f, 1f, 8f, 32, 0),
            LegacyModelPartFactory.FromVanilla("cube5", 0f, 0f, 0f, -4f, 21f, -4f, 8f, 1f, 8f, 32, 9),
            LegacyModelPartFactory.FromVanilla("cube6", 0f, 0f, 0f, -4f, 22f, -4f, 8f, 1f, 8f, 32, 18),
            LegacyModelPartFactory.FromVanilla("cube7", 0f, 0f, 0f, -4f, 23f, -4f, 8f, 1f, 8f, 32, 27),
            LegacyModelPartFactory.FromVanilla("inside_cube", 0f, 0f, 0f, -2f, 18f, -2f, 4f, 4f, 4f, 24, 40)
        };
        AssertPartsMatch(expected, MagmaCubeModelDefinition.Definition.Parts);
    }

    [Fact]
    public void ShulkerPartsAndVariantsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("lid", 0f, 24f, 0f, -8f, -16f, -8f, 16f, 12f, 16f, 0, 0),
            LegacyModelPartFactory.FromVanilla("base", 0f, 24f, 0f, -8f, -8f, -8f, 16f, 8f, 16f, 0, 28),
            LegacyModelPartFactory.FromVanilla("head", 0f, 12f, 0f, -3f, 0f, -3f, 6f, 6f, 6f, 0, 52)
        };
        AssertPartsMatch(expected, ShulkerModelDefinition.Definition.Parts);
        foreach (var key in ShulkerModelDefinition.VariantKeys)
        {
            AssertPartsMatch(expected, ShulkerModelDefinition.Variants[key].Parts);
        }

        Assert.Equal(16, ShulkerModelDefinition.VariantKeys.Count);
        Assert.Equal("shulker_white", ShulkerModelDefinition.Variants["white"].Name);
        Assert.Equal("entity/shulker/shulker_white", ShulkerModelDefinition.Variants["white"].TexturePath);
        Assert.Equal("entity/shulker/shulker_red", ShulkerModelDefinition.Variants["red"].TexturePath);
    }

    [Fact]
    public void VexPartsMatchLegacyRecords()
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("root", 0f, -2.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("head", "root", 0f, 20f, 0f, -2.5f, -5f, -2.5f, 5f, 5f, 5f, 0, 0),
            LegacyModelPartFactory.FromVanillaChild("body", "root", 0f, 20f, 0f, -1.5f, 0f, -1f, 3f, 4f, 2f, 0, 10),
            LegacyModelPartFactory.FromVanillaChild("body_inner", "body", 0f, 0f, 0f, -1.5f, 1f, -1f, 3f, 5f, 2f, 0, 16, grow: -0.2f),
            LegacyModelPartFactory.FromVanillaChild("right_arm", "body", -1.75f, 0.25f, 0f, -1.25f, -0.5f, -1f, 2f, 4f, 2f, 23, 0, grow: -0.1f),
            LegacyModelPartFactory.FromVanillaChild("left_arm", "body", 1.75f, 0.25f, 0f, -0.75f, -0.5f, -1f, 2f, 4f, 2f, 23, 6, grow: -0.1f),
            LegacyModelPartFactory.FromVanillaChild("left_wing", "body", 0.5f, 1f, 1f, 0f, 0f, 0f, 0f, 5f, 8f, 16, 14, mirror: true),
            LegacyModelPartFactory.FromVanillaChild("right_wing", "body", -0.5f, 1f, 1f, 0f, 0f, 0f, 0f, 5f, 8f, 16, 14)
        };
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = expected[i] with { Kind = EntityGeometryLayerKind.Translucent };
        }

        AssertPartsMatch(expected, VexModelDefinition.Definition.Parts);
        Assert.NotNull(VexModelDefinition.Definition.TextureSelector);
        Assert.Equal("entity/illager/vex_charging", VexModelDefinition.Definition.TextureSelector!(ChargingState(true)));
        Assert.Equal("entity/illager/vex", VexModelDefinition.Definition.TextureSelector!(ChargingState(false)));
        Assert.Equal("entity/illager/vex_charging", Assert.Single(VexModelDefinition.Definition.AdditionalTexturePaths));
    }

    [Fact]
    public void NamesAndCountsRemain()
    {
        AssertNames(SlimeModelDefinition.Definition.Parts, ["cube", "right_eye", "left_eye", "mouth", "outer_cube"]);
        AssertNames(MagmaCubeModelDefinition.Definition.Parts, ["cube0", "cube1", "cube2", "cube3", "cube4", "cube5", "cube6", "cube7", "inside_cube"]);
        AssertNames(ShulkerModelDefinition.Definition.Parts, ["lid", "base", "head"]);
        AssertNames(VexModelDefinition.Definition.Parts, ["root", "head", "body", "body_inner", "right_arm", "left_arm", "left_wing", "right_wing"]);
    }

    private static Sharpcraft.Client.World.EntityRenderState ChargingState(bool charging) =>
        new(1, VexModelDefinition.VexEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsCharging: charging);

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
