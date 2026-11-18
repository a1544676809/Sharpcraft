// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;

/// <summary>
/// Resolves a render-only entity model definition from the registered entity type id. This is the C# equivalent of
/// EntityModelSet.bakeLayer: no world or simulation dependency, and missing entries stay null for placeholder fallback.
/// </summary>
public static partial class EntityModelResolver
{
    /// <summary>Item identity table used by equipment layer selectors; defaults to the embedded baseline snapshot.</summary>
    public static ItemRegistry? Items { get; set; } = ItemRegistry.LoadEmbedded();

    /// <summary>Item model resolver installed by the window host; used by held-item layer selectors.</summary>
    public static ItemModelResolver? ItemModels { get; set; }

    /// <summary>Item tag table loaded from the extracted pack; empty until the launcher installs it.</summary>
    public static ItemTagRegistry ItemTags { get; set; } = ItemTagRegistry.Empty;


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
        Models.Values.Concat(VariantModels.Values).Concat(BabyModels.Values).Concat(BabyVariantModels.Values)
            .Concat([HappyGhastModelDefinition.RopesDefinition, HappyGhastModelDefinition.RopesBabyDefinition])
            .Distinct().ToArray();

    /// <summary>Every registered entity type id, used by registry-wide model gates.</summary>
    public static IReadOnlyCollection<int> RegisteredTypeIds =>
        Models.Keys.Concat(BabyModels.Keys).Distinct().Order().ToArray();

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

    /// <summary>Variant key used for the slim player model.</summary>
    public const string SlimPlayerVariantKey = "slim";

    /// <summary>
    /// Resolves the decorated player model for the requested arm width. Slim is registered as a named variant so it
    /// passes through <see cref="Decorate"/> and keeps the same equipment layers as the wide player.
    /// </summary>
    public static EntityModelDefinition ResolvePlayerModel(bool slim) =>
        slim && TryResolveVariant(EntityPlaceholderMesher.PlayerEntityTypeId, SlimPlayerVariantKey) is { } slimModel
            ? slimModel
            : Resolve(EntityPlaceholderMesher.PlayerEntityTypeId);

    private static readonly Dictionary<(int TypeId, string Name, bool Baby), EntityModelDefinition> EquippedModels = new();

