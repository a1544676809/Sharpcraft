// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>Equipment asset family; the entity renderer picks the family, the item decides the texture inside it.</summary>
public enum EquipmentAssetKind
{
    HorseBody,
    HorseSaddle,
    DonkeySaddle,
    MuleSaddle,
    SkeletonHorseSaddle,
    ZombieHorseSaddle,
    CamelSaddle,
    PigSaddle,
    StriderSaddle,
    LlamaBody,
    NautilusBody,
    NautilusSaddle,
    WolfBody,
    HappyGhastBody,
    /// <summary>Humanoid armor outer layer (head/chest/feet): same humanoid geometry, outer deformation.</summary>
    HumanoidArmor,
    /// <summary>Humanoid armor leggings (legs): same humanoid geometry, inner deformation, leggings texture layer.</summary>
    HumanoidLeggings,
    /// <summary>Baby humanoid armor: dedicated baby mesh, non-uniform deformation, humanoid_baby texture layer.</summary>
    HumanoidArmorBaby,
    /// <summary>Elytra wings (chest-glider); rendered as a dedicated two-wing model.</summary>
    Elytra
}

/// <summary>Resolved equipment textures; overlay, dye and player-texture metadata are optional.</summary>
public sealed record EquipmentTexture(
    string PrimaryPath,
    string? OverlayPath = null,
    bool UsePlayerTexture = false,
    int? ColourWhenUndyed = null,
    bool Glint = false);

/// <summary>
/// Controlled item → equipment texture mapping for the first equipment stage. Keys are item identifiers, never magic
/// item ids; see docs/analysis/render/entity-equipment.md 3.2.
/// </summary>
public static class EquipmentTextureCatalog
{
    /// <summary>Parsed equipment assets; empty until the resource stack is installed, with the static table as fallback.</summary>
    public static EquipmentAssetRegistry Assets { get; set; } = EquipmentAssetRegistry.Empty;

    private static readonly Dictionary<string, string> HorseBodyMaterials = new(StringComparer.Ordinal)
    {
        ["leather_horse_armor"] = "leather",
        ["iron_horse_armor"] = "iron",
        ["golden_horse_armor"] = "gold",
        ["diamond_horse_armor"] = "diamond",
        ["copper_horse_armor"] = "copper",
        ["netherite_horse_armor"] = "netherite"
    };

    private static readonly Dictionary<string, string> NautilusBodyMaterials = new(StringComparer.Ordinal)
    {
        ["iron_nautilus_armor"] = "iron",
        ["golden_nautilus_armor"] = "gold",
        ["diamond_nautilus_armor"] = "diamond",
        ["copper_nautilus_armor"] = "copper",
        ["netherite_nautilus_armor"] = "netherite"
    };

    private static readonly string[] CarpetColors =
    [
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black"
    ];

