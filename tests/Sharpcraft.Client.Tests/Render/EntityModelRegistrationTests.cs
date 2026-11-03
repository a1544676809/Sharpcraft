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
    public void TheCamelTypeIdMatchesTheBaselineEntityTypeOrder()
    {
        // Derived from EntityTypes registration order (validated against the ids the other models already use).
        Assert.Equal(19, CamelModelDefinition.CamelEntityTypeId);
        Assert.Equal(111, RabbitModelDefinition.RabbitEntityTypeId);
    }
}
