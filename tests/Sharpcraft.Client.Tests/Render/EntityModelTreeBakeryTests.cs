// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-713: the central tree bakery preserves reference part identity (including zero-cube groups) and stores each part's
/// cubes as one node, unlike the legacy one-cube-per-EntityModelPart path.
/// </summary>
public sealed class EntityModelTreeBakeryTests
{
    [Fact]
    public void BreezeKeepsMultiCubePartsAsOneNode()
    {
        var compiled = VanillaMeshBakery.Build(BreezeModelDefinition.Mesh, 32, 32);
        Assert.Equal(11, compiled.Parts.Count);
        Assert.Equal(14, compiled.Cubes.Count);

        var head = Assert.IsType<EntityModelPartNode>(compiled.Find("head"));
        Assert.Equal(2, head.Cubes.Count);
        Assert.Contains(compiled.Find("eyes"), head.Children);

        var eyes = Assert.IsType<EntityModelPartNode>(compiled.Find("eyes"));
        Assert.Equal(2, eyes.Cubes.Count);

        var windMid = Assert.IsType<EntityModelPartNode>(compiled.Find("wind_mid"));
        Assert.Equal(3, windMid.Cubes.Count);

        var windTop = Assert.IsType<EntityModelPartNode>(compiled.Find("wind_top"));
        Assert.Equal(3, windTop.Cubes.Count);
    }

    [Fact]
    public void ArmadilloKeepsZeroCubeGroupNodes()
    {
        var compiled = VanillaMeshBakery.Build(ArmadilloModelDefinition.Mesh, 64, 64);
        Assert.Equal(14, compiled.Parts.Count);

        var head = Assert.IsType<EntityModelPartNode>(compiled.Find("head"));
        Assert.Empty(head.Cubes);
        Assert.Equal("body", head.Parent);
        Assert.Contains(compiled.Find("head_cube"), head.Children);

        var body = Assert.IsType<EntityModelPartNode>(compiled.Find("body"));
        Assert.Single(body.Cubes);
    }

    [Fact]
    public void FoxAndRabbitDefinitionsExposeCompiledTrees()
    {
        Assert.NotNull(FoxModelDefinition.Definition.Compiled);
        Assert.NotNull(RabbitModelDefinition.Definition.Compiled);

        var fox = FoxModelDefinition.Definition.Compiled!;
        Assert.Equal(10, fox.Parts.Count);
        Assert.Equal("body", Assert.IsType<EntityModelPartNode>(fox.Find("tail")).Parent);

        var rabbit = RabbitModelDefinition.Definition.Compiled!;
        Assert.Equal(13, rabbit.Parts.Count);
        Assert.Empty(Assert.IsType<EntityModelPartNode>(rabbit.Find("frontlegs")).Cubes);
        Assert.Empty(Assert.IsType<EntityModelPartNode>(rabbit.Find("backlegs")).Cubes);
        Assert.Contains(rabbit.Find("right_front_leg"), rabbit.Find("frontlegs")!.Children);
    }

    [Fact]
    public void CamelEquipmentAddsTreeSaddleParts()
    {
        Assert.NotNull(CamelModelDefinition.Definition.Compiled);
        var decorated = EntityModelResolver.Resolve(CamelModelDefinition.CamelEntityTypeId);
        Assert.NotNull(decorated.Compiled);
        var saddle = decorated.Compiled!.Find("equipment_CamelSaddle_7_saddle");
        Assert.NotNull(saddle);
        Assert.Equal(3, saddle.Cubes.Count);
        Assert.NotNull(decorated.Compiled.Find("equipment_CamelSaddle_7_bridle"));
        Assert.NotNull(decorated.Compiled.Find("equipment_CamelSaddle_7_reins"));
    }