    /// <summary>Resolves the texture for <paramref name="kind"/> and an equipped item, or null when unsupported.</summary>
    public static EquipmentTexture? Resolve(
        EquipmentAssetKind kind,
        ItemInfo item,
        bool baby = false,
        string? equippableAssetId = null,
        bool? glintOverride = null)
    {
        var path = item.Id.Path;
        if (TryResolveFromAssets(kind, path, baby, equippableAssetId, glintOverride) is { } fromAssets)
        {
            return fromAssets;
        }

        var saddle = path == "saddle";

        return kind switch
        {
            EquipmentAssetKind.HorseSaddle when saddle => new("entity/equipment/horse_saddle/saddle"),
            EquipmentAssetKind.DonkeySaddle when saddle => new("entity/equipment/donkey_saddle/saddle"),
            EquipmentAssetKind.MuleSaddle when saddle => new("entity/equipment/mule_saddle/saddle"),
            EquipmentAssetKind.SkeletonHorseSaddle when saddle => new("entity/equipment/skeleton_horse_saddle/saddle"),
            EquipmentAssetKind.ZombieHorseSaddle when saddle => new("entity/equipment/zombie_horse_saddle/saddle"),
            EquipmentAssetKind.CamelSaddle when saddle => new("entity/equipment/camel_saddle/saddle"),
            EquipmentAssetKind.PigSaddle when saddle => new("entity/equipment/pig_saddle/saddle"),
            EquipmentAssetKind.StriderSaddle when saddle => new("entity/equipment/strider_saddle/saddle"),
            EquipmentAssetKind.NautilusSaddle when saddle => new("entity/equipment/nautilus_saddle/saddle"),
            EquipmentAssetKind.HorseBody when HorseBodyMaterials.TryGetValue(path, out var horseMaterial) =>
                new("entity/equipment/horse_body/" + horseMaterial,
                    horseMaterial == "leather" ? "entity/equipment/horse_body/leather_overlay" : null),
            EquipmentAssetKind.NautilusBody when NautilusBodyMaterials.TryGetValue(path, out var nautilusMaterial) =>
                new("entity/equipment/nautilus_body/" + nautilusMaterial),
            EquipmentAssetKind.LlamaBody when path.EndsWith("_carpet", StringComparison.Ordinal) =>
                new("entity/equipment/llama_body/" + path[..^"_carpet".Length]),
            EquipmentAssetKind.HappyGhastBody when path.EndsWith("_harness", StringComparison.Ordinal) =>
                new("entity/equipment/happy_ghast_body/" + path),
            EquipmentAssetKind.WolfBody when path == "wolf_armor" =>
                new("entity/equipment/wolf_body/armadillo_scute",
                    "entity/equipment/wolf_body/armadillo_scute_overlay"),
            EquipmentAssetKind.HumanoidArmor when TryArmorMaterial(path, out var armorMaterial) =>
                new("entity/equipment/humanoid/" + armorMaterial),
            EquipmentAssetKind.HumanoidLeggings when TryArmorMaterial(path, out var leggingsMaterial) =>
                new("entity/equipment/humanoid_leggings/" + leggingsMaterial),
            EquipmentAssetKind.HumanoidArmorBaby when TryArmorMaterial(path, out var babyMaterial) =>
                new("entity/equipment/humanoid_baby/" + babyMaterial),
            EquipmentAssetKind.Elytra when path == "elytra" =>
                new("entity/equipment/wings/elytra"),
            _ => null
        };
    }

    /// <summary>Loads the known equipment asset JSONs from the resource stack; static mapping remains the fallback.</summary>
    public static void LoadAssets(ResourceManager resources) =>
        Assets = EquipmentAssetRegistry.Load(resources, EquipmentAssetIds);

    /// <summary>All equipment asset ids shipped by 26.3's client resource pack.</summary>
    public static IReadOnlyList<Identifier> EquipmentAssetIds { get; } = BuildEquipmentAssetIds();

    private static List<Identifier> BuildEquipmentAssetIds()
    {
        var ids = new List<Identifier>();
        foreach (var material in new[] { "chainmail", "copper", "diamond", "gold", "iron", "leather", "netherite", "turtle_scute" })
        {
            ids.Add(new Identifier("minecraft", material));
        }

        ids.Add(new Identifier("minecraft", "saddle"));
        ids.Add(new Identifier("minecraft", "armadillo_scute"));
        ids.Add(new Identifier("minecraft", "elytra"));
        ids.Add(new Identifier("minecraft", "trader_llama"));
        ids.Add(new Identifier("minecraft", "trader_llama_baby"));
        foreach (var color in CarpetColors)
        {
            ids.Add(new Identifier("minecraft", color + "_carpet"));
            ids.Add(new Identifier("minecraft", color + "_harness"));
        }

        return ids;
    }

    private static EquipmentTexture? TryResolveFromAssets(
        EquipmentAssetKind kind,
        string itemPath,
        bool baby,
        string? equippableAssetId,
        bool? glintOverride)
    {
        if (!TryGetAssetBinding(kind, itemPath, baby, out var assetId, out var layerType))
        {
            return null;
        }

        if (equippableAssetId is not null && Identifier.TryParse(equippableAssetId, out var overridden))
        {
            assetId = overridden;
        }

        if (Assets.Get(assetId) is not { } info)
        {
            return null;
        }

        var layers = info.GetLayers(layerType);
        if (layers.Count == 0)
        {
            return null;
        }

        var primary = EquipmentAssetRegistry.EquipmentLayerPath(layerType, layers[0].Texture.Path);
        var overlay = layers.Count > 1
            ? EquipmentAssetRegistry.EquipmentLayerPath(layerType, layers[1].Texture.Path)
            : null;
        return new EquipmentTexture(
            primary,
            overlay,
            layers[0].UsePlayerTexture,
            layers[0].Dyeable?.ColourWhenUndyed,
            glintOverride == true);
    }

