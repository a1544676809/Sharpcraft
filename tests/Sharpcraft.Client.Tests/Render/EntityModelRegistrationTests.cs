// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Registration of the last hierarchical models (docs/modules/entity-render.md 9).</summary>
public sealed class EntityModelRegistrationTests
{
    [Fact]
    public void TheRabbitTypeIsRegisteredWithItsGroupNodes()
    {
        var definition = EntityModelResolver.Resolve(RabbitModelDefinition.RabbitEntityTypeId);

        Assert.Equal("rabbit", definition.Name);
        // 9 visible cubes plus group nodes: frontlegs, backlegs, right_hind_leg, left_hind_leg.
        Assert.Equal(13, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "frontlegs" && part.Parent == "body");
        Assert.Contains(definition.Parts, part => part.Name == "right_haunch" && part.Parent == "right_hind_leg");
    }

    [Fact]
    public void TheCamelTypeIsRegisteredWithItsHeadCubes()
    {
        var definition = EntityModelResolver.Resolve(CamelModelDefinition.CamelEntityTypeId);

        Assert.Equal("camel", definition.Name);
        Assert.Equal(13, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "head_lower" && part.Parent == "head");
        Assert.Contains(definition.Parts, part => part.Name == "hump" && part.Parent == "body");
    }

    [Fact]
    public void TheBatTypeIsRegisteredWithTwoSegmentWings()
    {
        var definition = EntityModelResolver.Resolve(BatModelDefinition.BatEntityTypeId);

        Assert.Equal("bat", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "right_wing_tip" && part.Parent == "right_wing");
        Assert.Contains(definition.Parts, part => part.Name == "left_ear" && part.Parent == "head");
    }

    [Fact]
    public void TheBeeTypeIsRegisteredWithBoneBodyAndWings()
    {
        var definition = EntityModelResolver.Resolve(BeeModelDefinition.BeeEntityTypeId);

        Assert.Equal("bee", definition.Name);
        Assert.Equal(10, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "body" && part.Parent == "bone");
        Assert.Contains(definition.Parts, part => part.Name == "stinger" && part.Parent == "body");
        Assert.Contains(definition.Parts, part => part.Name == "left_wing" && part.Parent == "bone");
    }

    [Fact]
    public void TheDolphinTypeIsRegisteredWithItsFinChain()
    {
        var definition = EntityModelResolver.Resolve(DolphinModelDefinition.DolphinEntityTypeId);

        Assert.Equal("dolphin", definition.Name);
        Assert.Equal(8, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "tail_fin" && part.Parent == "tail");
        Assert.Contains(definition.Parts, part => part.Name == "nose" && part.Parent == "head");
    }

    [Fact]
    public void TheGuardianAndElderShareGeometryWithASeparateScale()
    {
        var guardian = EntityModelResolver.Resolve(GuardianModelDefinition.GuardianEntityTypeId);
        var elder = EntityModelResolver.Resolve(GuardianModelDefinition.ElderGuardianEntityTypeId);

        Assert.Equal("guardian", guardian.Name);
        Assert.Equal(23, guardian.Parts.Count);
        Assert.Equal(12, guardian.Parts.Count(part => part.Name.StartsWith("spike", StringComparison.Ordinal)));
        Assert.Contains(guardian.Parts, part => part.Name == "eye" && part.Parent == "head");
        Assert.Contains(guardian.Parts, part => part.Name == "tail2_fin" && part.Parent == "tail2");
        Assert.Equal("elder_guardian", elder.Name);
        Assert.Equal(2.35f, elder.Scale);
    }

    [Fact]
    public void TheFrogAndTadpoleTypesAreRegistered()
    {
        var frog = EntityModelResolver.Resolve(FrogModelDefinition.FrogEntityTypeId);
        var tadpole = EntityModelResolver.Resolve(TadpoleModelDefinition.TadpoleEntityTypeId);

        Assert.Equal("frog", frog.Name);
        Assert.Equal(18, frog.Parts.Count);
        Assert.Contains(frog.Parts, part => part.Name == "right_eye" && part.Parent == "eyes");
        Assert.Contains(frog.Parts, part => part.Name == "body_layer" && part.Parent == "body");
        Assert.Equal("tadpole", tadpole.Name);
        Assert.Equal(2, tadpole.Parts.Count);
    }

    [Fact]
    public void TheAxolotlTypeIsRegisteredWithGillsAndVariants()
    {
        var definition = EntityModelResolver.Resolve(AxolotlModelDefinition.AxolotlEntityTypeId);

        Assert.Equal("axolotl", definition.Name);
        Assert.Equal(11, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "right_gills" && part.Parent == "head");
        Assert.Equal(5, AxolotlModelDefinition.VariantKeys.Count);
        Assert.Equal("entity/axolotl/axolotl_blue", EntityModelResolver.ResolveVariantStrict(AxolotlModelDefinition.AxolotlEntityTypeId, "blue").TexturePath);
    }

    [Fact]
    public void TheStriderTypeHasSixBristlesUnderTheBody()
    {
        var definition = EntityModelResolver.Resolve(StriderModelDefinition.StriderEntityTypeId);

        Assert.Equal("strider", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
        Assert.Equal(6, definition.Parts.Count(part => part.Name.EndsWith("bristle", StringComparison.Ordinal)));
        Assert.All(definition.Parts.Where(part => part.Name.EndsWith("bristle", StringComparison.Ordinal)), part => Assert.Equal("body", part.Parent));
    }

    [Fact]
    public void TheCamelTypeIdMatchesTheBaselineEntityTypeOrder()
    {
        // Derived from EntityTypes registration order (validated against the ids the other models already use).
        Assert.Equal(19, CamelModelDefinition.CamelEntityTypeId);
        Assert.Equal(111, RabbitModelDefinition.RabbitEntityTypeId);
    }
}
