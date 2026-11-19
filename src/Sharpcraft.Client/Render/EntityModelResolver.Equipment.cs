// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;


public static partial class EntityModelResolver
{
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

    /// <summary>Glint sampler for held special models that use the entity-scale glint pass.</summary>
    public static string? ResolveHeldItemSpecialGlintTexture(EntityRenderState state, bool offhand = false) =>
        (offhand ? state.OffHandFoil : state.MainHandFoil) != ItemFoilType.None
        && ResolveHeldItemSpecial(state, offhand) is "minecraft:trident" or "minecraft:shield"
            ? ItemInHandMesher.GlintTexture
            : null;

    /// <summary>Decoded banner pattern layers for the item in one hand, or null when it carries none.</summary>
    public static IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>? ResolveHeldItemBannerPatterns(
        EntityRenderState state,
        bool offhand = false) =>
        state.EquipmentBannerPatternsAt(offhand ? 1 : 0);

    /// <summary>
    /// Base-colour dye id for a held banner/shield pattern overlay. Banners take the item model `color` property
    /// (BannerSpecialRenderer.Unbaked.baseColor); shields take the stack BASE_COLOR component and fall back to white,
    /// matching ShieldSpecialRenderer.submitPatterns(Objects.requireNonNullElse(baseColor, DyeColor.WHITE)).
    /// </summary>
    public static int ResolveHeldItemBannerBaseColor(EntityRenderState state, bool offhand = false)
    {
        if (ResolveHeldItemSpecial(state, offhand) == "minecraft:shield")
        {
            return state.EquipmentBannerBaseColorAt(offhand ? 1 : 0) ?? 0;
        }

        var properties = ResolveHeldItemSpecialProperties(state, offhand);
        return properties is not null && properties.TryGetValue("color", out var name)
            ? DyeColors.IdFromName(name) ?? 0
            : 0;
    }

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

    /// <summary>
    /// Resolves one hand's item model, or null when the hand is empty, the item is unknown, or the model is a special
    /// renderer (those keep their own hand layers).
    /// </summary>
    public static ItemModelResolution? ResolveHeldItemModel(EntityRenderState state, bool offhand = false)
    {
        var placements = ResolveHeldItemPlacements(state, offhand);
        return placements.Count > 0 ? placements[0].Model : null;
    }