    [Fact]
    public void HorseExposesCompiledTreeAndMarkingLayers()
    {
        var horse = HorseModelDefinition.Definition.Compiled;
        Assert.NotNull(horse);
        Assert.Equal(60, horse!.Parts.Count);
        Assert.NotNull(horse.Find("marking_white_head_parts"));
        Assert.NotNull(horse.Find("marking_blackdots_tail"));
        Assert.Equal(EntityGeometryLayerKind.Translucent, horse.Find("marking_white_tail")!.Cubes[0].Kind);

        var baseState = new EntityRenderState(1, HorseModelDefinition.HorseEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var markedState = baseState with { Markings = 1 };
        var mesher = new EntityModelMesher();
        var baseVertexFloats = mesher.Build([baseState], new SpriteRect(0, 0, 64, 64, 1), 64, 64).VertexFloats;
        var markedVertexFloats = mesher.Build([markedState], new SpriteRect(0, 0, 64, 64, 1), 64, 64).VertexFloats;
        Assert.True(markedVertexFloats > baseVertexFloats);
    }

    [Fact]
    public void HorseEquipmentAddsIndependentArmorAndSaddleTrees()
    {
        var decorated = EntityModelResolver.Resolve(HorseModelDefinition.HorseEntityTypeId);
        Assert.NotNull(decorated.Compiled);
        var armorBody = decorated.Compiled!.Find("equipment_HorseBody_6_body");
        Assert.NotNull(armorBody);
        Assert.Equal(0.05f, armorBody.Cubes[0].Grow, 3);
        var armorHead = decorated.Compiled.Find("equipment_HorseBody_6_head");
        Assert.NotNull(armorHead);
        Assert.Equal(0.1f, armorHead.Cubes[0].Grow, 3);

        var saddle = decorated.Compiled.Find("equipment_HorseSaddle_7_saddle");
        Assert.NotNull(saddle);
        Assert.Single(saddle.Cubes);
        var leftLine = decorated.Compiled.Find("equipment_HorseSaddle_7_left_saddle_line");
        Assert.NotNull(leftLine);
        var noSaddle = new EntityRenderState(1, HorseModelDefinition.HorseEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var withSaddle = noSaddle with
        {
            Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, -1, 949),
            IsVehicle = true
        };
        Assert.False(leftLine.VisibleWhen!(noSaddle));
        Assert.True(leftLine.VisibleWhen(withSaddle));
    }

    [Fact]
    public void DonkeyAndMuleUseTheDonkeyCompiledTree()
    {
        Assert.NotNull(HorseModelDefinition.Donkey.Compiled);
        Assert.Equal(14, HorseModelDefinition.Donkey.Compiled!.Parts.Count);
        Assert.NotNull(HorseModelDefinition.Donkey.Compiled.Find("left_chest"));
        Assert.NotNull(HorseModelDefinition.Donkey.Compiled.Find("right_chest"));
        Assert.Contains(HorseModelDefinition.Donkey.Compiled.Find("head")!.Children, child => child.Name == "left_ear");

        var mule = EntityModelResolver.Resolve(HorseModelDefinition.MuleEntityTypeId);
        Assert.NotNull(mule.Compiled);
        var saddle = mule.Compiled!.Find("equipment_MuleSaddle_7_saddle");
        Assert.NotNull(saddle);
    }

    [Fact]
    public void LlamaKeepsTheReferenceMultiCubeHeadAndDecorTree()
    {
        var llama = LlamaModelDefinition.Definition.Compiled;
        Assert.NotNull(llama);
        Assert.Equal(8, llama!.Parts.Count);
        var head = llama.Find("head");
        Assert.NotNull(head);
        Assert.Equal(4, head.Cubes.Count);
        Assert.Null(llama.Find("neck"));

        var decorated = EntityModelResolver.Resolve(LlamaModelDefinition.LlamaEntityTypeId);
        Assert.NotNull(decorated.Compiled);
        var decorHead = decorated.Compiled!.Find("equipment_LlamaBody_6_head");
        Assert.NotNull(decorHead);
        Assert.Equal(4, decorHead.Cubes.Count);
        Assert.Equal(0.5f, decorHead.Cubes[0].Grow, 3);

        var trader = EntityModelResolver.Resolve(TraderLlamaModelDefinition.TraderLlamaEntityTypeId);
        Assert.NotNull(trader.Compiled);
        var traderDecor = trader.Compiled!.Find("equipment_LlamaBody_6_head");
        Assert.NotNull(traderDecor);
        var plainState = new EntityRenderState(1, TraderLlamaModelDefinition.TraderLlamaEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        Assert.True(traderDecor.VisibleWhen!(plainState));
        Assert.Equal("entity/equipment/llama_body/trader_llama", traderDecor.Cubes[0].TextureSelector!(plainState));
    }

    [Fact]
    public void NautilusAndHappyGhastExposeCompiledEquipmentTrees()
    {
        var nautilus = NautilusModelDefinition.Definition.Compiled;
        Assert.NotNull(nautilus);
        Assert.Equal(3, nautilus!.Find("shell")!.Cubes.Count);
        Assert.Equal(2, nautilus.Find("body")!.Cubes.Count);

        var decoratedNautilus = EntityModelResolver.Resolve(NautilusModelDefinition.NautilusEntityTypeId);
        Assert.NotNull(decoratedNautilus.Compiled);
        var armorShell = decoratedNautilus.Compiled!.Find("equipment_NautilusBody_6_shell");
        Assert.NotNull(armorShell);
        Assert.Equal(3, armorShell.Cubes.Count);
        Assert.Equal(0.01f, armorShell.Cubes[0].Grow, 3);
        Assert.NotNull(decoratedNautilus.Compiled.Find("equipment_NautilusSaddle_7_shell"));

        var happy = HappyGhastModelDefinition.Definition.Compiled;
        Assert.NotNull(happy);
        Assert.Equal(10, happy!.Parts.Count);
        var decoratedHappy = EntityModelResolver.Resolve(HappyGhastModelDefinition.HappyGhastEntityTypeId);
        Assert.NotNull(decoratedHappy.Compiled);
        Assert.Single(decoratedHappy.Compiled!.Find("equipment_HappyGhastBody_6_harness")!.Cubes);
        Assert.NotNull(decoratedHappy.Compiled.Find("equipment_HappyGhastBody_6_goggles"));
    }

    [Fact]
    public void HumanoidMobsExposeCompiledTreesAndArmorSlotLayers()
    {
        Assert.NotNull(HumanoidMobModelDefinition.Zombie.Compiled);
        Assert.NotNull(HumanoidMobModelDefinition.Drowned.Compiled);
        Assert.NotNull(HumanoidMobModelDefinition.Husk.Compiled);
        Assert.NotNull(HumanoidMobModelDefinition.Skeleton.Compiled);
        Assert.NotNull(HumanoidMobModelDefinition.Stray.Compiled);
        Assert.NotNull(HumanoidMobModelDefinition.Bogged.Compiled);
        Assert.NotNull(HumanoidMobModelDefinition.WitherSkeleton.Compiled);

        var zombie = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);
        Assert.NotNull(zombie.Compiled);
        var head = zombie.Compiled!.Find("equipment_HumanoidArmor_5_head");
        Assert.NotNull(head);
        Assert.Single(head.Cubes);
        Assert.Equal(1.0f, head.Cubes[0].Grow, 3);
        var hat = zombie.Compiled.Find("equipment_HumanoidArmor_5_hat");
        Assert.NotNull(hat);
        Assert.Equal(1.5f, hat.Cubes[0].Grow, 3);

        var chest = zombie.Compiled.Find("equipment_HumanoidArmor_4_body");
        Assert.NotNull(chest);
        Assert.Equal(1.0f, chest.Cubes[0].Grow, 3);
        var leggings = zombie.Compiled.Find("equipment_HumanoidLeggings_3_body");
        Assert.NotNull(leggings);
        Assert.Equal(0.5f, leggings.Cubes[0].Grow, 3);
        var leggingsLeg = zombie.Compiled.Find("equipment_HumanoidLeggings_3_left_leg");
        Assert.NotNull(leggingsLeg);
        Assert.Equal(0.4f, leggingsLeg.Cubes[0].Grow, 3);
        var boots = zombie.Compiled.Find("equipment_HumanoidArmor_2_left_leg");
        Assert.NotNull(boots);
        Assert.Equal(0.9f, boots.Cubes[0].Grow, 3);
    }

    [Fact]
    public void ZombieNautilusWarmVariantAttachesCoralUnderShell()
    {
        var warm = ZombieNautilusModelDefinition.Variants["warm"];
        Assert.NotNull(warm.Compiled);
        var corals = warm.Compiled!.Find("corals");
        Assert.NotNull(corals);
        var plain = new EntityRenderState(1, ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var armored = plain with { Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, 1, -1) };
        Assert.True(corals.VisibleWhen!(plain));
        Assert.False(corals.VisibleWhen(armored));
    }

    [Fact]
    public void FourthAdultBatchExposesCompiledTrees()
    {
        Assert.NotNull(GuardianModelDefinition.Definition.Compiled);
        Assert.NotNull(GuardianModelDefinition.Elder.Compiled);
        Assert.NotNull(EnderDragonModelDefinition.Definition.Compiled);
        Assert.NotNull(WitherBossModelDefinition.Definition.Compiled);
        Assert.NotNull(ZombieNautilusModelDefinition.Definition.Compiled);

        Assert.Equal(23, GuardianModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(9, WitherBossModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(66, EnderDragonModelDefinition.Definition.Compiled!.Parts.Count);
    }

    [Fact]
    public void ThirdAdultBatchExposesCompiledTrees()
    {
        Assert.NotNull(BlazeModelDefinition.Definition.Compiled);
        Assert.NotNull(HoglinModelDefinition.Definition.Compiled);
        Assert.NotNull(HoglinModelDefinition.Zoglin.Compiled);
        Assert.NotNull(SlimeModelDefinition.Definition.Compiled);
        Assert.NotNull(StriderModelDefinition.Definition.Compiled);
        Assert.NotNull(SnifferModelDefinition.Definition.Compiled);
        Assert.NotNull(GiantModelDefinition.Definition.Compiled);

        Assert.Equal(13, BlazeModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(11, HoglinModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(5, SlimeModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(9, StriderModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(16, SnifferModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(6, GiantModelDefinition.Definition.Compiled!.Parts.Count);
    }

    [Fact]
    public void SecondAdultBatchExposesCompiledTrees()
    {
        Assert.NotNull(CodModelDefinition.Definition.Compiled);
        Assert.NotNull(DolphinModelDefinition.Definition.Compiled);
        Assert.NotNull(GhastModelDefinition.Definition.Compiled);
        Assert.NotNull(MagmaCubeModelDefinition.Definition.Compiled);
        Assert.NotNull(TurtleModelDefinition.Definition.Compiled);
        Assert.NotNull(PufferfishModelDefinition.Definition.Compiled);
        Assert.NotNull(PufferfishModelDefinition.Variants["medium"].Compiled);
        Assert.NotNull(PufferfishModelDefinition.Variants["large"].Compiled);

        Assert.Equal(7, CodModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(8, DolphinModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(10, GhastModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(9, MagmaCubeModelDefinition.Definition.Compiled!.Parts.Count);
        Assert.Equal(8, TurtleModelDefinition.Definition.Compiled!.Parts.Count);
    }

    [Fact]
    public void RemainingAdultModelsExposeCompiledTreesAndVariantLayers()
    {
        Assert.NotNull(AxolotlModelDefinition.Definition.Compiled);
        Assert.NotNull(AxolotlModelDefinition.Variants["blue"].Compiled);
        Assert.NotNull(ParrotModelDefinition.Definition.Compiled);
        Assert.NotNull(ParrotModelDefinition.Variants["gray"].Compiled);
        Assert.NotNull(SalmonModelDefinition.Definition.Compiled);
        Assert.NotNull(SalmonModelDefinition.Variants["large"].Compiled);
        Assert.NotNull(TropicalFishModelDefinition.Definition.Compiled);
        Assert.NotNull(TropicalFishModelDefinition.Variants["clayfish"].Compiled);
        Assert.NotNull(VillagerModelDefinition.Definition.Compiled);
        Assert.NotNull(VillagerModelDefinition.Variants["plains"].Compiled);
        Assert.NotNull(ZombieVillagerModelDefinition.Definition.Compiled);

        var pattern = TropicalFishModelDefinition.Definition.Compiled!.Find("pattern_body");
        Assert.NotNull(pattern);
        Assert.NotNull(pattern.Cubes[0].TextureSelector);
        Assert.NotNull(VillagerModelDefinition.Variants["plains"].Compiled!.Find("copylayer_head"));
    }

    [Fact]
    public void PiglinUsesPlayerArmorGeometryWithEmptyEars()
    {
        Assert.NotNull(PiglinModelDefinition.Definition.Compiled);
        var piglin = EntityModelResolver.Resolve(PiglinModelDefinition.PiglinEntityTypeId);
        Assert.NotNull(piglin.Compiled);
        var head = piglin.Compiled!.Find("equipment_HumanoidArmor_5_head");
        Assert.NotNull(head);
        Assert.NotNull(piglin.Compiled.Find("equipment_HumanoidArmor_5_hat"));
        Assert.NotNull(piglin.Compiled.Find("equipment_HumanoidArmor_5_left_ear"));
        Assert.NotNull(piglin.Compiled.Find("equipment_HumanoidArmor_5_right_ear"));
        Assert.NotNull(piglin.Compiled.Find("equipment_HumanoidArmor_4_body"));
    }

    [Fact]
    public void BabyPiglinUsesUniformSevenTenthsArmorTree()
    {
        var baby = EntityModelResolver.Resolve(PiglinModelDefinition.PiglinEntityTypeId, baby: true);
        Assert.NotNull(baby.Compiled);
        var head = baby.Compiled!.Find("equipment_HumanoidArmorBaby_5_head");
        Assert.NotNull(head);
        var headGrow = head.Cubes[0].GrowXyz;
        Assert.NotNull(headGrow);
        Assert.Equal(0.7f, headGrow!.Value.X, 3);
        Assert.Equal(0.7f, headGrow.Value.Y, 3);
        Assert.Equal(0.7f, headGrow.Value.Z, 3);
        Assert.NotNull(baby.Compiled.Find("equipment_HumanoidArmorBaby_5_left_ear"));
        var waist = baby.Compiled.Find("equipment_HumanoidArmorBaby_3_waist");
        Assert.NotNull(waist);
        Assert.Equal(0.6f, waist.Cubes[0].GrowXyz!.Value.X, 3);
    }

    [Fact]
    public void BabyZombieUsesDedicatedBabyArmorTree()
    {
        var baby = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId, baby: true);
        Assert.NotNull(baby.Compiled);
        var head = baby.Compiled!.Find("equipment_HumanoidArmorBaby_5_head");
        Assert.NotNull(head);
        var headGrow = head.Cubes[0].GrowXyz;
        Assert.NotNull(headGrow);
        Assert.Equal(-0.1f, headGrow!.Value.X, 3);
        Assert.Equal(0.5f, headGrow.Value.Y, 3);
        Assert.Equal(0.3f, headGrow.Value.Z, 3);

        var waist = baby.Compiled.Find("equipment_HumanoidArmorBaby_3_waist");
        Assert.NotNull(waist);
        var waistGrow = waist.Cubes[0].GrowXyz;
        Assert.NotNull(waistGrow);
        Assert.Equal(-0.2f, waistGrow!.Value.X, 3);
        Assert.Equal(0.2f, waistGrow.Value.Y, 3);
        Assert.Equal(0.2f, waistGrow.Value.Z, 3);
        var foot = baby.Compiled.Find("equipment_HumanoidArmorBaby_2_right_foot");
        Assert.NotNull(foot);
        Assert.NotNull(baby.Compiled.Find("equipment_HumanoidArmorBaby_2_left_leg"));
    }

    [Fact]
    public void WardenExposesEmissiveTreeLayers()
    {
        var warden = WardenModelDefinition.Definition.Compiled;
        Assert.NotNull(warden);
        Assert.True(warden!.Cubes.Count > 10);
        Assert.NotNull(warden.Find("bioluminescent_head"));
        Assert.NotNull(warden.Find("spots_body"));
        Assert.NotNull(warden.Find("heart_body"));
        Assert.NotNull(warden.Find("tendril_glow_left_tendril"));

        var geometry = new EntityModelMesher().Build(
            [new EntityRenderState(1, WardenModelDefinition.WardenEntityTypeId, 0f, 0f, 0f, 0f, 0f)],
            new SpriteRect(0, 0, 128, 128, 1), 128, 128);
        Assert.True(geometry.VertexFloats > 0);
    }

    [Fact]
    public void CatAndWolfExposeTreeCollars()
    {
        var cat = CatModelDefinition.Definition.Compiled;
        Assert.NotNull(cat);
        Assert.Equal(22, cat!.Parts.Count);
        Assert.NotNull(cat.Find("collar_head_main"));

        var wolf = WolfModelDefinition.Definition.Compiled;
        Assert.NotNull(wolf);
        Assert.Equal(26, wolf!.Parts.Count);
        Assert.NotNull(wolf.Find("collar_head"));

        var decoratedWolf = EntityModelResolver.Resolve(WolfModelDefinition.WolfEntityTypeId);
        Assert.NotNull(decoratedWolf.Compiled);
        var wolfArmor = decoratedWolf.Compiled!.Find("equipment_WolfBody_6_body");
        Assert.NotNull(wolfArmor);
        Assert.Equal(0.2f, wolfArmor.Cubes[0].Grow, 3);
        var crackiness = decoratedWolf.Compiled.Find("crackiness_body");
        Assert.NotNull(crackiness);
        Assert.Equal(0.2f, crackiness.Cubes[0].Grow, 3);

        var catWithout = new EntityModelMesher().Build(
            [new EntityRenderState(1, CatModelDefinition.CatEntityTypeId, 0f, 0f, 0f, 0f, 0f)],
            new SpriteRect(0, 0, 64, 32, 1), 64, 32);
        var catWith = new EntityModelMesher().Build(
            [new EntityRenderState(1, CatModelDefinition.CatEntityTypeId, 0f, 0f, 0f, 0f, 0f, HasCollar: true)],
            new SpriteRect(0, 0, 64, 32, 1), 64, 32);
        Assert.True(catWith.VertexFloats > catWithout.VertexFloats);
    }

    [Fact]
    public void FarmModelsExposeCompiledTrees()
    {
        Assert.NotNull(PigModelDefinition.Definition.Compiled);
        Assert.NotNull(PigModelDefinition.Variants["cold"].Compiled);
        Assert.NotNull(CowModelDefinition.Definition.Compiled);
        Assert.NotNull(CowModelDefinition.Variants["warm"].Compiled);
        Assert.NotNull(ChickenModelDefinition.Definition.Compiled);
        Assert.NotNull(ChickenModelDefinition.Variants["cold"].Compiled);
        Assert.NotNull(SheepModelDefinition.Definition.Compiled);
    }

    [Fact]
    public void SimpleModelsExposeCompiledTrees()
    {
        Assert.NotNull(PandaModelDefinition.Definition.Compiled);
        Assert.NotNull(GoatModelDefinition.Definition.Compiled);
        Assert.NotNull(FrogModelDefinition.Definition.Compiled);
        Assert.NotNull(TadpoleModelDefinition.Definition.Compiled);
        Assert.NotNull(BatModelDefinition.Definition.Compiled);
        Assert.NotNull(AllayModelDefinition.Definition.Compiled);
        Assert.NotNull(BeeModelDefinition.Definition.Compiled);
        Assert.NotNull(VexModelDefinition.Definition.Compiled);
        Assert.NotNull(CreeperModelDefinition.Definition.Compiled);
        Assert.NotNull(SpiderModelDefinition.Definition.Compiled);
        Assert.NotNull(EndermiteModelDefinition.Definition.Compiled);
        Assert.NotNull(ShulkerModelDefinition.Definition.Compiled);
        Assert.NotNull(ShulkerModelDefinition.Variants["blue"].Compiled);
        Assert.NotNull(SquidModelDefinition.Definition.Compiled);
        Assert.NotNull(SquidModelDefinition.GlowSquid.Compiled);
        Assert.NotNull(NautilusModelDefinition.Definition.Compiled);
        Assert.NotNull(PhantomModelDefinition.Definition.Compiled);
        Assert.NotNull(RavagerModelDefinition.Definition.Compiled);
        Assert.NotNull(PolarBearModelDefinition.Definition.Compiled);
        Assert.NotNull(SilverfishModelDefinition.Definition.Compiled);
        Assert.NotNull(IronGolemModelDefinition.Definition.Compiled);
        Assert.NotNull(SnowGolemModelDefinition.Definition.Compiled);
        Assert.NotNull(CreakingModelDefinition.Definition.Compiled);
        Assert.NotNull(CopperGolemModelDefinition.Definition.Compiled);
        Assert.NotNull(WitchModelDefinition.Definition.Compiled);
        Assert.NotNull(IllagerModelDefinition.Vindicator.Compiled);
        Assert.NotNull(IllagerModelDefinition.Evoker.Compiled);
    }

    [Fact]
    public void TreeMesherBuildsBreezeGeometry()
    {
        var state = new EntityRenderState(1, BreezeModelDefinition.BreezeEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var geometry = new EntityModelMesher().Build(
            [state],
            new SpriteRect(0, 0, 32, 32, 1),
            32,
            32);

        Assert.True(geometry.VertexFloats > 0);
        Assert.True(geometry.IndexCount > 0);
    }

    [Fact]
    public void TreeMesherInheritsArmadilloGroupVisibility()
    {
        var unrolled = new EntityRenderState(1, ArmadilloModelDefinition.ArmadilloEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        var rolled = new EntityRenderState(1, ArmadilloModelDefinition.ArmadilloEntityTypeId, 0f, 0f, 0f, 0f, 0f, IsHidingInShell: true);
        Assert.NotNull(ArmadilloModelDefinition.Definition.Compiled);
        Assert.NotNull(EntityModelResolver.Resolve(ArmadilloModelDefinition.ArmadilloEntityTypeId).Compiled);
        Assert.Same(ArmadilloModelDefinition.Definition.Compiled, EntityModelResolver.Resolve(ArmadilloModelDefinition.ArmadilloEntityTypeId).Compiled);
        Assert.Single(ArmadilloModelDefinition.Definition.Compiled!.Find("body")!.Cubes);
        Assert.Equal(6, ArmadilloModelDefinition.Definition.Compiled!.Root.Children.Count);
        var cubeNode = Assert.IsType<EntityModelPartNode>(ArmadilloModelDefinition.Definition.Compiled!.Find("cube"));
        var bodyNode = Assert.IsType<EntityModelPartNode>(ArmadilloModelDefinition.Definition.Compiled!.Find("body"));
        Assert.NotNull(bodyNode.VisibleWhen);
        Assert.True(bodyNode.VisibleWhen!(unrolled));
        Assert.False(bodyNode.VisibleWhen!(rolled));
        Assert.NotNull(cubeNode.VisibleWhen);
        Assert.True(cubeNode.VisibleWhen!(rolled));
        Assert.False(cubeNode.VisibleWhen!(unrolled));
        var unrolledMesher = new EntityModelMesher();
        var rolledMesher = new EntityModelMesher();
        var unrolledGeometry = unrolledMesher.Build([unrolled], new SpriteRect(0, 0, 64, 64, 1), 64, 64);
        var rolledGeometry = rolledMesher.Build([rolled], new SpriteRect(0, 0, 64, 64, 1), 64, 64);

        Assert.True(unrolledGeometry.VertexFloats > 0);
        Assert.True(rolledGeometry.VertexFloats > 0);
        Assert.True(rolledGeometry.VertexFloats < unrolledGeometry.VertexFloats,
            $"rolled={rolledGeometry.VertexFloats}, unrolled={unrolledGeometry.VertexFloats}");
    }
}