    private static bool TryGetAssetBinding(
        EquipmentAssetKind kind,
        string itemPath,
        bool baby,
        out Identifier assetId,
        out EquipmentLayerType layerType)
    {
        assetId = default;
        layerType = default;
        switch (kind)
        {
            case EquipmentAssetKind.HumanoidArmor:
            case EquipmentAssetKind.HumanoidLeggings:
            case EquipmentAssetKind.HumanoidArmorBaby:
                if (!TryArmorMaterial(itemPath, out var armorMaterial))
                {
                    return false;
                }

                assetId = new Identifier("minecraft", armorMaterial);
                layerType = kind switch
                {
                    EquipmentAssetKind.HumanoidLeggings => EquipmentLayerType.HumanoidLeggings,
                    EquipmentAssetKind.HumanoidArmorBaby => EquipmentLayerType.HumanoidBaby,
                    _ => EquipmentLayerType.Humanoid
                };
                return true;

            case EquipmentAssetKind.HorseBody:
                if (!HorseBodyMaterials.TryGetValue(itemPath, out var horseMaterial))
                {
                    return false;
                }

                assetId = new Identifier("minecraft", horseMaterial);
                layerType = EquipmentLayerType.HorseBody;
                return true;

            case EquipmentAssetKind.NautilusBody:
                if (!NautilusBodyMaterials.TryGetValue(itemPath, out var nautilusMaterial))
                {
                    return false;
                }

                assetId = new Identifier("minecraft", nautilusMaterial);
                layerType = EquipmentLayerType.NautilusBody;
                return true;

            case EquipmentAssetKind.LlamaBody when itemPath.EndsWith("_carpet", StringComparison.Ordinal):
                assetId = new Identifier("minecraft", itemPath);
                layerType = EquipmentLayerType.LlamaBody;
                return true;

            case EquipmentAssetKind.HappyGhastBody when itemPath.EndsWith("_harness", StringComparison.Ordinal):
                assetId = new Identifier("minecraft", itemPath);
                layerType = EquipmentLayerType.HappyGhastBody;
                return true;

            case EquipmentAssetKind.WolfBody when itemPath == "wolf_armor":
                assetId = new Identifier("minecraft", "armadillo_scute");
                layerType = EquipmentLayerType.WolfBody;
                return true;

            case EquipmentAssetKind.Elytra when itemPath == "elytra":
                assetId = new Identifier("minecraft", "elytra");
                layerType = EquipmentLayerType.Wings;
                return true;

            case EquipmentAssetKind.HorseSaddle:
            case EquipmentAssetKind.DonkeySaddle:
            case EquipmentAssetKind.MuleSaddle:
            case EquipmentAssetKind.SkeletonHorseSaddle:
            case EquipmentAssetKind.ZombieHorseSaddle:
            case EquipmentAssetKind.CamelSaddle:
            case EquipmentAssetKind.PigSaddle:
            case EquipmentAssetKind.StriderSaddle:
            case EquipmentAssetKind.NautilusSaddle:
                if (itemPath != "saddle")
                {
                    return false;
                }

                assetId = new Identifier("minecraft", "saddle");
                layerType = kind switch
                {
                    EquipmentAssetKind.HorseSaddle => EquipmentLayerType.HorseSaddle,
                    EquipmentAssetKind.DonkeySaddle => EquipmentLayerType.DonkeySaddle,
                    EquipmentAssetKind.MuleSaddle => EquipmentLayerType.MuleSaddle,
                    EquipmentAssetKind.SkeletonHorseSaddle => EquipmentLayerType.SkeletonHorseSaddle,
                    EquipmentAssetKind.ZombieHorseSaddle => EquipmentLayerType.ZombieHorseSaddle,
                    EquipmentAssetKind.CamelSaddle => EquipmentLayerType.CamelSaddle,
                    EquipmentAssetKind.PigSaddle => EquipmentLayerType.PigSaddle,
                    EquipmentAssetKind.StriderSaddle => EquipmentLayerType.StriderSaddle,
                    _ => EquipmentLayerType.NautilusSaddle
                };
                return true;

            default:
                return false;
        }
    }

