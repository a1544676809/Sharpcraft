// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Provider registry and per-provider steady-state allocation (docs/analysis/render/entity-animation-architecture.md 6.2).</summary>
public sealed class EntityAnimationProviderTests
{
    private static readonly int[] MigratedTypeIds =
    [
        PigModelDefinition.PigEntityTypeId,
        CowModelDefinition.CowEntityTypeId,
        CowModelDefinition.MooshroomEntityTypeId,
        SheepModelDefinition.SheepEntityTypeId,
        GoatModelDefinition.GoatEntityTypeId,
        HorseModelDefinition.HorseEntityTypeId,
        HorseModelDefinition.DonkeyEntityTypeId,
        HorseModelDefinition.MuleEntityTypeId,
        HorseModelDefinition.SkeletonHorseEntityTypeId,
        HorseModelDefinition.ZombieHorseEntityTypeId,
        HumanoidMobModelDefinition.ZombieEntityTypeId,
        HumanoidMobModelDefinition.DrownedEntityTypeId,
        HumanoidMobModelDefinition.HuskEntityTypeId,
        VillagerModelDefinition.VillagerEntityTypeId,
        ChickenModelDefinition.ChickenEntityTypeId,
        SquidModelDefinition.SquidEntityTypeId,
        SquidModelDefinition.GlowSquidEntityTypeId,
        BatModelDefinition.BatEntityTypeId,
        CamelModelDefinition.CamelEntityTypeId,
        ArmadilloModelDefinition.ArmadilloEntityTypeId,
        FrogModelDefinition.FrogEntityTypeId,
        AxolotlModelDefinition.AxolotlEntityTypeId,
        WardenModelDefinition.WardenEntityTypeId,
        CreakingModelDefinition.CreakingEntityTypeId,
        BreezeModelDefinition.BreezeEntityTypeId,
        SnifferModelDefinition.SnifferEntityTypeId,
        CopperGolemModelDefinition.CopperGolemEntityTypeId,
        NautilusModelDefinition.NautilusEntityTypeId,
        ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId,
        RavagerModelDefinition.RavagerEntityTypeId,
        HoglinModelDefinition.HoglinEntityTypeId,
        HoglinModelDefinition.ZoglinEntityTypeId,
        WitchModelDefinition.WitchEntityTypeId,
        VexModelDefinition.VexEntityTypeId,
        ParrotModelDefinition.ParrotEntityTypeId,
        StriderModelDefinition.StriderEntityTypeId,
        TadpoleModelDefinition.TadpoleEntityTypeId,
        GhastModelDefinition.GhastEntityTypeId,
        AllayModelDefinition.AllayEntityTypeId,
        GuardianModelDefinition.GuardianEntityTypeId,
        GuardianModelDefinition.ElderGuardianEntityTypeId,
        SpiderModelDefinition.SpiderEntityTypeId,
        CaveSpiderModelDefinition.CaveSpiderEntityTypeId,
        SilverfishModelDefinition.SilverfishEntityTypeId,
        BlazeModelDefinition.BlazeEntityTypeId,
        PhantomModelDefinition.PhantomEntityTypeId,
        CodModelDefinition.CodEntityTypeId,
        SalmonModelDefinition.SalmonEntityTypeId,
        SnowGolemModelDefinition.SnowGolemEntityTypeId,
        HappyGhastModelDefinition.HappyGhastEntityTypeId,
        IronGolemModelDefinition.IronGolemEntityTypeId,
        CreeperModelDefinition.CreeperEntityTypeId,
        GiantModelDefinition.GiantEntityTypeId,
        EndermiteModelDefinition.EndermiteEntityTypeId,
        DolphinModelDefinition.DolphinEntityTypeId,
        VillagerModelDefinition.WanderingTraderEntityTypeId,
        LlamaModelDefinition.LlamaEntityTypeId,
        TraderLlamaModelDefinition.TraderLlamaEntityTypeId,
        TurtleModelDefinition.TurtleEntityTypeId,
        ShulkerModelDefinition.ShulkerEntityTypeId,
        BeeModelDefinition.BeeEntityTypeId,
        IllagerModelDefinition.VindicatorEntityTypeId,
        IllagerModelDefinition.EvokerEntityTypeId,
        IllagerModelDefinition.PillagerEntityTypeId,
        IllagerModelDefinition.IllusionerEntityTypeId,
    ];

    [Fact]
    public void RegistryCoversEveryMigratedCreature()
    {
        foreach (var typeId in MigratedTypeIds)
        {
            Assert.True(EntityAnimationRegistry.TryGet(typeId, out var provider), $"no provider for type id {typeId}");
            Assert.Equal(typeId, provider.TypeId);
            Assert.NotEmpty(provider.Clips);
            Assert.NotNull(provider.CreateState());
            Assert.Same(provider, EntityAnimationRegistry.For(typeId));
            Assert.NotEmpty(EntityAnimationRegistry.Clips(typeId));
        }
    }

    [Fact]
    public void RegistryReturnsNothingForAnUnanimatedType()
    {
        Assert.False(EntityAnimationRegistry.TryGet(0, out _));
        Assert.Null(EntityAnimationRegistry.For(0));
        Assert.Empty(EntityAnimationRegistry.Clips(0));
        Assert.Null(EntityAnimationRegistry.CreateState(0));
    }

    [Fact]
    public void EveryAdvertisedClipAppliesWithoutThrowing()
    {
        foreach (var typeId in MigratedTypeIds)
        {
            var provider = EntityAnimationRegistry.For(typeId)!;
            var state = provider.CreateState();
            var frame = FrameFor(typeId);
            foreach (var clip in provider.Clips)
            {
                foreach (var seconds in new[] { 0f, 0.25f, 1.7f })
                {
                    var pose = new EntityPose();
                    provider.ApplyClip(state, in frame, clip, seconds, pose);
                }
            }
        }
    }

