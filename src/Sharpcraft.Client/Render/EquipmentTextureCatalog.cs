// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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
    HumanoidLeggings
}

/// <summary>Resolved equipment textures; <see cref="OverlayPath"/> is optional (leather horse armor, wolf armor).</summary>
public sealed record EquipmentTexture(string PrimaryPath, string? OverlayPath = null);

/// <summary>
/// Controlled item → equipment texture mapping for the first equipment stage. Keys are item identifiers, never magic
/// item ids; see docs/analysis/render/entity-equipment.md 3.2.
/// </summary>
public static class EquipmentTextureCatalog
{
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
    public static EquipmentTexture? Resolve(EquipmentAssetKind kind, ItemInfo item, bool baby = false)
    {
        var path = item.Id.Path;
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
            _ => null
        };
    }

    private static readonly string[] ArmorSuffixes = ["_helmet", "_chestplate", "_leggings", "_boots"];
    private static readonly string[] ArmorMaterials = ["leather", "chainmail", "iron", "gold", "diamond", "netherite", "turtle_scute"];

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

        foreach (var material in NautilusBodyMaterials.Values)
        {
            paths.Add("entity/equipment/nautilus_body/" + material);
        }

        paths.AddRange([
            "entity/equipment/wolf_body/armadillo_scute", "entity/equipment/wolf_body/armadillo_scute_overlay"
        ]);

        foreach (var material in ArmorMaterials)
        {
            paths.Add("entity/equipment/humanoid/" + material);
            paths.Add("entity/equipment/humanoid_leggings/" + material);
        }

        return paths;
    }
}
