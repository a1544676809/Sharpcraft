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
    public void TheVillagerTypeResolvesToNinePartsAndScale()
    {
        var definition = EntityModelResolver.Resolve(VillagerModelDefinition.VillagerEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("villager", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
        Assert.Equal(0.9375f, definition.Scale);
        Assert.NotEqual(0f, definition.Parts[4].RotationX);
    }

    [Fact]
    public void TheSheepTypeResolvesToAQuadrupedWithWool()
    {
        var definition = EntityModelResolver.Resolve(SheepModelDefinition.SheepEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("sheep", definition.Name);
        Assert.Equal(12, definition.Parts.Count);
        Assert.Contains("entity/sheep/sheep_wool", definition.TexturePaths);
    }

    [Fact]
    public void TheOcelotTypeReusesTheFelineGeometry()
    {
        var definition = EntityModelResolver.Resolve(CatModelDefinition.OcelotEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("ocelot", definition.Name);
        Assert.Equal(CatModelDefinition.Definition.Parts.Count, definition.Parts.Count);
    }

    [Fact]
    public void TheSquidTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(SquidModelDefinition.SquidEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("squid", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
    }

    [Fact]
    public void TheSnowGolemTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(SnowGolemModelDefinition.SnowGolemEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("snow_golem", definition.Name);
        Assert.Equal(5, definition.Parts.Count);
    }

    [Fact]
    public void TheGoatTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(GoatModelDefinition.GoatEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("goat", definition.Name);
        Assert.Equal(12, definition.Parts.Count);
    }

    [Fact]
    public void ThePandaTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(PandaModelDefinition.PandaEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("panda", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
    }

    [Fact]
    public void ThePolarBearTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(PolarBearModelDefinition.PolarBearEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("polar_bear", definition.Name);
        Assert.Equal(1.2f, definition.Scale);
    }

    [Fact]
    public void TheFoxTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(FoxModelDefinition.FoxEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("fox", definition.Name);
        Assert.Equal(10, definition.Parts.Count);
    }

    [Fact]
    public void TheCatTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(CatModelDefinition.CatEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("cat", definition.Name);
        Assert.Equal(0.8f, definition.Scale);
    }

    [Fact]
    public void TheWolfTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(WolfModelDefinition.WolfEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("wolf", definition.Name);
        Assert.Equal(11, definition.Parts.Count);
    }

    [Fact]
    public void TheChickenTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(ChickenModelDefinition.ChickenEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("chicken", definition.Name);
        Assert.Equal(8, definition.Parts.Count);
    }

    [Fact]
    public void TheCowTypeResolvesToAQuadrupedWithHornsAndUdder()
    {
        var definition = EntityModelResolver.Resolve(CowModelDefinition.CowEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("cow", definition.Name);
        Assert.Equal(10, definition.Parts.Count);
    }

    [Fact]
    public void ThePigTypeResolvesToAQuadrupedDefinition()
    {
        var definition = EntityModelResolver.Resolve(PigModelDefinition.PigEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("pig", definition.Name);
        Assert.Equal(7, definition.Parts.Count);
        Assert.NotEqual(0f, definition.Parts[2].RotationX);
    }

    [Fact]
    public void TheSpiderTypeResolvesToItsBodyAndEightLegs()
    {
        var definition = EntityModelResolver.Resolve(SpiderModelDefinition.SpiderEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("spider", definition.Name);
        Assert.Equal(11, definition.Parts.Count);
        Assert.NotEqual(0f, definition.Parts[3].RotationY);
    }

    [Fact]
    public void TheMagmaCubeTypeResolvesToNineFlatParts()
    {
        var definition = EntityModelResolver.Resolve(MagmaCubeModelDefinition.MagmaCubeEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("magma_cube", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
    }

    [Fact]
    public void TheSlimeTypeResolvesToCubeBodyDefinition()
    {
        var definition = EntityModelResolver.Resolve(SlimeModelDefinition.SlimeEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("slime", definition.Name);
        Assert.Equal(5, definition.Parts.Count);
        Assert.Equal("outer_cube", definition.Parts[4].Name);
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
