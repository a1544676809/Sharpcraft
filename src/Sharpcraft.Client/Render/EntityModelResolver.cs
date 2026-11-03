// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Resolves a render-only entity model definition from the registered entity type id. This is the C# equivalent of
/// EntityModelSet.bakeLayer: no world or simulation dependency, and missing entries stay null for placeholder fallback.
/// </summary>
public static class EntityModelResolver
{
    public static readonly EntityModelDefinition Placeholder = new(
        "placeholder",
        64,
        64,
        [new EntityModelPart("body", 0f, 0f, 14f, 14f, 14f, 0, 0)]);

    private static readonly Dictionary<int, EntityModelDefinition> Models = new()
    {        [EntityPlaceholderMesher.PlayerEntityTypeId] = PlayerModelDefinition.Definition,
        [HumanoidMobModelDefinition.SkeletonEntityTypeId] = HumanoidMobModelDefinition.Skeleton,
        [HumanoidMobModelDefinition.StrayEntityTypeId] = HumanoidMobModelDefinition.Stray,
        [HumanoidMobModelDefinition.BoggedEntityTypeId] = HumanoidMobModelDefinition.Bogged,
        [HumanoidMobModelDefinition.WitherSkeletonEntityTypeId] = HumanoidMobModelDefinition.WitherSkeleton,
        [HumanoidMobModelDefinition.DrownedEntityTypeId] = HumanoidMobModelDefinition.Drowned,
        [HumanoidMobModelDefinition.EndermanEntityTypeId] = HumanoidMobModelDefinition.Enderman,
        [CreeperModelDefinition.CreeperEntityTypeId] = CreeperModelDefinition.Definition,
        [SlimeModelDefinition.SlimeEntityTypeId] = SlimeModelDefinition.Definition,
        [MagmaCubeModelDefinition.MagmaCubeEntityTypeId] = MagmaCubeModelDefinition.Definition,
        [SpiderModelDefinition.SpiderEntityTypeId] = SpiderModelDefinition.Definition,
        [PigModelDefinition.PigEntityTypeId] = PigModelDefinition.Definition,
        [SheepModelDefinition.SheepEntityTypeId] = SheepModelDefinition.Definition,
        [CowModelDefinition.CowEntityTypeId] = CowModelDefinition.Definition,
        [CowModelDefinition.MooshroomEntityTypeId] = CowModelDefinition.Mooshroom,
        [ChickenModelDefinition.ChickenEntityTypeId] = ChickenModelDefinition.Definition,
        [RabbitModelDefinition.RabbitEntityTypeId] = RabbitModelDefinition.Definition,
        [CamelModelDefinition.CamelEntityTypeId] = CamelModelDefinition.Definition,
        [WolfModelDefinition.WolfEntityTypeId] = WolfModelDefinition.Definition,
        [CatModelDefinition.CatEntityTypeId] = CatModelDefinition.Definition,
        [CatModelDefinition.OcelotEntityTypeId] = CatModelDefinition.Ocelot,
        [FoxModelDefinition.FoxEntityTypeId] = FoxModelDefinition.Definition,
        [PolarBearModelDefinition.PolarBearEntityTypeId] = PolarBearModelDefinition.Definition,
        [PandaModelDefinition.PandaEntityTypeId] = PandaModelDefinition.Definition,
        [GoatModelDefinition.GoatEntityTypeId] = GoatModelDefinition.Definition,
        [SnowGolemModelDefinition.SnowGolemEntityTypeId] = SnowGolemModelDefinition.Definition,
        [SquidModelDefinition.SquidEntityTypeId] = SquidModelDefinition.Definition,
        [SquidModelDefinition.GlowSquidEntityTypeId] = SquidModelDefinition.GlowSquid,
        [HorseModelDefinition.HorseEntityTypeId] = HorseModelDefinition.Definition,
        [HorseModelDefinition.DonkeyEntityTypeId] = HorseModelDefinition.Donkey,
        [HorseModelDefinition.MuleEntityTypeId] = HorseModelDefinition.Mule,
        [HorseModelDefinition.ZombieHorseEntityTypeId] = HorseModelDefinition.ZombieHorse,
        [HorseModelDefinition.SkeletonHorseEntityTypeId] = HorseModelDefinition.SkeletonHorse,
        [LlamaModelDefinition.LlamaEntityTypeId] = LlamaModelDefinition.Definition,
        [VillagerModelDefinition.VillagerEntityTypeId] = VillagerModelDefinition.Definition,
        [VillagerModelDefinition.WanderingTraderEntityTypeId] = VillagerModelDefinition.WanderingTrader,
        [ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId] = ZombieVillagerModelDefinition.Definition,
        [EndermiteModelDefinition.EndermiteEntityTypeId] = EndermiteModelDefinition.Definition,
        [SilverfishModelDefinition.SilverfishEntityTypeId] = SilverfishModelDefinition.Definition,
        [IronGolemModelDefinition.IronGolemEntityTypeId] = IronGolemModelDefinition.Definition,
        [IllagerModelDefinition.VindicatorEntityTypeId] = IllagerModelDefinition.Vindicator,
        [IllagerModelDefinition.EvokerEntityTypeId] = IllagerModelDefinition.Evoker,
        [IllagerModelDefinition.PillagerEntityTypeId] = IllagerModelDefinition.Pillager,
        [BlazeModelDefinition.BlazeEntityTypeId] = BlazeModelDefinition.Definition,
        [TurtleModelDefinition.TurtleEntityTypeId] = TurtleModelDefinition.Definition,
        [HumanoidMobModelDefinition.HuskEntityTypeId] = HumanoidMobModelDefinition.Husk,
        [HumanoidMobModelDefinition.ZombieEntityTypeId] = HumanoidMobModelDefinition.Zombie
    };

