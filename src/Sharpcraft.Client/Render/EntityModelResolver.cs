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
public static class EntityModelResolver
{
    /// <summary>Item identity table used by equipment layer selectors; defaults to the embedded baseline snapshot.</summary>
    public static ItemRegistry? Items { get; set; } = ItemRegistry.LoadEmbedded();

    /// <summary>Item model resolver installed by the window host; used by held-item layer selectors.</summary>
    public static ItemModelResolver? ItemModels { get; set; }

    /// <summary>Item tag table loaded from the extracted pack; empty until the launcher installs it.</summary>
    public static ItemTagRegistry ItemTags { get; set; } = ItemTagRegistry.Empty;

    /// <summary>
    /// Resolves the full equipment texture metadata (primary/overlay paths, dye, player texture and glint) for one
    /// render state slot, or null when the slot is empty/unsupported. Callers that need more than a path should use
    /// this instead of <see cref="ResolveEquipmentTexture"/>.
    /// </summary>
    public static EquipmentTexture? ResolveEquipmentTextureInfo(
        EntityRenderState state,
        EquipmentAssetKind kind,
        int slot)
    {
        var itemId = state.Equipment[slot];
        if (itemId < 0 || Items is null || Items.ById(itemId) is not { } item)
        {
            return null;
        }

        return EquipmentTextureCatalog.Resolve(
            kind,
            item,
            equippableAssetId: state.EquipmentAssetIdAt(slot),
            glintOverride: state.EquipmentGlintOverrideAt(slot));
    }

    /// <summary>Resolves a dynamic equipment texture for a render state slot, or null when the slot is empty/unsupported.</summary>
    public static string? ResolveEquipmentTexture(
        EntityRenderState state,
        EquipmentAssetKind kind,
        int slot,
        bool overlay = false)
    {
        var texture = ResolveEquipmentTextureInfo(state, kind, slot);
        if (overlay)
        {
            return texture?.OverlayPath;
        }

        // EquipmentLayerRenderer replaces the first layer's texture with the avatar texture when the layer opts in;
        // only WingsLayer supplies that override, so the substitution is limited to the elytra equipment family.
        return kind == EquipmentAssetKind.Elytra &&
               texture is { UsePlayerTexture: true } &&
               state.PlayerElytraOverride is { Length: > 0 } playerTexture
            ? playerTexture
            : texture?.PrimaryPath;
    }

    /// <summary>
    /// Resolves the palette-mapped trim texture for one humanoid armor slot, or null when the item has no trim.
    /// </summary>
    public static string? ResolveTrimTexture(EntityRenderState state, EquipmentLayerType layerType, int slot)
    {
        var pattern = state.EquipmentTrimPatternAt(slot);
        var material = state.EquipmentTrimMaterialAt(slot);
        if (pattern is null || material is null)
        {
            return null;
        }

        EquipmentClientInfo? equipmentInfo = null;
        if (state.EquipmentAssetIdAt(slot) is { } assetId && Identifier.TryParse(assetId, out var equipmentAsset))
        {
            equipmentInfo = EquipmentTextureCatalog.Assets.Get(equipmentAsset);
        }

        return TrimTextureCatalog.Path(layerType, pattern, material, equipmentInfo);
    }

    /// <summary>
    /// Resolves the item's foil state for the glint layer: an explicit glint override wins, otherwise enchantment
    /// presence enables the overlay.
    /// </summary>
    public static bool ResolveEquipmentFoil(EntityRenderState state, int slot) =>
        state.EquipmentGlintOverrideAt(slot) ?? state.EquipmentHasEnchantmentsAt(slot) ?? false;

    /// <summary>Resolves one hand's special renderer type, or null when the item is not a special model.</summary>
    public static string? ResolveHeldItemSpecial(EntityRenderState state, bool offhand = false)
    {
        var itemId = offhand ? state.OffHandItemId : state.MainHandItemId;
        if (ItemModels is null || Items?.ById(itemId) is not { } item)
        {
            return null;
        }

        return ItemModels.Resolve(item.Id.ToString())?.SpecialRendererType;
    }

    /// <summary>Resolves one hand's special-renderer string properties, or null when not a special model.</summary>
    public static IReadOnlyDictionary<string, string>? ResolveHeldItemSpecialProperties(EntityRenderState state, bool offhand = false)
    {
        var itemId = offhand ? state.OffHandItemId : state.MainHandItemId;
        if (ItemModels is null || Items?.ById(itemId) is not { } item)
        {
            return null;
        }

        return ItemModels.Resolve(item.Id.ToString())?.SpecialProperties;
    }

