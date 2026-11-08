// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
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
        Assert.Equal(37, definition.Parts.Count);
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
    public void TheHoglinAndZoglinShareGeometryWithDifferentTextures()
    {
        var hoglin = EntityModelResolver.Resolve(HoglinModelDefinition.HoglinEntityTypeId);
        var zoglin = EntityModelResolver.Resolve(HoglinModelDefinition.ZoglinEntityTypeId);

        Assert.Equal("hoglin", hoglin.Name);
        Assert.Equal(11, hoglin.Parts.Count);
        Assert.Contains(hoglin.Parts, part => part.Name == "right_horn" && part.Parent == "head");
        Assert.Contains(hoglin.Parts, part => part.Name == "mane" && part.Parent == "body");
        Assert.Equal("entity/hoglin/zoglin", zoglin.TexturePath);
    }

    [Fact]
    public void ThePiglinFamilySharesHumanoidGeometryWithAPiglinHead()
    {
        var piglin = EntityModelResolver.Resolve(PiglinModelDefinition.PiglinEntityTypeId);
        var brute = EntityModelResolver.Resolve(PiglinModelDefinition.PiglinBruteEntityTypeId);
        var zombified = EntityModelResolver.Resolve(PiglinModelDefinition.ZombifiedPiglinEntityTypeId);

        Assert.Equal("piglin", piglin.Name);
        Assert.Equal(11, PiglinModelDefinition.Definition.Parts.Count);
        Assert.Equal(20, piglin.Parts.Count);
        Assert.Contains(piglin.Parts, part => part.Name == "head_nose" && part.Parent == "head");
        Assert.Contains(piglin.Parts, part => part.Name == "left_ear" && part.Parent == "head");
        Assert.Equal("entity/piglin/piglin_brute", brute.TexturePath);
        Assert.Equal("entity/piglin/zombified_piglin", zombified.TexturePath);
    }

    [Fact]
    public void TheAllayAndVexUseTheSmallTranslucentBody()
    {
        var allay = EntityModelResolver.Resolve(AllayModelDefinition.AllayEntityTypeId);
        var vex = EntityModelResolver.Resolve(VexModelDefinition.VexEntityTypeId);

        Assert.Equal("allay", allay.Name);
        Assert.Equal(8, allay.Parts.Count);
        Assert.All(allay.Parts, part => Assert.Equal(EntityGeometryLayerKind.Translucent, part.Kind));
        Assert.Contains(allay.Parts, part => part.Name == "left_wing" && part.Parent == "body");
        Assert.Equal("vex", vex.Name);
        Assert.Contains(vex.Parts, part => part.Name == "body_inner" && part.Parent == "body");
    }

    [Fact]
    public void TheParrotTypeIsRegisteredWithItsVariants()
    {
        var definition = EntityModelResolver.Resolve(ParrotModelDefinition.ParrotEntityTypeId);

        Assert.Equal("parrot", definition.Name);
        Assert.Equal(11, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "beak1" && part.Parent == "head");
        Assert.Equal("entity/parrot/parrot_grey", EntityModelResolver.ResolveVariantStrict(ParrotModelDefinition.ParrotEntityTypeId, "gray").TexturePath);
        Assert.Equal("entity/parrot/parrot_yellow_blue", EntityModelResolver.ResolveVariantStrict(ParrotModelDefinition.ParrotEntityTypeId, "yellow_blue").TexturePath);
    }

    [Fact]
    public void TheShulkerTypeHasLidBaseAndHeadWithColourVariants()
    {
        var definition = EntityModelResolver.Resolve(ShulkerModelDefinition.ShulkerEntityTypeId);

        Assert.Equal("shulker", definition.Name);
        Assert.Equal(3, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "lid");
        Assert.Equal(16, ShulkerModelDefinition.VariantKeys.Count);
        Assert.Equal("entity/shulker/shulker_light_blue", EntityModelResolver.ResolveVariantStrict(ShulkerModelDefinition.ShulkerEntityTypeId, "light_blue").TexturePath);
    }

    [Fact]
    public void TheCodAndSalmonAreRegisteredWithSalmonSizes()
    {
        var cod = EntityModelResolver.Resolve(CodModelDefinition.CodEntityTypeId);
        var salmon = EntityModelResolver.Resolve(SalmonModelDefinition.SalmonEntityTypeId);

        Assert.Equal("cod", cod.Name);
        Assert.Equal(7, cod.Parts.Count);
        Assert.Equal("salmon", salmon.Name);
        Assert.Equal(8, salmon.Parts.Count);
        Assert.Contains(salmon.Parts, part => part.Name == "back_fin" && part.Parent == "body_back");
        Assert.Equal(0.5f, EntityModelResolver.ResolveVariantStrict(SalmonModelDefinition.SalmonEntityTypeId, "small").Scale);
        Assert.Equal(1.5f, EntityModelResolver.ResolveVariantStrict(SalmonModelDefinition.SalmonEntityTypeId, "large").Scale);
    }

    [Fact]
    public void ThePufferfishHasThreePuffStateModels()
    {
        var small = EntityModelResolver.ResolveVariantStrict(PufferfishModelDefinition.PufferfishEntityTypeId, "small");
        var medium = EntityModelResolver.ResolveVariantStrict(PufferfishModelDefinition.PufferfishEntityTypeId, "medium");
        var large = EntityModelResolver.ResolveVariantStrict(PufferfishModelDefinition.PufferfishEntityTypeId, "large");

        Assert.Equal(6, small.Parts.Count);
        Assert.Equal(11, medium.Parts.Count);
        Assert.Equal(13, large.Parts.Count);
        Assert.All(new[] { small, medium, large }, model => Assert.Equal("entity/fish/pufferfish", model.TexturePath));
    }

    [Fact]
    public void TheNautilusTypeIsRegisteredWithShellAndMouth()
    {
        var definition = EntityModelResolver.Resolve(NautilusModelDefinition.NautilusEntityTypeId);

        Assert.Equal("nautilus", definition.Name);
        Assert.Equal(43, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "shell_mid" && part.Parent == "shell");
        Assert.Contains(definition.Parts, part => part.Name == "inner_mouth" && part.Parent == "body");
    }

    [Fact]
    public void CaveSpiderAndTraderLlamaReuseExistingGeometry()
    {
        var caveSpider = EntityModelResolver.Resolve(CaveSpiderModelDefinition.CaveSpiderEntityTypeId);
        var traderLlama = EntityModelResolver.Resolve(TraderLlamaModelDefinition.TraderLlamaEntityTypeId);

        Assert.Equal("cave_spider", caveSpider.Name);
        Assert.Equal(0.7f, caveSpider.Scale);
        Assert.Equal("entity/spider/cave_spider", caveSpider.TexturePath);
        Assert.Equal("entity/spider/spider_eyes", caveSpider.EmissiveTexturePath);
        Assert.Equal("trader_llama", traderLlama.Name);
        Assert.Equal("entity/llama/llama_brown", traderLlama.TexturePath);
    }

    [Fact]
    public void SpiderLegsUseTheBakedRotationSigns()
    {
        // Spider parts are authored without the global X mirror (mirrorX: false), so the baked mapping only flips Y.
        // For that mapping a reference zRot of -PI/4 stays -PI/4 in storage and the Y rotation is kept unchanged.
        var leg = Assert.Single(EntityModelResolver.Resolve(SpiderModelDefinition.SpiderEntityTypeId).Parts, part => part.Name == "right_hind_leg");

        Assert.Equal(MathF.PI / 4f, leg.RotationY, 4);
        Assert.Equal(MathF.PI / 4f, leg.RotationZ, 4);
    }

    [Fact]
    public void TheWitchTypeIsRegisteredWithItsHatChain()
    {
        var definition = EntityModelResolver.Resolve(WitchModelDefinition.WitchEntityTypeId);

        Assert.Equal("witch", definition.Name);
        Assert.Equal(15, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "mole" && part.Parent == "nose");
        Assert.Contains(definition.Parts, part => part.Name == "held_potion" && part.Parent == "nose" && part.Depth == 0f);
        Assert.Contains(definition.Parts, part => part.Name == "jacket" && part.Parent == "body" && Math.Abs(part.Grow - 0.5f) < 0.001f);
        Assert.Contains(definition.Parts, part => part.Name == "arms_left" && part.Parent == "arms");
        Assert.Contains(definition.Parts, part => part.Name == "hat2" && part.Parent == "hat");
        Assert.Contains(definition.Parts, part => part.Name == "hat4" && part.Parent == "hat3");
        Assert.Equal(0.9375f, definition.Scale);
    }

    [Fact]
    public void TheGiantUsesTheZombieGeometryAtScaleSix()
    {
        var definition = EntityModelResolver.Resolve(GiantModelDefinition.GiantEntityTypeId);

        Assert.Equal("giant", definition.Name);
        Assert.Equal(6f, definition.Scale);
        Assert.Equal("entity/zombie/zombie", definition.TexturePath);
        Assert.Equal(HumanoidMobModelDefinition.Zombie.Parts.Count, definition.Parts.Count);
    }

    [Fact]
    public void TheIllusionerReusesTheIllagerGeometry()
    {
        var definition = EntityModelResolver.Resolve(IllagerModelDefinition.IllusionerEntityTypeId);

        Assert.Equal("illusioner", definition.Name);
        Assert.Equal("entity/illager/illusioner", definition.TexturePath);
        Assert.Equal(IllagerModelDefinition.Evoker.Parts.Count, definition.Parts.Count);
    }

    [Fact]
    public void TheSnifferTypeHasSixLegsAndAGrownBody()
    {
        var definition = EntityModelResolver.Resolve(SnifferModelDefinition.SnifferEntityTypeId);

        Assert.Equal("sniffer", definition.Name);
        Assert.Equal(16, definition.Parts.Count);
        Assert.Equal(6, definition.Parts.Count(part => part.Name.EndsWith("_leg", StringComparison.Ordinal)));
        Assert.Contains(definition.Parts, part => part.Name == "body_outer" && part.Parent == "body" && Math.Abs(part.Grow - 0.5f) < 0.001f);
        Assert.Contains(definition.Parts, part => part.Name == "lower_beak" && part.Parent == "head");
    }

    [Fact]
    public void TheCreakingTypeHasItsGrasperLimbsAndEyes()
    {
        var definition = EntityModelResolver.Resolve(CreakingModelDefinition.CreakingEntityTypeId);

        Assert.Equal("creaking", definition.Name);
        Assert.Equal(18, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "head_plate_right" && part.Parent == "head");
        Assert.Contains(definition.Parts, part => part.Name == "left_hand" && part.Parent == "left_arm");
        Assert.Equal("entity/creaking/creaking_eyes", definition.EmissiveTexturePath);
    }

    [Fact]
    public void TheGhastTypeHasNineTentaclesAndTheShootingTexture()
    {
        var definition = EntityModelResolver.Resolve(GhastModelDefinition.GhastEntityTypeId);

        Assert.Equal("ghast", definition.Name);
        Assert.Equal(10, definition.Parts.Count);
        Assert.Equal(9, definition.Parts.Count(part => part.Name.StartsWith("tentacle", StringComparison.Ordinal)));
        Assert.Equal(13f, definition.Parts.Single(part => part.Name == "tentacle1").Height);
        Assert.Equal(4.5f, definition.Scale);
        Assert.NotNull(definition.TextureSelector);
    }

    [Fact]
    public void TheHappyGhastHasTentaclesUnderTheBody()
    {
        var definition = EntityModelResolver.Resolve(HappyGhastModelDefinition.HappyGhastEntityTypeId);

        Assert.Equal("happy_ghast", definition.Name);
        Assert.Equal(12, definition.Parts.Count);
        Assert.All(definition.Parts.Where(part => part.Name.StartsWith("tentacle", StringComparison.Ordinal)), part => Assert.Equal("body", part.Parent));
        Assert.Equal(8f, definition.Parts.Single(part => part.Name == "tentacle6").Height);
        Assert.Equal(4f, definition.Scale);
    }

    [Fact]
    public void ThePhantomTypeHasTwoSegmentWingsAndTail()
    {
        var definition = EntityModelResolver.Resolve(PhantomModelDefinition.PhantomEntityTypeId);

        Assert.Equal("phantom", definition.Name);
        Assert.Equal(8, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "left_wing_tip" && part.Parent == "left_wing_base");
        Assert.Contains(definition.Parts, part => part.Name == "tail_tip" && part.Parent == "tail_base");
        Assert.Equal("entity/phantom/phantom_eyes", definition.EmissiveTexturePath);
    }

    [Fact]
    public void TheRavagerTypeHasNeckHeadHornsAndFourLegs()
    {
        var definition = EntityModelResolver.Resolve(RavagerModelDefinition.RavagerEntityTypeId);

        Assert.Equal("ravager", definition.Name);
        Assert.Equal(12, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "mouth" && part.Parent == "head");
        Assert.Contains(definition.Parts, part => part.Name == "left_horn" && part.Parent == "head");
        Assert.Equal(4, definition.Parts.Count(part => part.Name.EndsWith("_leg", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheArmadilloSwapsItsBodyForTheShellCube()
    {
        var definition = EntityModelResolver.Resolve(ArmadilloModelDefinition.ArmadilloEntityTypeId);

        Assert.Equal("armadillo", definition.Name);
        Assert.Equal(14, definition.Parts.Count);
        Assert.NotNull(definition.Parts.Single(part => part.Name == "body").VisibleWhen);
        Assert.NotNull(definition.Parts.Single(part => part.Name == "cube").VisibleWhen);
        Assert.NotSame(definition.Parts.Single(part => part.Name == "body").VisibleWhen, definition.Parts.Single(part => part.Name == "cube").VisibleWhen);
    }

    [Fact]
    public void TheCopperGolemHasFourWeatheringTexturesAndEyes()
    {
        var definition = EntityModelResolver.Resolve(CopperGolemModelDefinition.CopperGolemEntityTypeId);

        Assert.Equal("copper_golem", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
        Assert.Equal("entity/copper_golem/copper_golem_eyes_weathered",
            EntityModelResolver.ResolveVariantStrict(CopperGolemModelDefinition.CopperGolemEntityTypeId, "weathered").EmissiveTexturePath);
        Assert.Equal("entity/copper_golem/copper_golem_oxidized",
            EntityModelResolver.ResolveVariantStrict(CopperGolemModelDefinition.CopperGolemEntityTypeId, "oxidized").TexturePath);
    }

    [Fact]
    public void TheBreezeHasSeparateBodyWindAndEyesLayers()
    {
        var definition = EntityModelResolver.Resolve(BreezeModelDefinition.BreezeEntityTypeId);

        Assert.Equal("breeze", definition.Name);
        Assert.Contains(definition.Parts, part => part.Name == "wind_bottom" && part.Kind == EntityGeometryLayerKind.Translucent);
        Assert.Contains(definition.Parts, part => part.Name == "eyes" && part.EmissiveOnly);
        Assert.Equal("entity/breeze/breeze_eyes", definition.EmissiveTexturePath);
        Assert.Contains("entity/breeze/breeze_wind", definition.TexturePaths);
    }

    [Fact]
    public void TheZombieNautilusWarmVariantAddsCorals()
    {
        var temperate = EntityModelResolver.ResolveVariantStrict(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, "temperate");
        var warm = EntityModelResolver.ResolveVariantStrict(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, "warm");

        Assert.Equal(43, temperate.Parts.Count);
        Assert.Equal(91, warm.Parts.Count);
        Assert.Contains(warm.Parts, part => part.Name == "corals" && part.Parent == "shell");
        Assert.Contains(warm.Parts, part => part.Name == "red_coral_first" && part.Parent == "red_coral");
        Assert.Equal("entity/nautilus/zombie_nautilus_coral", warm.TexturePath);
    }

    [Fact]
    public void TheWitherTypeHasThreeHeadsAndARibcage()
    {
        var definition = EntityModelResolver.Resolve(WitherBossModelDefinition.WitherEntityTypeId);

        Assert.Equal("wither", definition.Name);
        Assert.Equal(9, definition.Parts.Count);
        Assert.Equal(3, definition.Parts.Count(part => part.Name.EndsWith("head", StringComparison.Ordinal)));
        Assert.Equal(3, definition.Parts.Count(part => part.Name.StartsWith("rib_", StringComparison.Ordinal)));
        Assert.Equal(-0.20420352f, definition.Parts.Single(part => part.Name == "ribcage").RotationX);
    }

    [Fact]
    public void TheWardenTypeHasItsCoreBodyParts()
    {
        var definition = EntityModelResolver.Resolve(WardenModelDefinition.WardenEntityTypeId);

        Assert.Equal("warden", definition.Name);
        Assert.Equal(25, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "right_tendril" && part.Parent == "head");
        Assert.Contains(definition.Parts, part => part.Name == "left_ribcage" && part.Parent == "body");
        Assert.Contains(definition.Parts, part => part.Name == "left_leg" && part.Parent == "bone");
        Assert.Contains(definition.Parts, part => part.Name == "bioluminescent_head" && part.EmissiveTexturePath == "entity/warden/warden_bioluminescent_layer");
        Assert.Contains(definition.Parts, part => part.Name == "heart_body" && part.EmissiveOnly);
    }

    [Fact]
    public void TheEnderDragonTypeHasFiveNeckAndTwelveTailSegments()
    {
        var definition = EntityModelResolver.Resolve(EnderDragonModelDefinition.EnderDragonEntityTypeId);

        Assert.Equal("ender_dragon", definition.Name);
        Assert.Equal(66, definition.Parts.Count);
        Assert.Equal(5, definition.Parts.Count(part => part.Name.StartsWith("neck", StringComparison.Ordinal) && !part.Name.EndsWith("_scale", StringComparison.Ordinal)));
        Assert.Equal(12, definition.Parts.Count(part => part.Name.StartsWith("tail", StringComparison.Ordinal) && !part.Name.EndsWith("_scale", StringComparison.Ordinal)));
        Assert.Contains(definition.Parts, part => part.Name == "left_wing_tip_skin" && part.Parent == "left_wing");
    }

    [Fact]
    public void TheTropicalFishHasASmallAndPatternCopyForEachPattern()
    {
        var small = EntityModelResolver.ResolveVariantStrict(TropicalFishModelDefinition.TropicalFishEntityTypeId, "kob");
        var large = EntityModelResolver.ResolveVariantStrict(TropicalFishModelDefinition.TropicalFishEntityTypeId, "blockfish");

        Assert.Equal(10, small.Parts.Count);
        Assert.Equal(12, large.Parts.Count);
        Assert.Contains(small.Parts, part => part.Name == "pattern_body" && part.TexturePath == "entity/fish/tropical_a_pattern_1");
        Assert.Contains(large.Parts, part => part.Name == "pattern_body" && part.TexturePath == "entity/fish/tropical_b_pattern_4");
        Assert.NotNull(small.Parts.Single(part => part.Name == "pattern_body").TintSelector);
    }

    [Fact]
    public void BabyPigCowChickenResolveToTheirOwnGeometryAndTextures()
    {
        var pig = EntityModelResolver.Resolve(PigModelDefinition.PigEntityTypeId, true);
        var cow = EntityModelResolver.Resolve(CowModelDefinition.CowEntityTypeId, true);
        var chicken = EntityModelResolver.Resolve(ChickenModelDefinition.ChickenEntityTypeId, true);

        Assert.Equal("baby_pig", pig.Name);
        Assert.Equal(7, pig.Parts.Count);
        Assert.Equal("baby_cow", cow.Name);
        Assert.Equal(9, cow.Parts.Count);
        Assert.Equal("baby_chicken", chicken.Name);
        Assert.Equal(8, chicken.Parts.Count);
        Assert.Equal("entity/cow/cow_cold_baby",
            EntityModelResolver.ResolveVariantStrict(CowModelDefinition.CowEntityTypeId, "cold", true).TexturePath);
        Assert.Equal("baby_pig", EntityVariantTable.Builtin.Resolve(PigModelDefinition.PigEntityTypeId, 0, true).Name);
    }

    [Fact]
    public void BabyCatWolfFoxRabbitResolveToTheirOwnGeometry()
    {
        var cat = EntityModelResolver.Resolve(CatModelDefinition.CatEntityTypeId, true);
        var wolf = EntityModelResolver.Resolve(WolfModelDefinition.WolfEntityTypeId, true);
        var fox = EntityModelResolver.Resolve(FoxModelDefinition.FoxEntityTypeId, true);
        var rabbit = EntityModelResolver.Resolve(RabbitModelDefinition.RabbitEntityTypeId, true);

        Assert.Equal("baby_cat", cat.Name);
        Assert.Equal(22, cat.Parts.Count);
        Assert.Equal("baby_wolf", wolf.Name);
        Assert.Equal(44, wolf.Parts.Count(p => !p.Name.StartsWith("crackiness_")));
        Assert.Equal("baby_fox", fox.Name);
        Assert.Equal(10, fox.Parts.Count);
        Assert.Equal("baby_rabbit", rabbit.Name);
        Assert.Equal(17, rabbit.Parts.Count);
        Assert.Equal("entity/cat/cat_all_black_baby", EntityModelResolver.ResolveVariantStrict(CatModelDefinition.CatEntityTypeId, "all_black", true).TexturePath);
        Assert.Equal("entity/rabbit/rabbit_black_baby", EntityModelResolver.ResolveVariantStrict(RabbitModelDefinition.RabbitEntityTypeId, "black", true).TexturePath);
        Assert.Contains("entity/wolf/wolf_spotted_tame_baby", EntityModelResolver.ResolveVariantStrict(WolfModelDefinition.WolfEntityTypeId, "spotted", true).TexturePaths);
        Assert.Contains("entity/fox/fox_sleep_baby", EntityModelResolver.ResolveVariantStrict(FoxModelDefinition.FoxEntityTypeId, "red", true).TexturePaths);
    }

    [Fact]
    public void BabyHorseLlamaCamelDonkeyResolveToTheirOwnGeometry()
    {
        Assert.Equal(207, EntityModelResolver.Resolve(HorseModelDefinition.HorseEntityTypeId, true).Parts.Count);
        Assert.Equal(22, EntityModelResolver.Resolve(LlamaModelDefinition.LlamaEntityTypeId, true).Parts.Count);
        Assert.Equal(33, EntityModelResolver.Resolve(CamelModelDefinition.CamelEntityTypeId, true).Parts.Count);
        Assert.Equal(30, EntityModelResolver.Resolve(37, true).Parts.Count);
        Assert.Equal("entity/horse/horse_darkbrown_baby",
            EntityModelResolver.ResolveVariantStrict(HorseModelDefinition.HorseEntityTypeId, "dark_brown", true).TexturePath);
    }

    [Fact]
    public void BabyGoatPandaPolarBearTurtleResolveToTheirOwnGeometry()
    {
        Assert.Equal("baby_goat", EntityModelResolver.Resolve(63, true).Name);
        Assert.Equal(12, EntityModelResolver.Resolve(63, true).Parts.Count);
        Assert.Equal("baby_panda", EntityModelResolver.Resolve(97, true).Name);
        Assert.Equal(9, EntityModelResolver.Resolve(97, true).Parts.Count);
        Assert.Equal("baby_polar_bear", EntityModelResolver.Resolve(105, true).Name);
        Assert.Equal(9, EntityModelResolver.Resolve(105, true).Parts.Count);
        Assert.Equal("baby_turtle", EntityModelResolver.Resolve(141, true).Name);
        Assert.Equal(6, EntityModelResolver.Resolve(141, true).Parts.Count);
    }

    [Fact]
    public void BabySquidBeeDolphinAxolotlResolveToTheirOwnGeometry()
    {
        Assert.Equal(9, EntityModelResolver.Resolve(130, true).Parts.Count);
        Assert.Equal(9, EntityModelResolver.Resolve(11, true).Parts.Count);
        Assert.Equal(8, EntityModelResolver.Resolve(36, true).Parts.Count);
        Assert.Equal(13, EntityModelResolver.Resolve(7, true).Parts.Count);
        Assert.Equal("entity/axolotl/axolotl_blue_baby",
            EntityModelResolver.ResolveVariantStrict(7, "blue", true).TexturePath);
    }

    [Fact]
    public void BabyZombieDrownedZombieVillagerAndVillagerResolveToTheirOwnGeometry()
    {
        Assert.Equal(8, EntityModelResolver.Resolve(154, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal(15, EntityModelResolver.Resolve(39, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal(10, EntityModelResolver.Resolve(157, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal(12, EntityModelResolver.Resolve(143, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Contains("entity/zombie/drowned_outer_layer_baby", EntityModelResolver.Resolve(39, true).TexturePaths);
    }

    [Fact]
    public void BabyPiglinZombifiedPiglinHoglinZoglinResolveToTheirOwnGeometry()
    {
        Assert.Equal(12, EntityModelResolver.Resolve(102, true).Parts.Count(part => !part.Name.StartsWith("equipment_")));
        Assert.Equal(12, EntityModelResolver.Resolve(158, true).Parts.Count(part => !part.Name.StartsWith("equipment_")));
        Assert.Equal(11, EntityModelResolver.Resolve(65, true).Parts.Count);
        Assert.Equal(11, EntityModelResolver.Resolve(153, true).Parts.Count);
        Assert.Equal("entity/hoglin/zoglin_baby", EntityModelResolver.Resolve(153, true).TexturePath);
    }

    [Fact]
    public void BabyStriderHuskSniffletArmadilloResolveToTheirOwnGeometry()
    {
        Assert.Equal(6, EntityModelResolver.Resolve(132, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal(8, EntityModelResolver.Resolve(68, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal(16, EntityModelResolver.Resolve(122, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal(13, EntityModelResolver.Resolve(4, true).Parts.Count(p => !p.Name.StartsWith("equipment_")));
        Assert.Equal("entity/armadillo/armadillo_baby", EntityModelResolver.Resolve(4, true).TexturePath);
    }

    [Fact]
    public void BabyNautilusHappyGhastGlowSquidResolveToTheirOwnGeometry()
    {
        Assert.Equal(43, EntityModelResolver.Resolve(89, true).Parts.Count);
        Assert.Equal(13, EntityModelResolver.Resolve(59, true).Parts.Count);
        Assert.Equal(9, EntityModelResolver.Resolve(62, true).Parts.Count);
        Assert.Equal(0.95f, EntityModelResolver.Resolve(59, true).Scale);
        Assert.Equal("entity/squid/glow_squid_baby", EntityModelResolver.Resolve(62, true).TexturePath);
        Assert.Equal("entity/panda/panda_brown", EntityModelResolver.ResolveVariantStrict(97, "brown").TexturePath);
        Assert.Equal("entity/panda/panda_lazy_baby", EntityModelResolver.ResolveVariantStrict(97, "lazy", true).TexturePath);
    }

    [Fact]
    public void VillagerAndZombieVillagerHaveBiomeTypeLayers()
    {
        Assert.Contains("entity/villager/type/desert", EntityModelResolver.ResolveVariantStrict(143, "desert").TexturePaths);
        Assert.Contains("entity/zombie_villager/type/jungle", EntityModelResolver.ResolveVariantStrict(157, "jungle").TexturePaths);
        Assert.Contains("entity/villager/baby/taiga", EntityModelResolver.ResolveVariantStrict(143, "taiga", true).TexturePaths);
        Assert.Contains("entity/zombie_villager/baby/snow", EntityModelResolver.ResolveVariantStrict(157, "snow", true).TexturePaths);
    }

    [Fact]
    public void TheCamelTypeIdMatchesTheBaselineEntityTypeOrder()
    {
        // Derived from EntityTypes registration order (validated against the ids the other models already use).
        Assert.Equal(19, CamelModelDefinition.CamelEntityTypeId);
        Assert.Equal(111, RabbitModelDefinition.RabbitEntityTypeId);
    }
}









