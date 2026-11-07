// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using Sharpcraft.Client.World;
using Sharpcraft.Common.Registries;

/// <summary>
/// Resolves a render-only entity model definition from the registered entity type id. This is the C# equivalent of
/// EntityModelSet.bakeLayer: no world or simulation dependency, and missing entries stay null for placeholder fallback.
/// </summary>
public static class EntityModelResolver
{
    /// <summary>Item identity table used by equipment layer selectors; defaults to the embedded baseline snapshot.</summary>
    public static ItemRegistry? Items { get; set; } = ItemRegistry.LoadEmbedded();

    /// <summary>Item tag table loaded from the extracted pack; empty until the launcher installs it.</summary>
    public static ItemTagRegistry ItemTags { get; set; } = ItemTagRegistry.Empty;

    /// <summary>Resolves a dynamic equipment texture for a render state slot, or null when the slot is empty/unsupported.</summary>
    public static string? ResolveEquipmentTexture(EntityRenderState state, EquipmentAssetKind kind, int slot, bool overlay = false)
    {
        var itemId = state.Equipment[slot];
        if (itemId < 0 || Items is null || Items.ById(itemId) is not { } item)
        {
            return null;
        }

        var texture = EquipmentTextureCatalog.Resolve(kind, item);
        return overlay ? texture?.OverlayPath : texture?.PrimaryPath;
    }

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
        (baby ? BabyVariantModels : VariantModels).TryGetValue((typeId, key), out var definition)
            ? Decorate(typeId, baby, definition)
            : null;

    /// <summary>
    /// Looks up a named variant and throws when it is missing. Used by tools and tests so an unhandled variant is loud
    /// instead of silently falling back (docs/analysis/render/entity-variants.md 6).
    /// </summary>
    public static EntityModelDefinition ResolveVariantStrict(int typeId, string key, bool baby = false) =>
        TryResolveVariant(typeId, key, baby)
        ?? throw new KeyNotFoundException($"no variant '{key}' registered for entity type {typeId}");

    public static EntityModelDefinition? TryResolve(int typeId, bool baby = false) =>
        (baby ? BabyModels : Models).TryGetValue(typeId, out var definition) ? Decorate(typeId, baby, definition) : null;

    public static EntityModelDefinition Resolve(int typeId, bool baby = false) =>
        TryResolve(typeId, baby) ?? Placeholder;

    private static readonly Dictionary<(int TypeId, string Name, bool Baby), EntityModelDefinition> EquippedModels = new();