    private static readonly string[] ArmorSuffixes = ["_helmet", "_chestplate", "_leggings", "_boots"];
    private static readonly string[] ArmorMaterials = ["leather", "chainmail", "copper", "iron", "gold", "diamond", "netherite", "turtle_scute"];
    private static readonly string[] BabyArmorMaterials = ["leather", "chainmail", "copper", "iron", "gold", "diamond", "netherite"];

    /// <summary>Maps a vanilla armor item path to its equipment asset material (turtle_helmet → turtle_scute).</summary>
    private static bool TryArmorMaterial(string path, out string material)
    {
        material = string.Empty;
        if (string.Equals(path, "turtle_helmet", StringComparison.Ordinal))
        {
            material = "turtle_scute";
            return true;
        }

        foreach (var suffix in ArmorSuffixes)
        {
            if (!path.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            material = path[..^suffix.Length] switch
            {
                "leather" => "leather",
                "chainmail" => "chainmail",
                "copper" => "copper",
                "iron" => "iron",
                "golden" => "gold",
                "diamond" => "diamond",
                "netherite" => "netherite",
                _ => string.Empty
            };
            return material.Length > 0;
        }

        return false;
    }

    /// <summary>All llama carpet item identifiers, used by tests and the entity renderer.</summary>
    public static IEnumerable<string> LlamaCarpetItems => CarpetColors.Select(color => color + "_carpet");

    /// <summary>All happy ghast harness item identifiers.</summary>
    public static IEnumerable<string> HappyGhastHarnessItems => CarpetColors.Select(color => color + "_harness");

    /// <summary>All equipment textures modelled in this stage; handed to the skin loader with the equipment definition.</summary>
    public static IReadOnlyList<string> AllTexturePaths { get; } = BuildAllTexturePaths();

    private static List<string> BuildAllTexturePaths()
    {
        var paths = new List<string>();
        foreach (var material in HorseBodyMaterials.Values)
        {
            paths.Add("entity/equipment/horse_body/" + material);
        }

        paths.Add("entity/equipment/horse_body/leather_overlay");
        paths.AddRange([
            "entity/equipment/horse_saddle/saddle", "entity/equipment/donkey_saddle/saddle",
            "entity/equipment/mule_saddle/saddle", "entity/equipment/skeleton_horse_saddle/saddle",
            "entity/equipment/zombie_horse_saddle/saddle", "entity/equipment/camel_saddle/saddle",
            "entity/equipment/pig_saddle/saddle", "entity/equipment/strider_saddle/saddle",
            "entity/equipment/nautilus_saddle/saddle"
        ]);
        foreach (var color in CarpetColors)
        {
            paths.Add("entity/equipment/llama_body/" + color);
            paths.Add("entity/equipment/happy_ghast_body/" + color + "_harness");
        }

        paths.AddRange(["entity/equipment/llama_body/trader_llama", "entity/equipment/llama_body/trader_llama_baby"]);

        foreach (var material in NautilusBodyMaterials.Values)
        {
            paths.Add("entity/equipment/nautilus_body/" + material);
        }

        paths.AddRange([
            "entity/equipment/wolf_body/armadillo_scute", "entity/equipment/wolf_body/armadillo_scute_overlay",
            "entity/wolf/wolf_armor_crackiness_low", "entity/wolf/wolf_armor_crackiness_medium",
            "entity/wolf/wolf_armor_crackiness_high"
        ]);

        foreach (var material in ArmorMaterials)
        {
            paths.Add("entity/equipment/humanoid/" + material);
            paths.Add("entity/equipment/humanoid_leggings/" + material);
        }

        foreach (var material in BabyArmorMaterials)
        {
            paths.Add("entity/equipment/humanoid_baby/" + material);
        }

        paths.Add("entity/equipment/humanoid_baby/leather_overlay");
        paths.Add("entity/equipment/wings/elytra");

        return paths;
    }
}
