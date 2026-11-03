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
    public void TheSkeletonVariantsReuseSkeletonGeometry()
    {
        Assert.Equal("stray", EntityModelResolver.Resolve(HumanoidMobModelDefinition.StrayEntityTypeId).Name);
        Assert.Equal("bogged", EntityModelResolver.Resolve(HumanoidMobModelDefinition.BoggedEntityTypeId).Name);
        Assert.Equal("wither_skeleton", EntityModelResolver.Resolve(HumanoidMobModelDefinition.WitherSkeletonEntityTypeId).Name);
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
    public void TheTurtleTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(TurtleModelDefinition.TurtleEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("turtle", definition.Name);
        Assert.Equal(8, definition.Parts.Count);
    }

    [Fact]
    public void TheBlazeTypeResolvesToHeadAndTwelveRods()
    {
        var definition = EntityModelResolver.Resolve(BlazeModelDefinition.BlazeEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("blaze", definition.Name);
        Assert.Equal(13, definition.Parts.Count);
    }

    [Fact]
    public void TheIllagerVariantsReuseIllagerGeometry()
    {
        Assert.Equal("vindicator", EntityModelResolver.Resolve(IllagerModelDefinition.VindicatorEntityTypeId).Name);
        Assert.Equal("evoker", EntityModelResolver.Resolve(IllagerModelDefinition.EvokerEntityTypeId).Name);
        Assert.Equal("pillager", EntityModelResolver.Resolve(IllagerModelDefinition.PillagerEntityTypeId).Name);
    }

    [Fact]
    public void TheIronGolemTypeResolvesToItsRootParts()
    {
        var definition = EntityModelResolver.Resolve(IronGolemModelDefinition.IronGolemEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("iron_golem", definition.Name);
        Assert.Equal(8, definition.Parts.Count);
    }

    [Fact]
    public void TheSilverfishTypeResolvesToSevenSegmentsAndThreeLayers()
    {
        var definition = EntityModelResolver.Resolve(SilverfishModelDefinition.SilverfishEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("silverfish", definition.Name);
        Assert.Equal(10, definition.Parts.Count);
    }

    [Fact]
    public void TheEndermiteTypeResolvesToFourSegments()
    {
        var definition = EntityModelResolver.Resolve(EndermiteModelDefinition.EndermiteEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("endermite", definition.Name);
        Assert.Equal(4, definition.Parts.Count);
    }

    [Fact]
    public void TheZombieVillagerTypeUsesHumanoidGeometry()
    {
        var definition = EntityModelResolver.Resolve(ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("zombie_villager", definition.Name);
        Assert.Equal(8, definition.Parts.Count);
    }

    [Fact]
    public void TheWanderingTraderReusesVillagerGeometry()
    {
        var definition = EntityModelResolver.Resolve(VillagerModelDefinition.WanderingTraderEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("wandering_trader", definition.Name);
        Assert.Equal(VillagerModelDefinition.Definition.Parts.Count, definition.Parts.Count);
    }

    [Fact]
    public void TheVillagerTypeResolvesToItsFullBodyHierarchy()
    {
        var definition = EntityModelResolver.Resolve(VillagerModelDefinition.VillagerEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("villager", definition.Name);
        Assert.Equal(12, definition.Parts.Count);
        Assert.Equal(0.9375f, definition.Scale);
        Assert.Contains(definition.Parts, part => part.Name == "hat_rim" && part.Parent == "hat");
        Assert.Contains(definition.Parts, part => part.Name == "jacket" && part.Parent == "body" && Math.Abs(part.Grow - 0.5f) < 0.001f);
        Assert.Contains(definition.Parts, part => part.Name == "arms_left" && part.Parent == "arms");
        Assert.NotEqual(0f, definition.Parts.Single(part => part.Name == "arms").RotationX);
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
    public void TheLlamaTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(LlamaModelDefinition.LlamaEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("llama", definition.Name);
        Assert.Equal(22, definition.Parts.Count);
    }

    [Fact]
    public void TheZombieAndSkeletonHorseReuseEquineGeometry()
    {
        Assert.Equal("zombie_horse", EntityModelResolver.Resolve(HorseModelDefinition.ZombieHorseEntityTypeId).Name);
        Assert.Equal("skeleton_horse", EntityModelResolver.Resolve(HorseModelDefinition.SkeletonHorseEntityTypeId).Name);
    }

    [Fact]
    public void TheMuleTypeReusesEquineGeometry()
    {
        var definition = EntityModelResolver.Resolve(HorseModelDefinition.MuleEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("mule", definition.Name);
        Assert.Equal(0.92f, definition.Scale);
    }

    [Fact]
    public void TheDonkeyTypeReusesEquineGeometry()
    {
        var definition = EntityModelResolver.Resolve(HorseModelDefinition.DonkeyEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("donkey", definition.Name);
        Assert.Equal(0.87f, definition.Scale);
    }

    [Fact]
    public void TheHorseTypeResolvesToItsParts()
    {
        var definition = EntityModelResolver.Resolve(HorseModelDefinition.HorseEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("horse", definition.Name);
        Assert.Equal(240, definition.Parts.Count);
    }

    [Fact]
    public void TheGlowSquidTypeReusesSquidGeometry()
    {
        var definition = EntityModelResolver.Resolve(SquidModelDefinition.GlowSquidEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("glow_squid", definition.Name);
        Assert.Equal(SquidModelDefinition.Definition.Parts.Count, definition.Parts.Count);
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
        // 11 visible cubes plus two empty group nodes ("head", "tail"), doubled by collar and equipment layers.
        Assert.Equal(52, definition.Parts.Count);
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
    public void TheMooshroomTypeReusesCowGeometry()
    {
        var definition = EntityModelResolver.Resolve(CowModelDefinition.MooshroomEntityTypeId);

        Assert.NotNull(definition);
        Assert.Equal("mooshroom", definition.Name);
        Assert.Equal(CowModelDefinition.Definition.Parts.Count, definition.Parts.Count);
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