    private static EntityModelDefinition Decorate(int typeId, bool baby, EntityModelDefinition definition)
    {
        var layers = EquipmentLayers(typeId);
        if (layers.Length == 0)
        {
            return definition;
        }

        var key = (typeId, definition.Name, baby);
        if (EquippedModels.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var parts = definition.Parts;
        if (baby && ReferenceEquals(layers, HumanoidArmorLayers))
        {
            foreach (var slot in HumanoidArmorSlots)
            {
                parts = EntityModelLayerHelpers.WithBabyArmorSlot(parts, EquipmentAssetKind.HumanoidArmorBaby, slot, "entity/equipment/humanoid_baby/iron");
            }

            parts = EntityModelLayerHelpers.WithElytra(parts, baby: true);
        }
        else
        {
            foreach (var (kind, slot, fallback) in layers)
            {
                parts = kind is EquipmentAssetKind.HumanoidArmor or EquipmentAssetKind.HumanoidLeggings
                    ? EntityModelLayerHelpers.WithArmorSlot(parts, kind, slot, fallback)
                    : EntityModelLayerHelpers.WithEquipment(parts, kind, slot, fallback, baseCopy: kind != EquipmentAssetKind.HappyGhastBody);
            }

            if (ReferenceEquals(layers, HumanoidArmorLayers))
            {
                parts = EntityModelLayerHelpers.WithElytra(parts);
            }
        }

        // Wolf armor shows a crackiness overlay based on the chest/body slot durability.
        if (typeId == 152)
        {
            parts = EntityModelLayerHelpers.WithCrackiness(parts);
        }

        var compiled = definition.Compiled;
        if (compiled is not null)
        {
            foreach (var (kind, slot, fallback) in layers)
            {
                var equipmentTree = kind is EquipmentAssetKind.HumanoidArmor or EquipmentAssetKind.HumanoidLeggings
                    ? EquipmentModelTrees.For(kind, slot, player: typeId == EntityPlaceholderMesher.PlayerEntityTypeId)
                    : EquipmentModelTrees.For(kind);
                if (equipmentTree is { })
                {
                    var fixedTexture = typeId == 138 && kind == EquipmentAssetKind.LlamaBody
                        ? "entity/equipment/llama_body/trader_llama"
                        : null;
                    compiled = EntityModelLayerHelpers.WithEquipment(
                        compiled, equipmentTree, kind, slot, fallback, fixedTexture: fixedTexture);
                }
            }

            if (typeId == 152 && EquipmentModelTrees.For(EquipmentAssetKind.WolfBody) is { } wolfArmor)
            {
                compiled = EntityModelLayerHelpers.WithCrackiness(compiled, wolfArmor);
            }

            if (ReferenceEquals(layers, HumanoidArmorLayers))
            {
                compiled = EntityModelLayerHelpers.WithElytra(compiled);
            }
        }

        var decorated = new EntityModelDefinition(
            definition.Name,
            definition.TextureWidth,
            definition.TextureHeight,
            parts,
            definition.FaceTransforms,
            definition.TexturePath,
            definition.Scale,
            definition.EmissiveTexturePath,
            definition.TextureSelector,
            definition.AdditionalTexturePaths.Concat(EquipmentTextureCatalog.AllTexturePaths).Distinct(StringComparer.Ordinal).ToArray(),
            compiled: compiled);
        EquippedModels[key] = decorated;
        return decorated;
    }

    private static readonly int[] HumanoidArmorSlots = [5, 4, 3, 2];

    private static readonly (EquipmentAssetKind Kind, int Slot, string Fallback)[] HumanoidArmorLayers =
    [
        (EquipmentAssetKind.HumanoidArmor, 5, "entity/equipment/humanoid/iron"),
        (EquipmentAssetKind.HumanoidArmor, 4, "entity/equipment/humanoid/iron"),
        (EquipmentAssetKind.HumanoidLeggings, 3, "entity/equipment/humanoid_leggings/iron"),
        (EquipmentAssetKind.HumanoidArmor, 2, "entity/equipment/humanoid/iron")
    ];

    private static (EquipmentAssetKind Kind, int Slot, string Fallback)[] EquipmentLayers(int typeId) => typeId switch
    {
        67 => [(EquipmentAssetKind.HorseBody, 6, "entity/equipment/horse_body/iron"),
               (EquipmentAssetKind.HorseSaddle, 7, "entity/equipment/horse_saddle/saddle")],
        37 => [(EquipmentAssetKind.DonkeySaddle, 7, "entity/equipment/donkey_saddle/saddle")],
        88 => [(EquipmentAssetKind.MuleSaddle, 7, "entity/equipment/mule_saddle/saddle")],
        119 => [(EquipmentAssetKind.SkeletonHorseSaddle, 7, "entity/equipment/skeleton_horse_saddle/saddle")],
        155 => [(EquipmentAssetKind.ZombieHorseSaddle, 7, "entity/equipment/zombie_horse_saddle/saddle")],
        19 => [(EquipmentAssetKind.CamelSaddle, 7, "entity/equipment/camel_saddle/saddle")],
        79 or 138 => [(EquipmentAssetKind.LlamaBody, 6, "entity/equipment/llama_body/white")],
        89 or 156 => [(EquipmentAssetKind.NautilusBody, 6, "entity/equipment/nautilus_body/iron"),
                      (EquipmentAssetKind.NautilusSaddle, 7, "entity/equipment/nautilus_saddle/saddle")],
        59 => [(EquipmentAssetKind.HappyGhastBody, 6, "entity/equipment/happy_ghast_body/white_harness")],
        152 => [(EquipmentAssetKind.WolfBody, 6, "entity/equipment/wolf_body/armadillo_scute")],
        159 => HumanoidArmorLayers,
        154 or 39 or 68 or 118 or 131 or 16 or 150 or 157 => HumanoidArmorLayers,
        _ => []
    };
    public static int? FindTypeId(string name)
    {        foreach (var pair in Models)
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
        new(BabyModelDefinitions.Models
            .Concat(BabyModelDefinitionsExtra.Models)
            .Concat(BabyModelDefinitionsEquine.Models)
            .Concat(BabyModelDefinitionsMisc.Models)
            .Concat(BabyModelDefinitionsAquatic.Models)
            .Concat(BabyModelDefinitionsHumanoid.Models)
            .Concat(BabyModelDefinitionsNether.Models)
            .Concat(BabyModelDefinitionsOdd.Models)
            .Concat(BabyModelDefinitionsFinal.Models));
    private static readonly Dictionary<(int TypeId, string Key), EntityModelDefinition> BabyVariantModels =
        new(BabyModelDefinitions.Variants
            .Concat(BabyModelDefinitionsExtra.Variants)
            .Concat(BabyModelDefinitionsEquine.Variants)
            .Concat(BabyModelDefinitionsMisc.Variants)
            .Concat(BabyModelDefinitionsAquatic.Variants)
            .Concat(BabyModelDefinitionsHumanoid.Variants));

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
        RegisterVariants(PandaModelDefinition.PandaEntityTypeId, PandaModelDefinition.Variants);
        RegisterVariants(VillagerModelDefinition.VillagerEntityTypeId, VillagerModelDefinition.Variants);
        RegisterVariants(ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId, ZombieVillagerModelDefinition.Variants);
    }
}