    /// <summary>Resolves the atlas texture for skull/chest/banner/pot/conduit/end-cube special models.</summary>
    public static string? ResolveHeldItemSpecialTexture(EntityRenderState state, bool offhand = false)
    {
        var specialType = ResolveHeldItemSpecial(state, offhand);
        var properties = ResolveHeldItemSpecialProperties(state, offhand);
        switch (specialType)
        {
            case "minecraft:chest":
            case "minecraft:shulker_box":
            {
                var fallback = specialType == "minecraft:chest" ? "minecraft:normal" : "minecraft:shulker";
                var texture = properties is not null && properties.TryGetValue("texture", out var value)
                    ? value
                    : fallback;
                if (!Identifier.TryParse(texture, out var textureId))
                {
                    return null;
                }

                return (specialType == "minecraft:chest" ? "entity/chest/" : "entity/shulker/") + textureId.Path;
            }

            case "minecraft:banner":
                return SpecialItemModelDefinition.BannerTexture;
            case "minecraft:shield":
                // The reference picks the patterned base sprite when patterns or a base colour are present.
                return SpecialItemModelDefinition.ShieldSprite(
                    state.EquipmentHasBannerPatternsAt(offhand ? 1 : 0) ?? false);
            case "minecraft:decorated_pot":
                return SpecialItemModelDefinition.DecoratedPotTexture;
            case "minecraft:conduit":
                return SpecialItemModelDefinition.ConduitTexture;
            case "minecraft:end_cube":
                return SpecialItemModelDefinition.EndPortalTexture;
            case "minecraft:head":
            case "minecraft:player_head":
                break;
            default:
                return null;
        }

        var kind = properties is not null && properties.TryGetValue("kind", out var kindValue) ? kindValue : "player";
        return kind switch
        {
            "skeleton" => "entity/skeleton/skeleton",
            "wither_skeleton" => "entity/skeleton/wither_skeleton",
            "zombie" => "entity/zombie/zombie",
            "creeper" => "entity/creeper/creeper",
            "dragon" => "entity/enderdragon/dragon",
            "piglin" => "entity/piglin/piglin",
            _ => DefaultPlayerSkin.Get(state.Uuid).Path
        };
    }

    /// <summary>Decorated pot sides sample their own pottery-pattern sprite.</summary>
    public static string? ResolveHeldItemPotSideTexture(EntityRenderState state, bool offhand = false) =>
        ResolveHeldItemSpecial(state, offhand) == "minecraft:decorated_pot"
            ? SpecialItemModelDefinition.DecoratedPotSideTexture
            : null;

    /// <summary>Decoded banner pattern layers for the item in one hand, or null when it carries none.</summary>
    public static IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>? ResolveHeldItemBannerPatterns(
        EntityRenderState state,
        bool offhand = false) =>
        state.EquipmentBannerPatternsAt(offhand ? 1 : 0);

    /// <summary>Atlas sprites a held banner or shield needs for its pattern layers.</summary>
    public static IReadOnlyList<string> ResolveHeldItemPatternSprites(EntityRenderState state, bool offhand = false)
    {
        var layers = ResolveHeldItemBannerPatterns(state, offhand);
        if (layers is null || layers.Count == 0)
        {
            return [];
        }

        var prefix = ResolveHeldItemSpecial(state, offhand) == "minecraft:shield" ? "entity/shield/" : "entity/banner/";
        var result = new List<string>(layers.Count);
        foreach (var layer in layers)
        {
            if (layer.PatternAssetId is { Length: > 0 } asset)
            {
                result.Add(prefix + asset);
            }
        }

        return result;
    }

    /// <summary>Resolves one hand's first item-atlas texture path, or null when unknown.</summary>
    public static string? ResolveHeldItemTexture(EntityRenderState state, bool offhand = false)
    {
        var itemId = offhand ? state.OffHandItemId : state.MainHandItemId;
        if (ItemModels is null || Items?.ById(itemId) is not { } item)
        {
            return null;
        }

        var model = ItemModels.Resolve(item.Id.ToString());
        if (model is null || model.SpecialRendererType is not null)
        {
            return null;
        }

        var texture = model.Textures.TryGetValue("layer1", out var layer1)
            ? layer1
            : model.Textures.TryGetValue("layer0", out var layer0) ? layer0 : null;
        if (texture is null || !Identifier.TryParse(texture, out var identifier))
        {
            return null;
        }

        return identifier.Path;
    }

