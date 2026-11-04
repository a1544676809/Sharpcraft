// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Per-creature animation controller and its clip set. One singleton instance is registered per entity type id; the
/// instance creates and consumes a per-entity <see cref="IEntityAnimationState"/> and never holds entity- or
/// frame-specific state itself.
/// </summary>
/// <remarks>
/// <see cref="Apply"/> is the game hot path: it is called once per entity per frame from the mesh preparation pass and
/// must not allocate (docs/analysis/render/entity-animation-architecture.md 6.2). <see cref="ApplyClip"/> is only for
/// the sandbox/tooling path, where the caller names a clip with a string and passes explicit seconds.
/// </remarks>
public interface IEntityAnimationProvider
{
    /// <summary>The entity type id this provider animates.</summary>
    int TypeId { get; }

    /// <summary>Clip names exposed for the sandbox and tools; empty means this type has no animation.</summary>
    IReadOnlyList<string> Clips { get; }

    /// <summary>Creates the per-entity mutable animation state. Called once per entity, never per frame.</summary>
    IEntityAnimationState CreateState();

    /// <summary>Updates derived state from <paramref name="frame"/> and writes this entity's pose overrides.</summary>
    void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose);

    /// <summary>Sandbox/tool path: plays one named clip at an explicit elapsed time.</summary>
    void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose);
}

/// <summary>
/// Maps an entity type id to its animation provider. Built once at startup; lookup is a dictionary hit and allocates
/// nothing.
/// </summary>
public static class EntityAnimationRegistry
{
    private static readonly IEntityAnimationProvider[] Providers = Build();
    private static readonly Dictionary<int, IEntityAnimationProvider> ByTypeId = Index(Providers);

    /// <summary>All registered providers, in registration order.</summary>
    public static IReadOnlyList<IEntityAnimationProvider> All => Providers;

    /// <summary>Looks up the provider for a type id.</summary>
    public static bool TryGet(int typeId, out IEntityAnimationProvider provider) =>
        ByTypeId.TryGetValue(typeId, out provider!);

    /// <summary>The provider for a type id, or null when the type has no animation.</summary>
    public static IEntityAnimationProvider? For(int typeId) =>
        ByTypeId.GetValueOrDefault(typeId);

    /// <summary>Creates the per-entity state for a type id, or null when the type has no animation.</summary>
    public static IEntityAnimationState? CreateState(int typeId) => For(typeId)?.CreateState();

    /// <summary>Clip names for a type id (empty when the type has no animation).</summary>
    public static IReadOnlyList<string> Clips(int typeId) => For(typeId)?.Clips ?? Array.Empty<string>();

    private static Dictionary<int, IEntityAnimationProvider> Index(IEntityAnimationProvider[] providers)
    {
        var map = new Dictionary<int, IEntityAnimationProvider>(providers.Length);
        foreach (var provider in providers)
        {
            if (!map.TryAdd(provider.TypeId, provider))
            {
                throw new InvalidOperationException($"duplicate animation provider for type id {provider.TypeId}");
            }
        }

        return map;
    }

    private static IEntityAnimationProvider[] Build() =>
    [
        new QuadrupedAnimationProvider(PigModelDefinition.PigEntityTypeId),
        new QuadrupedAnimationProvider(CowModelDefinition.CowEntityTypeId),
        new QuadrupedAnimationProvider(CowModelDefinition.MooshroomEntityTypeId),
        new QuadrupedAnimationProvider(SheepModelDefinition.SheepEntityTypeId),
        new QuadrupedAnimationProvider(GoatModelDefinition.GoatEntityTypeId),
        new EquineAnimationProvider(HorseModelDefinition.HorseEntityTypeId),
        new EquineAnimationProvider(HorseModelDefinition.DonkeyEntityTypeId),
        new EquineAnimationProvider(HorseModelDefinition.MuleEntityTypeId),
        new EquineAnimationProvider(HorseModelDefinition.SkeletonHorseEntityTypeId),
        new EquineAnimationProvider(HorseModelDefinition.ZombieHorseEntityTypeId),
        new HumanoidAnimationProvider(HumanoidMobModelDefinition.ZombieEntityTypeId),
        new HumanoidAnimationProvider(HumanoidMobModelDefinition.DrownedEntityTypeId),
        new HumanoidAnimationProvider(HumanoidMobModelDefinition.HuskEntityTypeId),
        new VillagerAnimationProvider(VillagerModelDefinition.VillagerEntityTypeId),
        new ChickenAnimationProvider(ChickenModelDefinition.ChickenEntityTypeId),
        new SquidAnimationProvider(SquidModelDefinition.SquidEntityTypeId),
        new SquidAnimationProvider(SquidModelDefinition.GlowSquidEntityTypeId),
        new BatAnimationProvider(BatModelDefinition.BatEntityTypeId),
        new CamelAnimationProvider(CamelModelDefinition.CamelEntityTypeId),
        new ArmadilloAnimationProvider(ArmadilloModelDefinition.ArmadilloEntityTypeId),
        new FrogAnimationProvider(FrogModelDefinition.FrogEntityTypeId),
        new AxolotlAnimationProvider(AxolotlModelDefinition.AxolotlEntityTypeId),
        new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId),
        new CreakingAnimationProvider(),
        new BreezeAnimationProvider(BreezeModelDefinition.BreezeEntityTypeId),
    ];
}