    /// <summary>
    /// Variant definitions keyed by entity type id and registry entry name (docs/analysis/render/entity-variants.md).
    /// The base <see cref="Models"/> entry for a type is its default variant.
    /// </summary>
    private static readonly Dictionary<(int TypeId, string Key), EntityModelDefinition> VariantModels = new();

    public static IReadOnlyCollection<EntityModelDefinition> Definitions =>
        Models.Values.Concat(VariantModels.Values).Distinct().ToArray();

    /// <summary>Registers a named variant for an entity type.</summary>
    public static void RegisterVariant(int typeId, string key, EntityModelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrEmpty(key);
        VariantModels[(typeId, key)] = definition;
    }

    /// <summary>Registers every variant of one entity type.</summary>
    public static void RegisterVariants(int typeId, IReadOnlyDictionary<string, EntityModelDefinition> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        foreach (var pair in variants)
        {
            RegisterVariant(typeId, pair.Key, pair.Value);
        }
    }

    /// <summary>Looks up a named variant, or null when the type has no such variant.</summary>
    public static EntityModelDefinition? TryResolveVariant(int typeId, string key) =>
        VariantModels.TryGetValue((typeId, key), out var definition) ? definition : null;

    /// <summary>
    /// Looks up a named variant and throws when it is missing. Used by tools and tests so an unhandled variant is loud
    /// instead of silently falling back (docs/analysis/render/entity-variants.md 6).
    /// </summary>
    public static EntityModelDefinition ResolveVariantStrict(int typeId, string key) =>
        TryResolveVariant(typeId, key)
        ?? throw new KeyNotFoundException($"no variant '{key}' registered for entity type {typeId}");

    public static EntityModelDefinition? TryResolve(int typeId) =>
        Models.TryGetValue(typeId, out var definition) ? definition : null;

    public static EntityModelDefinition Resolve(int typeId) =>
        TryResolve(typeId) ?? Placeholder;

    public static int? FindTypeId(string name)
    {
        foreach (var pair in Models)
        {
            if (string.Equals(pair.Value.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }

    public static void Register(int typeId, EntityModelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Models[typeId] = definition;
    }

    static EntityModelResolver()
    {
        RegisterVariants(PigModelDefinition.PigEntityTypeId, PigModelDefinition.Variants);
        RegisterVariants(CowModelDefinition.CowEntityTypeId, CowModelDefinition.Variants);
        RegisterVariants(ChickenModelDefinition.ChickenEntityTypeId, ChickenModelDefinition.Variants);
        RegisterVariants(CatModelDefinition.CatEntityTypeId, CatModelDefinition.Variants);
        RegisterVariants(HorseModelDefinition.HorseEntityTypeId, HorseModelDefinition.Variants);
        RegisterVariants(LlamaModelDefinition.LlamaEntityTypeId, LlamaModelDefinition.Variants);
    }
}