    private static EntityModelDefinition Decorate(int typeId, bool baby, EntityModelDefinition definition)
    {
        var layers = EquipmentLayers(typeId);
        if (layers.Length == 0 && typeId != WitchModelDefinition.WitchEntityTypeId)
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

        var baseCompiled = definition.Compiled;
        IReadOnlyList<EntityRenderLayerDefinition>? renderLayers = null;
        var babyArmorLayers = baby &&
            (ReferenceEquals(layers, HumanoidArmorLayers) || ReferenceEquals(layers, PiglinArmorLayers));
        if ((!baby || babyArmorLayers) && baseCompiled is not null && layers.Length > 0)
        {
            var baseLayerModel = new EntityModelDefinition(
                definition.Name,
                definition.TextureWidth,
                definition.TextureHeight,
                definition.Parts,
                definition.FaceTransforms,
                definition.TexturePath,
                definition.Scale,
                definition.EmissiveTexturePath,
                definition.TextureSelector,
                definition.AdditionalTexturePaths,
                compiled: baseCompiled,
                baselinePx: definition.BaselinePx,
                rawSourceFrame: definition.RawSourceFrame,
                sourceFrameBaselinePx: definition.SourceFrameBaselinePx);
            var customLayers = new List<EntityRenderLayerDefinition>();
            foreach (var baseLayer in definition.RenderLayers)
            {
                if (baseLayer.Kind == EntityGeometryLayerKind.Elytra)
                {
                    continue;
                }

                customLayers.Add(new EntityRenderLayerDefinition(
                    baseLayer.Name,
                    baseLayer.Kind,
                    baseLayer.Order,
                    baseLayerModel,
                    definition.TexturePath,
                    definition.TextureSelector));
            }

            var order = 10;
            var piglin = typeId is (PiglinModelDefinition.PiglinEntityTypeId
                or PiglinModelDefinition.PiglinBruteEntityTypeId
                or PiglinModelDefinition.ZombifiedPiglinEntityTypeId);
            foreach (var (kind, slot, fallback) in layers)
            {
                var effectiveKind = baby && kind is (EquipmentAssetKind.HumanoidArmor or EquipmentAssetKind.HumanoidLeggings)
                    ? EquipmentAssetKind.HumanoidArmorBaby
                    : kind;
                if (EquipmentModelTrees.For(
                        effectiveKind,
                        slot,
                        player: typeId == EntityPlaceholderMesher.PlayerEntityTypeId,
                        baby: baby,
                        piglin: piglin) is not { } equipmentTree)
                {
                    continue;
                }

                var fixedTexture = typeId == 138 && kind == EquipmentAssetKind.LlamaBody
                    ? "entity/equipment/llama_body/trader_llama"
                    : null;
                var layerTexture = baby && effectiveKind == EquipmentAssetKind.HumanoidArmorBaby
                    ? "entity/equipment/humanoid_baby/iron"
                    : fixedTexture ?? fallback;
                Func<EntityRenderState, string?> selector = fixedTexture is null
                    ? state => EntityModelResolver.ResolveEquipmentTexture(state, effectiveKind, slot)
                    : _ => fixedTexture;
                var layerName = "equipment_" + effectiveKind + "_" + slot;
                var rawAdultHumanoidArmor = !baby && !piglin
                    && typeId is EntityPlaceholderMesher.PlayerEntityTypeId
                        or HumanoidMobModelDefinition.ZombieEntityTypeId
                        or HumanoidMobModelDefinition.SkeletonEntityTypeId
                        or HumanoidMobModelDefinition.DrownedEntityTypeId
                        or HumanoidMobModelDefinition.BoggedEntityTypeId;
                var rawBabyHumanoidArmor = baby && !piglin
                    && effectiveKind == EquipmentAssetKind.HumanoidArmorBaby;
                var rawArmor = rawAdultHumanoidArmor
                    ? PlayerArmorModelDefinition.ForSlot(slot)
                    : rawBabyHumanoidArmor
                        ? BabyHumanoidArmorModelDefinition.ForSlot(slot)
                        : typeId == HumanoidMobModelDefinition.HuskEntityTypeId
                            ? HuskArmorModelDefinition.ForSlot(slot)
                            : typeId == GiantModelDefinition.GiantEntityTypeId
                                ? GiantArmorModelDefinition.ForSlot(slot)
                                : typeId == WolfModelDefinition.WolfEntityTypeId
                                    && effectiveKind == EquipmentAssetKind.WolfBody
                                    ? WolfArmorModelDefinition.Definition
                                    : piglin
                                        ? PiglinArmorModelDefinition.ForSlot(slot, baby)
                                        : null;
                var layerModel = rawArmor is null
                    ? new EntityModelDefinition(
                        layerName,
                        definition.TextureWidth,
                        definition.TextureHeight,
                        [],
                        PlayerModelDefinition.DefaultFaceTransforms,
                        layerTexture,
                        definition.Scale,
                        textureSelector: selector,
                        additionalTexturePaths: [layerTexture],
                        compiled: EntityModelLayerHelpers.WithTint(
                            equipmentTree,
                            state => EntityModelResolver.ResolveEquipmentTint(state, effectiveKind, slot)),
                        baselinePx: 24f,
                        rawSourceFrame: false,
                        sourceFrameBaselinePx: 24f)
                    : new EntityModelDefinition(
                        layerName,
                        rawArmor.TextureWidth,
                        rawArmor.TextureHeight,
                        rawArmor.Parts,
                        faceTransforms: null,
                        layerTexture,
                        rawArmor.Scale,
                        textureSelector: selector,
                        additionalTexturePaths: [layerTexture],
                        compiled: EntityModelLayerHelpers.WithTint(
                            rawArmor.Compiled!,
                            state => EntityModelResolver.ResolveEquipmentTint(state, effectiveKind, slot)),
                        baselinePx: 0f,
                        rawSourceFrame: true,
                        sourceFrameBaselinePx: 0f);
                customLayers.Add(new EntityRenderLayerDefinition(
                    layerName,
                    EntityGeometryLayerKind.Elytra,
                    order++,
                    layerModel,
                    layerTexture,
                    selector,
                    state => selector(state) is not null,
                    AllCubes: true));

                var trimLayerType = effectiveKind switch
                {
                    EquipmentAssetKind.HumanoidArmor => EquipmentLayerType.Humanoid,
                    EquipmentAssetKind.HumanoidLeggings => EquipmentLayerType.HumanoidLeggings,
                    _ => (EquipmentLayerType?)null
                };
                if (trimLayerType is { } trimType)
                {
                    Func<EntityRenderState, string?> trimSelector = state => ResolveTrimTexture(state, trimType, slot);
                    var trimName = "trim_" + effectiveKind + "_" + slot;
                    var trimModel = new EntityModelDefinition(
                        trimName,
                        definition.TextureWidth,
                        definition.TextureHeight,
                        [],
                        PlayerModelDefinition.DefaultFaceTransforms,
                        texturePath: null,
                        definition.Scale,
                        textureSelector: trimSelector,
                        compiled: equipmentTree,
                        baselinePx: 24f,
                        rawSourceFrame: false,
                        sourceFrameBaselinePx: 24f);
                    customLayers.Add(new EntityRenderLayerDefinition(
                        trimName,
                        EntityGeometryLayerKind.Elytra,
                        order++,
                        trimModel,
                        null,
                        trimSelector,
                        state => trimSelector(state) is not null,
                        AllCubes: true));
                }

                const string glintTexture = "misc/enchanted_glint_armor";
                var glintModel = new EntityModelDefinition(
                    "glint_" + effectiveKind + "_" + slot,
                    definition.TextureWidth,
                    definition.TextureHeight,
                    [],
                    PlayerModelDefinition.DefaultFaceTransforms,
                    glintTexture,
                    definition.Scale,
                    emissiveTexturePath: glintTexture,
                    textureSelector: state => ResolveEquipmentFoil(state, slot) ? glintTexture : null,
                    additionalTexturePaths: [glintTexture],
                    compiled: EntityModelLayerHelpers.WithEmissiveTexture(equipmentTree, glintTexture),
                    baselinePx: 24f,
                    rawSourceFrame: false,
                    sourceFrameBaselinePx: 24f);
                customLayers.Add(new EntityRenderLayerDefinition(
                    "glint_" + effectiveKind + "_" + slot,
                    EntityGeometryLayerKind.Glint,
                    order++,
                    glintModel,
                    glintTexture,
                    state => ResolveEquipmentFoil(state, slot) ? glintTexture : null,
                    state => ResolveEquipmentFoil(state, slot),
                    AllCubes: true));
            }

            if (typeId == 152 && EquipmentModelTrees.For(EquipmentAssetKind.WolfBody) is { } wolfArmor)
            {
                Func<EntityRenderState, string?> crackSelector = static state => state.Crackiness switch
                {
                    1 => "entity/wolf/wolf_armor_crackiness_low",
                    2 => "entity/wolf/wolf_armor_crackiness_medium",
                    3 => "entity/wolf/wolf_armor_crackiness_high",
                    _ => null
                };
                var crackinessModel = new EntityModelDefinition(
                    "crackiness",
                    definition.TextureWidth,
                    definition.TextureHeight,
                    [],
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/wolf/wolf_armor_crackiness_low",
                    definition.Scale,
                    textureSelector: crackSelector,
                    additionalTexturePaths:
                    [
                        "entity/wolf/wolf_armor_crackiness_low",
                        "entity/wolf/wolf_armor_crackiness_medium",
                        "entity/wolf/wolf_armor_crackiness_high"
                    ],
                    compiled: wolfArmor,
                    baselinePx: 24f,
                    rawSourceFrame: false,
                    sourceFrameBaselinePx: 24f);
                customLayers.Add(new EntityRenderLayerDefinition(
                    "crackiness",
                    EntityGeometryLayerKind.Elytra,
                    order++,
                    crackinessModel,
                    "entity/wolf/wolf_armor_crackiness_low",
                    crackSelector,
                    static state => state.Crackiness > 0,
                    AllCubes: true));
            }

            if (ReferenceEquals(layers, HumanoidArmorLayers))
            {
                var elytraCompiled = EntityModelLayerHelpers.BuildElytraLayer(baseCompiled, baby);
                var elytraModel = new EntityModelDefinition(
                    "elytra",
                    definition.TextureWidth,
                    definition.TextureHeight,
                    [],
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/equipment/wings/elytra",
                    definition.Scale,
                    textureSelector: state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4),
                    additionalTexturePaths: ["entity/equipment/wings/elytra"],
                    compiled: elytraCompiled,
                    baselinePx: 0f,
                    rawSourceFrame: true,
                    sourceFrameBaselinePx: 0f);
                customLayers.Add(new EntityRenderLayerDefinition(
                    "elytra",
                    EntityGeometryLayerKind.Elytra,
                    order,
                    elytraModel,
                    "entity/equipment/wings/elytra",
                    state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4),
                    state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4) is not null,
                    AllCubes: true));
            }

            renderLayers = customLayers;
        }

        if (typeId == WitchModelDefinition.WitchEntityTypeId)
        {
            var existing = renderLayers ?? definition.RenderLayers;
            var witchLayers = new List<EntityRenderLayerDefinition>(existing);
            witchLayers.Add(new EntityRenderLayerDefinition(
                "held_potion",
                EntityGeometryLayerKind.Cutout,
                40,
                WitchModelDefinition.HeldPotionDefinition,
                WitchModelDefinition.PotionTexture,
                static _ => WitchModelDefinition.PotionTexture,
                static state => state.IsHoldingPotion,
                AllCubes: true,
                UsesItemAtlas: true));
            renderLayers = witchLayers;
        }

        if (ReferenceEquals(layers, HumanoidArmorLayers))
        {
            var existing = renderLayers ?? definition.RenderLayers;
            var heldLayers = new List<EntityRenderLayerDefinition>(existing);
            AddSpecialHandLayers(heldLayers, "trident", "minecraft:trident", SpecialItemModelDefinition.HeldTridentRight, SpecialItemModelDefinition.HeldTridentLeft, SpecialItemModelDefinition.TridentTexture,
                static (_, _) => SpecialItemModelDefinition.TridentTexture);
            ApplyShieldHeldTransforms();
            AddSpecialHandLayers(heldLayers, "shield", "minecraft:shield", SpecialItemModelDefinition.HeldShieldRight, SpecialItemModelDefinition.HeldShieldLeft, SpecialItemModelDefinition.ShieldTexture,
                ResolveHeldItemSpecialTexture);
            AddSpecialHeadLayers(heldLayers);
            AddSpecialChestLayers(heldLayers);
            AddSpecialShulkerLayers(heldLayers);
            AddSpecialBannerPotConduitLayers(heldLayers);
            renderLayers = heldLayers;
        }

        if (typeId == HappyGhastModelDefinition.HappyGhastEntityTypeId)
        {
            var ropeModel = baby
                ? HappyGhastModelDefinition.RopesBabyDefinition
                : HappyGhastModelDefinition.RopesDefinition;
            var existing = renderLayers ?? definition.RenderLayers;
            var ropeLayers = new List<EntityRenderLayerDefinition>(existing);
            ropeLayers.Add(new EntityRenderLayerDefinition(
                "happy_ghast_ropes",
                EntityGeometryLayerKind.Elytra,
                20,
                ropeModel,
                HappyGhastModelDefinition.RopesTexture,
                _ => HappyGhastModelDefinition.RopesTexture,
                state => state.IsLeashed && IsHarnessItem(state.Equipment.Body),
                AllCubes: true));
            renderLayers = ropeLayers;
        }

        var compiled = definition.Compiled;
        if (compiled is not null)
        {
            foreach (var (kind, slot, fallback) in layers)
            {
                var effectiveKind = baby && kind is (EquipmentAssetKind.HumanoidArmor or EquipmentAssetKind.HumanoidLeggings)
                    ? EquipmentAssetKind.HumanoidArmorBaby
                    : kind;
                var equipmentTree = effectiveKind is (EquipmentAssetKind.HumanoidArmor
                    or EquipmentAssetKind.HumanoidLeggings
                    or EquipmentAssetKind.HumanoidArmorBaby)
                    ? EquipmentModelTrees.For(
                        effectiveKind,
                        slot,
                        player: !baby && typeId == EntityPlaceholderMesher.PlayerEntityTypeId,
                        baby: baby,
                        piglin: typeId is (PiglinModelDefinition.PiglinEntityTypeId
                            or PiglinModelDefinition.PiglinBruteEntityTypeId
                            or PiglinModelDefinition.ZombifiedPiglinEntityTypeId))
                    : EquipmentModelTrees.For(kind);
                if (equipmentTree is { })
                {
                    var fixedTexture = typeId == 138 && kind == EquipmentAssetKind.LlamaBody
                        ? "entity/equipment/llama_body/trader_llama"
                        : null;
                    compiled = EntityModelLayerHelpers.WithEquipment(
                        compiled,
                        equipmentTree,
                        effectiveKind,
                        slot,
                        baby ? "entity/equipment/humanoid_baby/iron" : fallback,
                        fixedTexture: fixedTexture);
                }
            }

            if (typeId == 152 && EquipmentModelTrees.For(EquipmentAssetKind.WolfBody) is { } wolfArmor)
            {
                compiled = EntityModelLayerHelpers.WithCrackiness(compiled, wolfArmor);
            }

            if (ReferenceEquals(layers, HumanoidArmorLayers))
            {
                compiled = EntityModelLayerHelpers.WithElytra(compiled, baby);
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
            compiled: compiled,
            renderLayers: renderLayers,
            baselinePx: definition.BaselinePx,
            rawSourceFrame: definition.RawSourceFrame,
            sourceFrameBaselinePx: definition.SourceFrameBaselinePx);
        EquippedModels[key] = decorated;
        return decorated;
    }

    /// <summary>
    /// Bakes the reference held-item chain into the special shield meshes.
    /// </summary>
    /// <remarks>
    /// The reference submits the shield model inside the entity pose stack, so its world matrix is
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
        RegisterVariant(EntityPlaceholderMesher.PlayerEntityTypeId, SlimPlayerVariantKey, PlayerModelDefinition.SlimDefinition);
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


