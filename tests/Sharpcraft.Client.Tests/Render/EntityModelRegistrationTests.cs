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
        Assert.Equal(11, piglin.Parts.Count);
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
        Assert.Equal(9, definition.Parts.Count);
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
        Assert.Equal(14, definition.Parts.Count);
        Assert.Contains(definition.Parts, part => part.Name == "mole" && part.Parent == "nose");
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
        Assert.Equal(10, definition.Parts.Count);
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
    public void TheCamelTypeIdMatchesTheBaselineEntityTypeOrder()
    {
        // Derived from EntityTypes registration order (validated against the ids the other models already use).
        Assert.Equal(19, CamelModelDefinition.CamelEntityTypeId);
        Assert.Equal(111, RabbitModelDefinition.RabbitEntityTypeId);
    }
}
