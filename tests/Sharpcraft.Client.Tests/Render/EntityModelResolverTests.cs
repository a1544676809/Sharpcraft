// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class EntityModelResolverTests
{
    [Fact]
    public void ThePlayerTypeResolvesToThePlayerDefinition()
    {
        var definition = EntityModelResolver.Resolve(Sharpcraft.Client.Render.EntityPlaceholderMesher.PlayerEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("player", definition.Name);
        Assert.Equal(6, definition.Parts.Count);
    }

    [Fact]
    public void TheSkeletonTypeResolvesToTheHumanoidSkeletonDefinition()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.SkeletonEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("skeleton", definition.Name);
        Assert.Equal(6, definition.Parts.Count);
    }

    [Fact]
    public void TheCreeperTypeResolvesToSixFlatParts()
    {
        var definition = EntityModelResolver.Resolve(CreeperModelDefinition.CreeperEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("creeper", definition.Name);
        Assert.Equal(6, definition.Parts.Count);
        Assert.Equal(4f, definition.Parts[2].CentreZ);
    }

    [Fact]
    public void TheEndermanTypeResolvesToItsTallHumanoidDefinition()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.EndermanEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("enderman", definition.Name);
        Assert.Equal(30f, definition.Parts[2].Height);
    }

    [Fact]
    public void TheDrownedTypeResolvesToTheDrownedDefinition()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.DrownedEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("drowned", definition.Name);
        Assert.Contains("entity/zombie/drowned_outer_layer", definition.TexturePaths);
    }

    [Fact]
    public void TheHuskTypeResolvesToTheScaledZombieDefinition()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.HuskEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("husk", definition.Name);
        Assert.Equal(1.0625f, definition.Scale);
    }

    [Fact]
    public void TheZombieTypeResolvesToTheHumanoidZombieDefinition()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("zombie", definition.Name);
        Assert.Equal(6, definition.Parts.Count);
    }

    [Fact]
    public void AnUnregisteredTypeHasNoDefinition()
    {
        Assert.Null(EntityModelResolver.TryResolve(123456));
        Assert.Equal("placeholder", EntityModelResolver.Resolve(123456).Name);
    }
}