    /// <summary>Resolves one hand's item tint (tint index 0), or white when the item has no tint source.</summary>
    public static Vector3 ResolveHeldItemTint(EntityRenderState state, bool offhand = false)
    {
        var itemId = offhand ? state.OffHandItemId : state.MainHandItemId;
        if (ItemModels is null || Items?.ById(itemId) is not { } item)
        {
            return Vector3.One;
        }

        var model = ItemModels.Resolve(item.Id.ToString());
        return model is null || model.SpecialRendererType is not null
            ? Vector3.One
            : TintOf(model, ItemModels.GrassColormap);
    }

    /// <summary>Tint index 0 of a resolved model, or white when it declares no tint source.</summary>
    public static Vector3 TintOf(ItemModelResolution? model, GrassColorMap? grass) =>
        model is null ? Vector3.One : model.TintFor(0, grass);

    /// <summary>Resolves the leather/dyed tint for an equipment slot, or white when the item is not dyeable.</summary>
    public static Vector3 ResolveEquipmentTint(EntityRenderState state, EquipmentAssetKind kind, int slot)
    {
        var itemId = state.Equipment[slot];
        if (itemId < 0 || Items is null || Items.ById(itemId) is not { } item)
        {
            return Vector3.One;
        }

        var texture = EquipmentTextureCatalog.Resolve(kind, item, equippableAssetId: state.EquipmentAssetIdAt(slot));
        if ((state.EquipmentDyeColorAt(slot) ?? texture?.ColourWhenUndyed) is not { } rgb)
        {
            return Vector3.One;
        }

        return new Vector3(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
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
                compiled: baseCompiled);
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
                var layerModel = new EntityModelDefinition(
                    layerName,
                    definition.TextureWidth,
                    definition.TextureHeight,
                    [],
                    definition.FaceTransforms,
                    layerTexture,
                    definition.Scale,
                    textureSelector: selector,
                    additionalTexturePaths: [layerTexture],
                    compiled: EntityModelLayerHelpers.WithTint(
                        equipmentTree,
                        state => EntityModelResolver.ResolveEquipmentTint(state, effectiveKind, slot)));
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
                        definition.FaceTransforms,
                        texturePath: null,
                        definition.Scale,
                        textureSelector: trimSelector,
                        compiled: equipmentTree);
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
                    definition.FaceTransforms,
                    glintTexture,
                    definition.Scale,
                    emissiveTexturePath: glintTexture,
                    textureSelector: state => ResolveEquipmentFoil(state, slot) ? glintTexture : null,
                    additionalTexturePaths: [glintTexture],
                    compiled: EntityModelLayerHelpers.WithEmissiveTexture(equipmentTree, glintTexture));
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
                    definition.FaceTransforms,
                    "entity/wolf/wolf_armor_crackiness_low",
                    definition.Scale,
                    textureSelector: crackSelector,
                    additionalTexturePaths:
                    [
                        "entity/wolf/wolf_armor_crackiness_low",
                        "entity/wolf/wolf_armor_crackiness_medium",
                        "entity/wolf/wolf_armor_crackiness_high"
                    ],
                    compiled: wolfArmor);
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
                    definition.FaceTransforms,
                    "entity/equipment/wings/elytra",
                    definition.Scale,
                    textureSelector: state => EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4),
                    additionalTexturePaths: ["entity/equipment/wings/elytra"],
                    compiled: elytraCompiled);
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
            heldLayers.Add(new EntityRenderLayerDefinition(
                "held_item_right",
                EntityGeometryLayerKind.Cutout,
                50,
                baby ? HeldItemModelDefinition.BabyDefinition : HeldItemModelDefinition.Definition,
                TexturePath: null,
                TextureSelector: state => ResolveHeldItemTexture(state, offhand: false),
                VisibleWhen: state => ResolveHeldItemTexture(state, offhand: false) is not null,
                AllCubes: true,
                UsesItemAtlas: true));
            heldLayers.Add(new EntityRenderLayerDefinition(
                "held_item_left",
                EntityGeometryLayerKind.Cutout,
                51,
                baby ? HeldItemModelDefinition.BabyLeftDefinition : HeldItemModelDefinition.LeftDefinition,
                TexturePath: null,
                TextureSelector: state => ResolveHeldItemTexture(state, offhand: true),
                VisibleWhen: state => ResolveHeldItemTexture(state, offhand: true) is not null,
                AllCubes: true,
                UsesItemAtlas: true));
            AddSpecialHandLayers(heldLayers, "trident", "minecraft:trident", SpecialItemModelDefinition.HeldTridentRight, SpecialItemModelDefinition.HeldTridentLeft, SpecialItemModelDefinition.TridentTexture,
                static (_, _) => SpecialItemModelDefinition.TridentTexture);
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
            renderLayers: renderLayers);
        EquippedModels[key] = decorated;
        return decorated;
    }

    private static void AddSpecialHandLayers(
        List<EntityRenderLayerDefinition> layers,
        string name,
        string specialType,
        EntityModelDefinition right,
        EntityModelDefinition left,
        string staticTexture,
        Func<EntityRenderState, bool, string?> selector)
    {
        var order = 52 + (layers.Count * 2);
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_" + name + "_right",
            EntityGeometryLayerKind.Cutout,
            order,
            right,
            staticTexture,
            state => selector(state, false),
            state => ResolveHeldItemSpecial(state, offhand: false) == specialType,
            AllCubes: true,
            UsesItemAtlas: true));
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_" + name + "_left",
            EntityGeometryLayerKind.Cutout,
            order + 1,
            left,
            staticTexture,
            state => selector(state, true),
            state => ResolveHeldItemSpecial(state, offhand: true) == specialType,
            AllCubes: true,
            UsesItemAtlas: true));
    }

    private static void AddSpecialHeadLayers(List<EntityRenderLayerDefinition> layers)
    {
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_head_right",
            EntityGeometryLayerKind.Cutout,
            60,
            SpecialItemModelDefinition.HeldSkullRight,
            TexturePath: null,
            TextureSelector: state => ResolveHeldItemSpecialTexture(state, offhand: false),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: false) is "minecraft:head" or "minecraft:player_head",
            AllCubes: true,
            UsesItemAtlas: true));
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_head_left",
            EntityGeometryLayerKind.Cutout,
            61,
            SpecialItemModelDefinition.HeldSkullLeft,
            TexturePath: null,
            TextureSelector: state => ResolveHeldItemSpecialTexture(state, offhand: true),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: true) is "minecraft:head" or "minecraft:player_head",
            AllCubes: true,
            UsesItemAtlas: true));
    }

    private static void AddSpecialChestLayers(List<EntityRenderLayerDefinition> layers)
    {
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_chest_right",
            EntityGeometryLayerKind.Cutout,
            62,
            SpecialItemModelDefinition.HeldChestRight,
            TexturePath: null,
            TextureSelector: state => ResolveHeldItemSpecialTexture(state, offhand: false),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: false) == "minecraft:chest",
            AllCubes: true,
            UsesItemAtlas: true));
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_chest_left",
            EntityGeometryLayerKind.Cutout,
            63,
            SpecialItemModelDefinition.HeldChestLeft,
            TexturePath: null,
            TextureSelector: state => ResolveHeldItemSpecialTexture(state, offhand: true),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: true) == "minecraft:chest",
            AllCubes: true,
            UsesItemAtlas: true));
    }

    private static void AddSpecialShulkerLayers(List<EntityRenderLayerDefinition> layers)
    {
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_shulker_right",
            EntityGeometryLayerKind.Cutout,
            64,
            SpecialItemModelDefinition.HeldShulkerRight,
            TexturePath: null,
            TextureSelector: state => ResolveHeldItemSpecialTexture(state, offhand: false),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: false) == "minecraft:shulker_box",
            AllCubes: true,
            UsesItemAtlas: true));
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_shulker_left",
            EntityGeometryLayerKind.Cutout,
            65,
            SpecialItemModelDefinition.HeldShulkerLeft,
            TexturePath: null,
            TextureSelector: state => ResolveHeldItemSpecialTexture(state, offhand: true),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: true) == "minecraft:shulker_box",
            AllCubes: true,
            UsesItemAtlas: true));
    }

    /// <summary>
    /// Adds one special-model layer pair. <paramref name="selector"/> receives the off-hand flag so callers can reuse a
    /// single texture resolver for both hands.
    /// </summary>
    private static void AddSpecialPair(
        List<EntityRenderLayerDefinition> layers,
        string name,
        string specialType,
        EntityModelDefinition right,
        EntityModelDefinition left,
        int order,
        Func<EntityRenderState, bool, string?> selector)
    {
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_" + name + "_right",
            EntityGeometryLayerKind.Cutout,
            order,
            right,
            TexturePath: null,
            TextureSelector: state => selector(state, false),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: false) == specialType,
            AllCubes: true,
            UsesItemAtlas: true));
        layers.Add(new EntityRenderLayerDefinition(
            "held_special_" + name + "_left",
            EntityGeometryLayerKind.Cutout,
            order + 1,
            left,
            TexturePath: null,
            TextureSelector: state => selector(state, true),
            VisibleWhen: state => ResolveHeldItemSpecial(state, offhand: true) == specialType,
            AllCubes: true,
            UsesItemAtlas: true));
    }

    private static void AddSpecialBannerPotConduitLayers(List<EntityRenderLayerDefinition> layers)
    {
        // Banner body and flag are two reference models sharing the banner_base sprite.
        AddSpecialPair(layers, "banner", "minecraft:banner",
            SpecialItemModelDefinition.BannerStanding, SpecialItemModelDefinition.BannerStanding, 66,
            (state, offhand) => ResolveHeldItemSpecialTexture(state, offhand));
        AddSpecialPair(layers, "banner_flag", "minecraft:banner",
            SpecialItemModelDefinition.BannerFlagStanding, SpecialItemModelDefinition.BannerFlagStanding, 68,
            (state, offhand) => ResolveHeldItemSpecialTexture(state, offhand));
        AddSpecialPair(layers, "banner_patterns", "minecraft:banner",
            SpecialItemModelDefinition.BannerFlagPatternsRight, SpecialItemModelDefinition.BannerFlagPatternsLeft, 80,
            (state, offhand) => ResolveHeldItemPatternSprites(state, offhand).Count > 0
                ? SpecialItemModelDefinition.BannerTexture
                : null);
        AddSpecialPair(layers, "shield_patterns", "minecraft:shield",
            SpecialItemModelDefinition.ShieldPatternsRight, SpecialItemModelDefinition.ShieldPatternsLeft, 82,
            (state, offhand) => ResolveHeldItemPatternSprites(state, offhand).Count > 0
                ? SpecialItemModelDefinition.ShieldTexture
                : null);
        // Decorated pot uses a base sprite plus a per-pattern side sprite.
        AddSpecialPair(layers, "pot", "minecraft:decorated_pot",
            SpecialItemModelDefinition.DecoratedPot, SpecialItemModelDefinition.DecoratedPot, 70,
            (state, offhand) => ResolveHeldItemSpecialTexture(state, offhand));
        AddSpecialPair(layers, "pot_sides", "minecraft:decorated_pot",
            SpecialItemModelDefinition.DecoratedPotSides, SpecialItemModelDefinition.DecoratedPotSides, 72,
            (state, offhand) => ResolveHeldItemPotSideTexture(state, offhand));
        AddSpecialPair(layers, "conduit", "minecraft:conduit",
            SpecialItemModelDefinition.Conduit, SpecialItemModelDefinition.Conduit, 74,
            (state, offhand) => ResolveHeldItemSpecialTexture(state, offhand));
        AddSpecialPair(layers, "end_cube", "minecraft:end_cube",
            SpecialItemModelDefinition.EndCube, SpecialItemModelDefinition.EndCube, 76,
            (state, offhand) => ResolveHeldItemSpecialTexture(state, offhand));
    }

    private static bool IsHarnessItem(int itemId) =>
        Items?.ById(itemId) is { } item && item.Id.Path.EndsWith("_harness", StringComparison.Ordinal);

    private static readonly int[] HumanoidArmorSlots = [5, 4, 3, 2];

    private static readonly (EquipmentAssetKind Kind, int Slot, string Fallback)[] HumanoidArmorLayers =
    [
        (EquipmentAssetKind.HumanoidArmor, 5, "entity/equipment/humanoid/iron"),
        (EquipmentAssetKind.HumanoidArmor, 4, "entity/equipment/humanoid/iron"),
        (EquipmentAssetKind.HumanoidLeggings, 3, "entity/equipment/humanoid_leggings/iron"),
        (EquipmentAssetKind.HumanoidArmor, 2, "entity/equipment/humanoid/iron")
    ];

    private static readonly (EquipmentAssetKind Kind, int Slot, string Fallback)[] PiglinArmorLayers =
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
        102 or 103 or 158 => PiglinArmorLayers,
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