    [Theory]
    [InlineData(PigModelDefinition.PigEntityTypeId)]
    [InlineData(HorseModelDefinition.HorseEntityTypeId)]
    [InlineData(HumanoidMobModelDefinition.ZombieEntityTypeId)]
    [InlineData(VillagerModelDefinition.VillagerEntityTypeId)]
    [InlineData(ChickenModelDefinition.ChickenEntityTypeId)]
    [InlineData(SquidModelDefinition.SquidEntityTypeId)]
    [InlineData(BatModelDefinition.BatEntityTypeId)]
    [InlineData(CamelModelDefinition.CamelEntityTypeId)]
    [InlineData(ArmadilloModelDefinition.ArmadilloEntityTypeId)]
    [InlineData(FrogModelDefinition.FrogEntityTypeId)]
    [InlineData(AxolotlModelDefinition.AxolotlEntityTypeId)]
    [InlineData(WardenModelDefinition.WardenEntityTypeId)]
    [InlineData(CreakingModelDefinition.CreakingEntityTypeId)]
    [InlineData(BreezeModelDefinition.BreezeEntityTypeId)]
    [InlineData(SnifferModelDefinition.SnifferEntityTypeId)]
    [InlineData(CopperGolemModelDefinition.CopperGolemEntityTypeId)]
    [InlineData(NautilusModelDefinition.NautilusEntityTypeId)]
    [InlineData(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId)]
    [InlineData(RavagerModelDefinition.RavagerEntityTypeId)]
    [InlineData(HoglinModelDefinition.HoglinEntityTypeId)]
    [InlineData(HoglinModelDefinition.ZoglinEntityTypeId)]
    [InlineData(WitchModelDefinition.WitchEntityTypeId)]
    [InlineData(VexModelDefinition.VexEntityTypeId)]
    [InlineData(ParrotModelDefinition.ParrotEntityTypeId)]
    [InlineData(StriderModelDefinition.StriderEntityTypeId)]
    [InlineData(TadpoleModelDefinition.TadpoleEntityTypeId)]
    [InlineData(GhastModelDefinition.GhastEntityTypeId)]
    [InlineData(AllayModelDefinition.AllayEntityTypeId)]
    [InlineData(GuardianModelDefinition.GuardianEntityTypeId)]
    [InlineData(GuardianModelDefinition.ElderGuardianEntityTypeId)]
    [InlineData(SpiderModelDefinition.SpiderEntityTypeId)]
    [InlineData(CaveSpiderModelDefinition.CaveSpiderEntityTypeId)]
    [InlineData(SilverfishModelDefinition.SilverfishEntityTypeId)]
    [InlineData(BlazeModelDefinition.BlazeEntityTypeId)]
    [InlineData(PhantomModelDefinition.PhantomEntityTypeId)]
    [InlineData(CodModelDefinition.CodEntityTypeId)]
    [InlineData(SalmonModelDefinition.SalmonEntityTypeId)]
    [InlineData(SnowGolemModelDefinition.SnowGolemEntityTypeId)]
    [InlineData(HappyGhastModelDefinition.HappyGhastEntityTypeId)]
    [InlineData(IronGolemModelDefinition.IronGolemEntityTypeId)]
    [InlineData(CreeperModelDefinition.CreeperEntityTypeId)]
    [InlineData(GiantModelDefinition.GiantEntityTypeId)]
    [InlineData(EndermiteModelDefinition.EndermiteEntityTypeId)]
    [InlineData(DolphinModelDefinition.DolphinEntityTypeId)]
    [InlineData(VillagerModelDefinition.WanderingTraderEntityTypeId)]
    [InlineData(LlamaModelDefinition.LlamaEntityTypeId)]
    [InlineData(TraderLlamaModelDefinition.TraderLlamaEntityTypeId)]
    [InlineData(TurtleModelDefinition.TurtleEntityTypeId)]
    [InlineData(ShulkerModelDefinition.ShulkerEntityTypeId)]
    [InlineData(BeeModelDefinition.BeeEntityTypeId)]
    [InlineData(IllagerModelDefinition.VindicatorEntityTypeId)]
    [InlineData(IllagerModelDefinition.EvokerEntityTypeId)]
    [InlineData(IllagerModelDefinition.PillagerEntityTypeId)]
    [InlineData(IllagerModelDefinition.IllusionerEntityTypeId)]
    public void ProviderApplyDoesNotAllocateOnceWarm(int typeId)
    {
        var provider = EntityAnimationRegistry.For(typeId)!;
        var state = provider.CreateState();
        var pose = new EntityPose();
        var frame = FrameFor(typeId);

        for (var i = 0; i < 512; i++)
        {
            pose.Clear();
            provider.Apply(state, in frame, pose);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++)
        {
            pose.Clear();
            provider.Apply(state, in frame, pose);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    private static EntityAnimationFrame FrameFor(int typeId) => new(
        TypeId: typeId,
        AgeInTicks: 12.5f,
        WalkAnimationPos: 3.25f,
        WalkAnimationSpeed: 0.8f,
        Pitch: 15f,
        Yaw: -30f,
        Flap: 1.2f,
        FlapSpeed: 0.7f,
        TentacleAngle: 0.4f,
        IsResting: typeId == BatModelDefinition.BatEntityTypeId,
        IsBaby: typeId == AxolotlModelDefinition.AxolotlEntityTypeId,
        IsHidingInShell: typeId == ArmadilloModelDefinition.ArmadilloEntityTypeId);
}