    /// <summary>
    /// Every sub-model a held item submits. A composite item (a bed in hand is the visible case) draws all of its
    /// members, each under its own item-model transformation; models that carry a special renderer are left to the
    /// special hand layers.
    /// </summary>
    /// <param name="state">Render state of the holder.</param>
    /// <param name="offhand">True for the off hand.</param>
    public static IReadOnlyList<ItemModelPlacement> ResolveHeldItemPlacements(EntityRenderState state, bool offhand = false)
    {
        var itemId = offhand ? state.OffHandItemId : state.MainHandItemId;
        if (ItemModels is null || Items?.ById(itemId) is not { } item)
        {
            return [];
        }

        // The holder's using-item state selects between the model's condition branches (the shield switches to its
        // blocking model while in use).
        var placements = ItemModels.ResolveAll(item.Id.ToString(), state.IsUsingItem);
        if (placements.Count == 0 || placements[0].Model.SpecialRendererType is not null)
        {
            return [];
        }

        return placements;
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

    /// <summary>
    /// Held-item anchor from the hand: the reference <c>ItemInHandLayer</c> rotates XP by −90°, YP by 180°, applies the
    /// per-hand offset ((±1, 2, −10)/16 for adults, (0, 1, −4.5)/16 for babies) and finally the item's own display
    /// transform. The hand pose itself (root + arm) is already carried by the arm part the quad hangs under.
    /// </summary>
    public static Matrix4x4 ResolveHeldItemAnchor(EntityRenderState state, bool offhand = false)
    {
        var itemId = offhand ? state.OffHandItemId : state.MainHandItemId;
        ItemModelResolution? model = null;
        if (ItemModels is not null && Items?.ById(itemId) is { } item)
        {
            model = ItemModels.Resolve(item.Id.ToString());
        }

        return AnchorFor(model, offhand, state.IsBaby);
    }

    /// <summary>Model-space mirror: Sharpcraft stores models with X and Y negated relative to the reference frame.</summary>
    private static Matrix4x4 Mirror => Matrix4x4.CreateScale(-1f, -1f, 1f);

    /// <summary>The display context a hand's held item is submitted with: held items are always third person.</summary>
    public static ItemDisplayContext DisplayContextFor(bool offhand) =>
        offhand ? ItemDisplayContext.ThirdPersonLeftHand : ItemDisplayContext.ThirdPersonRightHand;

    /// <summary>Item display transform for a hand context, or identity when the model has none.</summary>
    public static Matrix4x4 DisplayFor(ItemModelResolution? model, bool offhand)
    {
        if (model is null)
        {
            return Matrix4x4.Identity;
        }

        var context = DisplayContextFor(offhand);
        // Reference LayerRenderState.applyTransform passes displayContext.leftHand() into ItemTransform.apply, which
        // negates translation.x and rotation.y/z on top of the per-context JSON entry. Omitting that fix renders offhand
        // items unmirrored — a bow lies flat instead of upright.
        return model.Display.TryGetValue(context, out var transform)
            ? transform.ToMatrix(applyLeftHandFix: offhand)
            : Matrix4x4.Identity;
    }

    /// <summary>Anchor for an already resolved model, split out so the transform can be tested without a registry.</summary>
    public static Matrix4x4 AnchorFor(ItemModelResolution? model, bool offhand, bool baby)
    {
        var display = Matrix4x4.Identity;
        if (model is not null)
        {
            var context = DisplayContextFor(offhand);
            if (model.Display.TryGetValue(context, out var transform))
            {
                display = transform.ToMatrix(applyLeftHandFix: offhand);
            }
        }

        // Reference data (probes in tools/Sharpcraft.ItemRender.Audit/truth/):
        //   ItemInHandLayer.submitArmWithItem: translateToHand → XP(-90°) → YP(180°) → T_off → item display
        //   T_off = (±1, 2, -10)/16 adult, (0, 1, -4.5)/16 baby (not armor stand), left hand mirrors X
        // The anchor is solved from reference world matrices for two poses and two display transforms; all four agree,
        // so it is pose- and item-independent. See ReferenceFrame.HeldItemAnchor.
        return display * AnchorCoreFor(offhand, baby);
    }

    /// <summary>
    /// The anchor without the item display transform, so callers that need to insert something between the display and
    /// the anchor — the STAB swing offset — can do so in the reference order <c>D · STAB · anchor · H</c>.
    /// </summary>
    public static Matrix4x4 AnchorCoreFor(bool offhand, bool baby)
    {
        var modelScale = baby ? 0.5f : 0.9f;
        var offset = baby ? ReferenceFrame.BabyOffset : ReferenceFrame.AdultOffset;
        return ReferenceFrame.HeldItemAnchor(
            new Vector3(offhand ? -offset.X : offset.X, offset.Y, offset.Z),
            modelScale);
    }
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
        // Banner body/flag are emitted by ItemInHandMesher through the raw hand chain (B-4 T3); the pattern overlay
        // still uses the resolver layer until its per-pattern sprites move to the same path.
        // Banner and shield pattern overlays are emitted by ItemInHandMesher through the same raw hand frame as the
        // body/flag/shield (#244 R-045); only the pot/conduit/end-cube specials remain on resolver layers here.
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

    private static readonly (EquipmentAssetKind Kind, int Slot, string Fallback)[] ArmorStandArmorLayers =
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
        119 => [(EquipmentAssetKind.HorseBody, 6, "entity/equipment/horse_body/iron"),
                      (EquipmentAssetKind.SkeletonHorseSaddle, 7, "entity/equipment/skeleton_horse_saddle/saddle")],
        155 => [(EquipmentAssetKind.HorseBody, 6, "entity/equipment/horse_body/iron"),
                      (EquipmentAssetKind.ZombieHorseSaddle, 7, "entity/equipment/zombie_horse_saddle/saddle")],
        19 => [(EquipmentAssetKind.CamelSaddle, 7, "entity/equipment/camel_saddle/saddle")],
        20 => [(EquipmentAssetKind.CamelHuskSaddle, 7, "entity/equipment/camel_husk_saddle/saddle")],
        101 => [(EquipmentAssetKind.PigSaddle, 7, "entity/equipment/pig_saddle/saddle")],
        132 => [(EquipmentAssetKind.StriderSaddle, 7, "entity/equipment/strider_saddle/saddle")],
        79 or 138 => [(EquipmentAssetKind.LlamaBody, 6, "entity/equipment/llama_body/white")],
        89 or 156 => [(EquipmentAssetKind.NautilusBody, 6, "entity/equipment/nautilus_body/iron"),
                      (EquipmentAssetKind.NautilusSaddle, 7, "entity/equipment/nautilus_saddle/saddle")],
        59 => [(EquipmentAssetKind.HappyGhastBody, 6, "entity/equipment/happy_ghast_body/white_harness")],
        102 or 103 or 158 => PiglinArmorLayers,
        152 => [(EquipmentAssetKind.WolfBody, 6, "entity/equipment/wolf_body/armadillo_scute")],
        5 => ArmorStandArmorLayers,
        159 => HumanoidArmorLayers,
        154 or 39 or 68 or 118 or 131 or 16 or 150 or 157 or 60 or 98 => HumanoidArmorLayers,
        _ => []
    };
}
