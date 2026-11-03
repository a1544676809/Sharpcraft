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
        [BatModelDefinition.BatEntityTypeId] = BatModelDefinition.Definition,
        [BeeModelDefinition.BeeEntityTypeId] = BeeModelDefinition.Definition,
        [DolphinModelDefinition.DolphinEntityTypeId] = DolphinModelDefinition.Definition,
        [FrogModelDefinition.FrogEntityTypeId] = FrogModelDefinition.Definition,
        [TadpoleModelDefinition.TadpoleEntityTypeId] = TadpoleModelDefinition.Definition,
        [AxolotlModelDefinition.AxolotlEntityTypeId] = AxolotlModelDefinition.Definition,
        [StriderModelDefinition.StriderEntityTypeId] = StriderModelDefinition.Definition,
        [HoglinModelDefinition.HoglinEntityTypeId] = HoglinModelDefinition.Definition,
        [HoglinModelDefinition.ZoglinEntityTypeId] = HoglinModelDefinition.Zoglin,
        [PiglinModelDefinition.PiglinEntityTypeId] = PiglinModelDefinition.Definition,
        [PiglinModelDefinition.PiglinBruteEntityTypeId] = PiglinModelDefinition.Brute,
        [PiglinModelDefinition.ZombifiedPiglinEntityTypeId] = PiglinModelDefinition.Zombified,
        [AllayModelDefinition.AllayEntityTypeId] = AllayModelDefinition.Definition,
        [VexModelDefinition.VexEntityTypeId] = VexModelDefinition.Definition,
        [ParrotModelDefinition.ParrotEntityTypeId] = ParrotModelDefinition.Definition,
        [ShulkerModelDefinition.ShulkerEntityTypeId] = ShulkerModelDefinition.Definition,
        [CodModelDefinition.CodEntityTypeId] = CodModelDefinition.Definition,
        [SalmonModelDefinition.SalmonEntityTypeId] = SalmonModelDefinition.Definition,
        [PufferfishModelDefinition.PufferfishEntityTypeId] = PufferfishModelDefinition.Definition,
        [NautilusModelDefinition.NautilusEntityTypeId] = NautilusModelDefinition.Definition,
        [CaveSpiderModelDefinition.CaveSpiderEntityTypeId] = CaveSpiderModelDefinition.Definition,
        [TraderLlamaModelDefinition.TraderLlamaEntityTypeId] = TraderLlamaModelDefinition.Definition,
        [WitchModelDefinition.WitchEntityTypeId] = WitchModelDefinition.Definition,
        [GiantModelDefinition.GiantEntityTypeId] = GiantModelDefinition.Definition,
        [SnifferModelDefinition.SnifferEntityTypeId] = SnifferModelDefinition.Definition,
        [CreakingModelDefinition.CreakingEntityTypeId] = CreakingModelDefinition.Definition,
        [GhastModelDefinition.GhastEntityTypeId] = GhastModelDefinition.Definition,
        [HappyGhastModelDefinition.HappyGhastEntityTypeId] = HappyGhastModelDefinition.Definition,
        [PhantomModelDefinition.PhantomEntityTypeId] = PhantomModelDefinition.Definition,
        [RavagerModelDefinition.RavagerEntityTypeId] = RavagerModelDefinition.Definition,
        [ArmadilloModelDefinition.ArmadilloEntityTypeId] = ArmadilloModelDefinition.Definition,
        [CopperGolemModelDefinition.CopperGolemEntityTypeId] = CopperGolemModelDefinition.Definition,
        [BreezeModelDefinition.BreezeEntityTypeId] = BreezeModelDefinition.Definition,
        [ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId] = ZombieNautilusModelDefinition.Definition,
        [WitherBossModelDefinition.WitherEntityTypeId] = WitherBossModelDefinition.Definition,
        [WardenModelDefinition.WardenEntityTypeId] = WardenModelDefinition.Definition,
        [EnderDragonModelDefinition.EnderDragonEntityTypeId] = EnderDragonModelDefinition.Definition,
        [TropicalFishModelDefinition.TropicalFishEntityTypeId] = TropicalFishModelDefinition.Definition,
        [GuardianModelDefinition.GuardianEntityTypeId] = GuardianModelDefinition.Definition,
        [GuardianModelDefinition.ElderGuardianEntityTypeId] = GuardianModelDefinition.Elder,
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
        [IllagerModelDefinition.IllusionerEntityTypeId] = IllagerModelDefinition.Illusioner,
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
        Models.Values.Concat(VariantModels.Values).Concat(BabyModels.Values).Concat(BabyVariantModels.Values).Distinct().ToArray();

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
    public static EntityModelDefinition? TryResolveVariant(int typeId, string key, bool baby = false) =>
        (baby ? BabyVariantModels : VariantModels).TryGetValue((typeId, key), out var definition) ? definition : null;

    /// <summary>
    /// Looks up a named variant and throws when it is missing. Used by tools and tests so an unhandled variant is loud
    /// instead of silently falling back (docs/analysis/render/entity-variants.md 6).
    /// </summary>
    public static EntityModelDefinition ResolveVariantStrict(int typeId, string key, bool baby = false) =>
        TryResolveVariant(typeId, key, baby)
        ?? throw new KeyNotFoundException($"no variant '{key}' registered for entity type {typeId}");

    public static EntityModelDefinition? TryResolve(int typeId, bool baby = false) =>
        (baby ? BabyModels : Models).TryGetValue(typeId, out var definition) ? definition : null;

    public static EntityModelDefinition Resolve(int typeId, bool baby = false) =>
        TryResolve(typeId, baby) ?? Placeholder;

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

    private static readonly Dictionary<int, EntityModelDefinition> BabyModels =
        new(BabyModelDefinitions.Models.Concat(BabyModelDefinitionsExtra.Models));
    private static readonly Dictionary<(int TypeId, string Key), EntityModelDefinition> BabyVariantModels =
        new(BabyModelDefinitions.Variants.Concat(BabyModelDefinitionsExtra.Variants));

    static EntityModelResolver()
    {
        RegisterVariants(PigModelDefinition.PigEntityTypeId, PigModelDefinition.Variants);
        RegisterVariants(CowModelDefinition.CowEntityTypeId, CowModelDefinition.Variants);
        RegisterVariants(ChickenModelDefinition.ChickenEntityTypeId, ChickenModelDefinition.Variants);
        RegisterVariants(CatModelDefinition.CatEntityTypeId, CatModelDefinition.Variants);
        RegisterVariants(HorseModelDefinition.HorseEntityTypeId, HorseModelDefinition.Variants);
        RegisterVariants(WolfModelDefinition.WolfEntityTypeId, WolfModelDefinition.Variants);
        RegisterVariants(FoxModelDefinition.FoxEntityTypeId, FoxModelDefinition.Variants);
        RegisterVariants(FrogModelDefinition.FrogEntityTypeId, FrogModelDefinition.Variants);
        RegisterVariants(AxolotlModelDefinition.AxolotlEntityTypeId, AxolotlModelDefinition.Variants);
        RegisterVariants(ParrotModelDefinition.ParrotEntityTypeId, ParrotModelDefinition.Variants);
        RegisterVariants(ShulkerModelDefinition.ShulkerEntityTypeId, ShulkerModelDefinition.Variants);
        RegisterVariants(SalmonModelDefinition.SalmonEntityTypeId, SalmonModelDefinition.Variants);
        RegisterVariants(PufferfishModelDefinition.PufferfishEntityTypeId, PufferfishModelDefinition.Variants);
        RegisterVariants(CopperGolemModelDefinition.CopperGolemEntityTypeId, CopperGolemModelDefinition.Variants);
        RegisterVariants(ZombieNautilusModelDefinition.ZombieNautilusEntityTypeId, ZombieNautilusModelDefinition.Variants);
        RegisterVariants(TropicalFishModelDefinition.TropicalFishEntityTypeId, TropicalFishModelDefinition.Variants);
        RegisterVariants(LlamaModelDefinition.LlamaEntityTypeId, LlamaModelDefinition.Variants);
        RegisterVariants(RabbitModelDefinition.RabbitEntityTypeId, RabbitModelDefinition.Variants);
    }
}
