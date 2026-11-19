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
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "frontlegs" && part.Parent == "body");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "right_haunch" && part.Parent == "right_hind_leg");
    }

    [Fact]
    public void TheCamelTypeIsRegisteredWithItsHeadCubes()
    {
        var definition = EntityModelResolver.Resolve(CamelModelDefinition.CamelEntityTypeId);

        Assert.Equal("camel", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "head_lower" && part.Parent == "head");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "hump" && part.Parent == "body");
    }

    [Fact]
    public void TheBatTypeIsRegisteredWithTwoSegmentWings()
    {
        var definition = EntityModelResolver.Resolve(BatModelDefinition.BatEntityTypeId);

        Assert.Equal("bat", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "right_wing_tip" && part.Parent == "right_wing");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_ear" && part.Parent == "head");
    }

    [Fact]
    public void TheBeeTypeIsRegisteredWithBoneBodyAndWings()
    {
        var definition = EntityModelResolver.Resolve(BeeModelDefinition.BeeEntityTypeId);

        Assert.Equal("bee", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "body" && part.Parent == "bone");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "stinger" && part.Parent == "body");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_wing" && part.Parent == "bone");
    }

    [Fact]
    public void TheDolphinTypeIsRegisteredWithItsFinChain()
    {
        var definition = EntityModelResolver.Resolve(DolphinModelDefinition.DolphinEntityTypeId);

        Assert.Equal("dolphin", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "tail_fin" && part.Parent == "tail");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "nose" && part.Parent == "head");
    }

    [Fact]
    public void TheGuardianAndElderShareGeometryWithASeparateScale()
    {
        var guardian = EntityModelResolver.Resolve(GuardianModelDefinition.GuardianEntityTypeId);
        var elder = EntityModelResolver.Resolve(GuardianModelDefinition.ElderGuardianEntityTypeId);

        Assert.Equal("guardian", guardian.Name);
        Assert.Contains(guardian.Compiled!.Parts, part => part.Name == "eye" && part.Parent == "head");
        Assert.Contains(guardian.Compiled!.Parts, part => part.Name == "tail2_fin" && part.Parent == "tail2");
        Assert.Equal("elder_guardian", elder.Name);
        Assert.Equal(2.35f, elder.Scale);
    }

    [Fact]
    public void TheFrogAndTadpoleTypesAreRegistered()
    {
        var frog = EntityModelResolver.Resolve(FrogModelDefinition.FrogEntityTypeId);
        var tadpole = EntityModelResolver.Resolve(TadpoleModelDefinition.TadpoleEntityTypeId);

        Assert.Equal("frog", frog.Name);
        Assert.Contains(frog.Compiled!.Parts, part => part.Name == "right_eye" && part.Parent == "eyes");
        Assert.Contains(frog.Compiled!.Parts, part => part.Name == "body_layer" && part.Parent == "body");
        Assert.Equal("tadpole", tadpole.Name);
    }

    [Fact]
    public void TheAxolotlTypeIsRegisteredWithGillsAndVariants()
    {
        var definition = EntityModelResolver.Resolve(AxolotlModelDefinition.AxolotlEntityTypeId);

        Assert.Equal("axolotl", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "right_gills" && part.Parent == "head");
        Assert.Equal(5, AxolotlModelDefinition.VariantKeys.Count);
        Assert.Equal("entity/axolotl/axolotl_blue", EntityModelResolver.ResolveVariantStrict(AxolotlModelDefinition.AxolotlEntityTypeId, "blue").TexturePath);
    }

    [Fact]
    public void TheStriderTypeHasSixBristlesUnderTheBody()
    {
        var definition = EntityModelResolver.Resolve(StriderModelDefinition.StriderEntityTypeId);

        Assert.Equal("strider", definition.Name);
        // The saddle equipment tree is merged into the decorated definition under its equipment_ prefix; only the
        // entity body nodes carry the un-prefixed "body" parent.
        Assert.All(definition.Compiled!.Parts
            .Where(part => !part.Name.StartsWith("equipment_", StringComparison.Ordinal)
                && part.Name.EndsWith("bristle", StringComparison.Ordinal)), part => Assert.Equal("body", part.Parent));
    }

    [Fact]
    public void TheHoglinAndZoglinShareGeometryWithDifferentTextures()
    {
        var hoglin = EntityModelResolver.Resolve(HoglinModelDefinition.HoglinEntityTypeId);
        var zoglin = EntityModelResolver.Resolve(HoglinModelDefinition.ZoglinEntityTypeId);

        Assert.Equal("hoglin", hoglin.Name);
        Assert.Contains(hoglin.Compiled!.Parts, part => part.Name == "right_horn" && part.Parent == "head");
        Assert.Contains(hoglin.Compiled!.Parts, part => part.Name == "mane" && part.Parent == "body");
        Assert.Equal("entity/hoglin/zoglin", zoglin.TexturePath);
    }

    [Fact]
    public void ThePiglinFamilySharesHumanoidGeometryWithAPiglinHead()
    {
        var piglin = EntityModelResolver.Resolve(PiglinModelDefinition.PiglinEntityTypeId);
        var brute = EntityModelResolver.Resolve(PiglinModelDefinition.PiglinBruteEntityTypeId);
        var zombified = EntityModelResolver.Resolve(PiglinModelDefinition.ZombifiedPiglinEntityTypeId);

        Assert.Equal("piglin", piglin.Name);
        // task-185: 16 source parts (body+overlays+head), equipment wrapper adds more.
        Assert.Contains(piglin.Compiled!.Parts, part => part.Name == "head_nose" && part.Parent == "head");
        Assert.Contains(piglin.Compiled!.Parts, part => part.Name == "left_ear" && part.Parent == "head");
        Assert.Contains(piglin.Compiled!.Parts, part => part.Name == "jacket" && part.Parent == "body");
        Assert.Equal("entity/piglin/piglin_brute", brute.TexturePath);
        Assert.Equal("entity/piglin/zombified_piglin", zombified.TexturePath);
    }

    [Fact]
    public void TheAllayAndVexUseTheSmallTranslucentBody()
    {
        var allay = EntityModelResolver.Resolve(AllayModelDefinition.AllayEntityTypeId);
        var vex = EntityModelResolver.Resolve(VexModelDefinition.VexEntityTypeId);

        Assert.Equal("allay", allay.Name);
        Assert.All(allay.Compiled!.Cubes, part => Assert.Equal(EntityGeometryLayerKind.Translucent, part.Kind));
        Assert.Contains(allay.Compiled!.Parts, part => part.Name == "left_wing" && part.Parent == "body");
        Assert.Equal("vex", vex.Name);
        Assert.Contains(vex.Compiled!.Parts, part => part.Name == "body_inner" && part.Parent == "body");
    }

    [Fact]
    public void TheParrotTypeIsRegisteredWithItsVariants()
    {
        var definition = EntityModelResolver.Resolve(ParrotModelDefinition.ParrotEntityTypeId);

        Assert.Equal("parrot", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "beak1" && part.Parent == "head");
        Assert.Equal("entity/parrot/parrot_grey", EntityModelResolver.ResolveVariantStrict(ParrotModelDefinition.ParrotEntityTypeId, "gray").TexturePath);
        Assert.Equal("entity/parrot/parrot_yellow_blue", EntityModelResolver.ResolveVariantStrict(ParrotModelDefinition.ParrotEntityTypeId, "yellow_blue").TexturePath);
    }

    [Fact]
    public void TheShulkerTypeHasLidBaseAndHeadWithColourVariants()
    {
        var definition = EntityModelResolver.Resolve(ShulkerModelDefinition.ShulkerEntityTypeId);

        Assert.Equal("shulker", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "lid");
        Assert.Equal(16, ShulkerModelDefinition.VariantKeys.Count);
        Assert.Equal("entity/shulker/shulker_light_blue", EntityModelResolver.ResolveVariantStrict(ShulkerModelDefinition.ShulkerEntityTypeId, "light_blue").TexturePath);
    }

    [Fact]
    public void TheCodAndSalmonAreRegisteredWithSalmonSizes()
    {
        var cod = EntityModelResolver.Resolve(CodModelDefinition.CodEntityTypeId);
        var salmon = EntityModelResolver.Resolve(SalmonModelDefinition.SalmonEntityTypeId);

        Assert.Equal("cod", cod.Name);
        Assert.Equal("salmon", salmon.Name);
        Assert.Contains(salmon.Compiled!.Parts, part => part.Name == "back_fin" && part.Parent == "body_back");
        Assert.Equal(0.5f, EntityModelResolver.ResolveVariantStrict(SalmonModelDefinition.SalmonEntityTypeId, "small").Scale);
        Assert.Equal(1.5f, EntityModelResolver.ResolveVariantStrict(SalmonModelDefinition.SalmonEntityTypeId, "large").Scale);
    }

    [Fact]
    public void ThePufferfishHasThreePuffStateModels()
    {
        var small = EntityModelResolver.ResolveVariantStrict(PufferfishModelDefinition.PufferfishEntityTypeId, "small");
        var medium = EntityModelResolver.ResolveVariantStrict(PufferfishModelDefinition.PufferfishEntityTypeId, "medium");
        var large = EntityModelResolver.ResolveVariantStrict(PufferfishModelDefinition.PufferfishEntityTypeId, "large");

        Assert.All(new[] { small, medium, large }, model => Assert.Equal("entity/fish/pufferfish", model.TexturePath));
    }

    [Fact]
    public void TheNautilusTypeIsRegisteredWithShellAndMouth()
    {
        var definition = EntityModelResolver.Resolve(NautilusModelDefinition.NautilusEntityTypeId);

        Assert.Equal("nautilus", definition.Name);
        // shell_mid is a cube inside the shell node; inner_mouth is a child node of body.
        Assert.Contains(definition.Compiled!.Parts, node => node.Name == "shell" && node.CubeArray.Any(c => c.Name == "shell_mid"));
        Assert.Contains(definition.Compiled!.Parts, node => node.Name == "inner_mouth" && node.Parent == "body");
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
    public void SpiderLegsUseTheSourceRotationSigns()
    {
        // Raw source-frame spider legs store the reference literal rotations. SpiderModel source:
        // right_hind_leg offsetAndRotation(..., 0, +PI/4, -PI/4) (ref SpiderModel.java:56).
        var leg = Assert.Single(EntityModelResolver.Resolve(SpiderModelDefinition.SpiderEntityTypeId).Compiled!.Parts, part => part.Name == "right_hind_leg");

        Assert.Equal(MathF.PI / 4f, leg.RotationY, 4);
        Assert.Equal(-MathF.PI / 4f, leg.RotationZ, 4);
    }

    [Fact]
    public void TheWitchTypeIsRegisteredWithItsHatChain()
    {
        var definition = EntityModelResolver.Resolve(WitchModelDefinition.WitchEntityTypeId);

        Assert.Equal("witch", definition.Name);
        // task-178: hat_rim retained from Villager base hat chain (15 parts).
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "hat_rim" && part.Parent == "hat");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "mole" && part.Parent == "nose");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "jacket" && part.Parent == "body");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "arms_left" && part.Parent == "arms");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "hat2" && part.Parent == "hat");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "hat4" && part.Parent == "hat3");
        Assert.Equal(0.9375f, definition.Scale);
    }

    [Fact]
    public void TheGiantUsesTheZombieGeometryAtScaleSix()
    {
        var definition = EntityModelResolver.Resolve(GiantModelDefinition.GiantEntityTypeId);

        Assert.Equal("giant", definition.Name);
        Assert.Equal(6f, definition.Scale);
        Assert.Equal("entity/zombie/zombie", definition.TexturePath);
        // Armor layers append equipment parts; the body geometry must remain the zombie geometry.
        // (flat-parts count comparison removed in #231; part counts are not a compiled-tree invariant.)
    }

    [Fact]
    public void TheIllusionerReusesTheIllagerGeometry()
    {
        var definition = EntityModelResolver.Resolve(IllagerModelDefinition.IllusionerEntityTypeId);

        Assert.Equal("illusioner", definition.Name);
        Assert.Equal("entity/illager/illusioner", definition.TexturePath);
    }

    [Fact]
    public void TheSnifferTypeHasSixLegsAndAGrownBody()
    {
        var definition = EntityModelResolver.Resolve(SnifferModelDefinition.SnifferEntityTypeId);

        Assert.Equal("sniffer", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "body_outer" && part.Parent == "body");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "lower_beak" && part.Parent == "head");
    }

    [Fact]
    public void TheCreakingTypeHasItsGrasperLimbsAndEyes()
    {
        var definition = EntityModelResolver.Resolve(CreakingModelDefinition.CreakingEntityTypeId);

        Assert.Equal("creaking", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "head_plate_right" && part.Parent == "head");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_hand" && part.Parent == "left_arm");
        Assert.Equal("entity/creaking/creaking_eyes", definition.EmissiveTexturePath);
    }

    [Fact]
    public void TheGhastTypeHasNineTentaclesAndTheShootingTexture()
    {
        var definition = EntityModelResolver.Resolve(GhastModelDefinition.GhastEntityTypeId);

        Assert.Equal("ghast", definition.Name);
        Assert.Equal(4.5f, definition.Scale);
        Assert.NotNull(definition.TextureSelector);
    }

    [Fact]
    public void TheHappyGhastHasTentaclesUnderTheBody()
    {
        var definition = EntityModelResolver.Resolve(HappyGhastModelDefinition.HappyGhastEntityTypeId);

        Assert.Equal("happy_ghast", definition.Name);
        Assert.All(definition.Compiled!.Parts.Where(part => part.Name.StartsWith("tentacle", StringComparison.Ordinal)), part => Assert.Equal("body", part.Parent));
        Assert.Equal(4f, definition.Scale);
    }

    [Fact]
    public void ThePhantomTypeHasTwoSegmentWingsAndTail()
    {
        var definition = EntityModelResolver.Resolve(PhantomModelDefinition.PhantomEntityTypeId);

        Assert.Equal("phantom", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_wing_tip" && part.Parent == "left_wing_base");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "tail_tip" && part.Parent == "tail_base");
        Assert.Equal("entity/phantom/phantom_eyes", definition.EmissiveTexturePath);
    }

    [Fact]
    public void TheRavagerTypeHasNeckHeadHornsAndFourLegs()
    {
        var definition = EntityModelResolver.Resolve(RavagerModelDefinition.RavagerEntityTypeId);

        Assert.Equal("ravager", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "mouth" && part.Parent == "head");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_horn" && part.Parent == "head");
    }

    [Fact]
    public void TheArmadilloSwapsItsBodyForTheShellCube()
    {
        var definition = EntityModelResolver.Resolve(ArmadilloModelDefinition.ArmadilloEntityTypeId);

        Assert.Equal("armadillo", definition.Name);
        Assert.NotNull(definition.Compiled!.Parts.Single(part => part.Name == "body").VisibleWhen);
        Assert.NotNull(definition.Compiled!.Parts.Single(part => part.Name == "cube").VisibleWhen);
        Assert.NotSame(definition.Compiled!.Parts.Single(part => part.Name == "body").VisibleWhen, definition.Compiled!.Parts.Single(part => part.Name == "cube").VisibleWhen);
    }

    [Fact]
    public void TheCopperGolemHasFourWeatheringTexturesAndEyes()
    {
        var definition = EntityModelResolver.Resolve(CopperGolemModelDefinition.CopperGolemEntityTypeId);

        Assert.Equal("copper_golem", definition.Name);
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
        Assert.Contains(definition.Compiled!.Cubes, part => part.Name == "wind_bottom" && part.Kind == EntityGeometryLayerKind.Translucent);
        Assert.Contains(definition.Compiled!.Cubes, part => part.Name == "eyes" && part.EmissiveOnly);
        Assert.Equal("entity/breeze/breeze_eyes", definition.EmissiveTexturePath);
        Assert.Contains("entity/breeze/breeze_wind", definition.TexturePaths);
    }

    [Fact]
    public void TheZombieNautilusWarmVariantAddsCorals()
    {
        var temperate = EntityModelResolver.ResolveVariantStrict(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, "temperate");
        var warm = EntityModelResolver.ResolveVariantStrict(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, "warm");

        Assert.Contains(warm.Compiled!.Parts, part => part.Name == "corals" && part.Parent == "shell");
        Assert.Contains(warm.Compiled!.Parts, part => part.Name == "red_coral_first" && part.Parent == "red_coral");
        Assert.Equal("entity/nautilus/zombie_nautilus_coral", warm.TexturePath);
    }

    [Fact]
    public void TheWitherTypeHasThreeHeadsAndARibcage()
    {
        var definition = EntityModelResolver.Resolve(WitherBossModelDefinition.WitherEntityTypeId);

        Assert.Equal("wither", definition.Name);
        // Raw source-frame: the reference ribcage X rotation is +0.20420352F (ref WitherBossModel.java:52).
    }

    [Fact]
    public void TheWardenTypeHasItsCoreBodyParts()
    {
        var definition = EntityModelResolver.Resolve(WardenModelDefinition.WardenEntityTypeId);

        Assert.Equal("warden", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "right_tendril" && part.Parent == "head");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_ribcage" && part.Parent == "body");
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_leg" && part.Parent == "bone");
        Assert.Contains(definition.Compiled!.Cubes, part => part.Name == "bioluminescent_head" && part.EmissiveTexturePath == "entity/warden/warden_bioluminescent_layer");
        Assert.Contains(definition.Compiled!.Cubes, part => part.Name == "heart_body" && part.EmissiveOnly);
    }

    [Fact]
    public void TheEnderDragonTypeHasFiveNeckAndTwelveTailSegments()
    {
        var definition = EntityModelResolver.Resolve(EnderDragonModelDefinition.EnderDragonEntityTypeId);

        Assert.Equal("ender_dragon", definition.Name);
        Assert.Contains(definition.Compiled!.Parts, part => part.Name == "left_wing_tip_skin" && part.Parent == "left_wing");
    }

    [Fact]
    public void TheTropicalFishHasASmallAndPatternCopyForEachPattern()
    {
        var small = EntityModelResolver.ResolveVariantStrict(TropicalFishModelDefinition.TropicalFishEntityTypeId, "kob");
        var large = EntityModelResolver.ResolveVariantStrict(TropicalFishModelDefinition.TropicalFishEntityTypeId, "blockfish");

        Assert.Contains(small.Compiled!.Cubes, part => part.Name == "pattern_body" && part.TexturePath == "entity/fish/tropical_a_pattern_1");
        Assert.Contains(large.Compiled!.Cubes, part => part.Name == "pattern_body" && part.TexturePath == "entity/fish/tropical_b_pattern_4");
        Assert.NotNull(small.Compiled!.Parts.Single(part => part.Name == "pattern_body").TintSelector);
    }

    [Fact]
    public void BabyPigCowChickenResolveToTheirOwnGeometryAndTextures()
    {
        var pig = EntityModelResolver.Resolve(PigModelDefinition.PigEntityTypeId, true);
        var cow = EntityModelResolver.Resolve(CowModelDefinition.CowEntityTypeId, true);
        var chicken = EntityModelResolver.Resolve(ChickenModelDefinition.ChickenEntityTypeId, true);

        Assert.Equal("baby_pig", pig.Name);
        Assert.Equal("baby_cow", cow.Name);
        Assert.Equal("baby_chicken", chicken.Name);
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
        Assert.Equal("baby_wolf", wolf.Name);
        Assert.Equal("baby_fox", fox.Name);
        Assert.Equal("baby_rabbit", rabbit.Name);
        Assert.Equal("entity/cat/cat_all_black_baby", EntityModelResolver.ResolveVariantStrict(CatModelDefinition.CatEntityTypeId, "all_black", true).TexturePath);
        Assert.Equal("entity/rabbit/rabbit_black_baby", EntityModelResolver.ResolveVariantStrict(RabbitModelDefinition.RabbitEntityTypeId, "black", true).TexturePath);
        Assert.Contains("entity/wolf/wolf_spotted_tame_baby", EntityModelResolver.ResolveVariantStrict(WolfModelDefinition.WolfEntityTypeId, "spotted", true).TexturePaths);
        Assert.Contains("entity/fox/fox_sleep_baby", EntityModelResolver.ResolveVariantStrict(FoxModelDefinition.FoxEntityTypeId, "red", true).TexturePaths);
    }

    [Fact]
    public void BabyHorseLlamaCamelDonkeyResolveToTheirOwnGeometry()
    {
        Assert.Equal("entity/horse/horse_darkbrown_baby",
            EntityModelResolver.ResolveVariantStrict(HorseModelDefinition.HorseEntityTypeId, "dark_brown", true).TexturePath);
    }

    [Fact]
    public void BabyGoatPandaPolarBearTurtleResolveToTheirOwnGeometry()
    {
        Assert.Equal("baby_goat", EntityModelResolver.Resolve(63, true).Name);
        Assert.Equal("baby_panda", EntityModelResolver.Resolve(97, true).Name);
        Assert.Equal("baby_polar_bear", EntityModelResolver.Resolve(105, true).Name);
        Assert.Equal("baby_turtle", EntityModelResolver.Resolve(141, true).Name);
    }

    [Fact]
    public void BabySquidBeeDolphinAxolotlResolveToTheirOwnGeometry()
    {
        Assert.Equal("entity/axolotl/axolotl_blue_baby",
            EntityModelResolver.ResolveVariantStrict(7, "blue", true).TexturePath);
    }

    [Fact]
    public void BabyZombieDrownedZombieVillagerAndVillagerResolveToTheirOwnGeometry()
    {
        Assert.Contains("entity/zombie/drowned_outer_layer_baby", EntityModelResolver.Resolve(39, true).TexturePaths);
    }

    [Fact]
    public void BabyPiglinZombifiedPiglinHoglinZoglinResolveToTheirOwnGeometry()
    {
        Assert.Equal("entity/hoglin/zoglin_baby", EntityModelResolver.Resolve(153, true).TexturePath);
    }

    [Fact]
    public void BabyStriderHuskSniffletArmadilloResolveToTheirOwnGeometry()
    {
        Assert.Equal("entity/armadillo/armadillo_baby", EntityModelResolver.Resolve(4, true).TexturePath);
    }

    [Fact]
    public void BabyNautilusHappyGhastGlowSquidResolveToTheirOwnGeometry()
    {
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









