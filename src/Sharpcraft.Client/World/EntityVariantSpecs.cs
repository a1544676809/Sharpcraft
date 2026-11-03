// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Where one entity type keeps its appearance variant, and how to name it.
/// </summary>
/// <param name="AccessorIndex">
/// Index of the variant accessor inside the entity's synced data (docs/analysis/render/entity-variants.md 2.1).
/// </param>
/// <param name="RegistryPath">
/// Registry key path (for example "pig_variant"), or null when the variant is a fixed integer enum.
/// </param>
/// <param name="Keys">
/// Variant names: for registry-backed types the vanilla bootstrap order used when no registry data is available; for
/// fixed enums the id-to-name table.
/// </param>
/// <param name="DefaultKey">Variant name used when the server never sent the field (it equals the default).</param>
/// <param name="ValueMask">Mask applied to the raw integer before it is used as an index.</param>
/// <param name="FlagsAccessorIndex">Accessor carrying the byte flag word (tame/nectar/sleeping), or -1.</param>
/// <param name="TameMask">Flag bit that means tamed (TamableAnimal bit 2 = 4).</param>
/// <param name="NectarMask">Flag bit that means carrying nectar (Bee bit 3 = 8).</param>
/// <param name="SleepingMask">Flag bit that means sleeping (Fox bit 5 = 32).</param>
/// <param name="AngerAccessorIndex">Accessor carrying the anger end time as a long, or -1.</param>
/// <param name="ColdMask">Flag bit that means "cold" (Strider suffocating boolean at 19).</param>
/// <param name="ChargingMask">Flag bit that means charging (Vex flag bit 0).</param>
public readonly record struct EntityVariantSpec(
    int AccessorIndex,
    string? RegistryPath,
    IReadOnlyList<string> Keys,
    string DefaultKey,
    int ValueMask = 0xFF,
    int FlagsAccessorIndex = -1,
    int TameMask = 0,
    int NectarMask = 0,
    int SleepingMask = 0,
    int AngerAccessorIndex = -1,
    int ColdMask = 0,
    int ChargingMask = 0);

/// <summary>
/// Answering "which synced-data field is the variant" for the entity types this client renders with variants.
/// </summary>
/// <remarks>
/// Type ids are literals here because this lives in the world layer and the model definitions live in the render
/// layer; the numbers are the baseline 26.3 registry ids already used by those definitions.
/// </remarks>
public static class EntityVariantSpecs
{
    private static readonly Dictionary<int, EntityVariantSpec> Specs = new()
    {
        // Pig: 18 boost time, then variant / sound variant.
        [101] = new(19, "pig_variant", ["temperate", "warm", "cold"], "temperate"),

        // Cow: AbstractCow adds nothing, so the first two accessors are the variants.
        [30] = new(18, "cow_variant", ["temperate", "warm", "cold"], "temperate"),

        [26] = new(18, "chicken_variant", ["temperate", "warm", "cold"], "temperate"),

        // Cat: TamableAnimal adds 2 (18/19), then the variant.
        [21] = new(
            20,
            "cat_variant",
            ["tabby", "black", "red", "siamese", "british_shorthair", "calico", "persian", "ragdoll", "white", "jellie", "all_black"],
            "tabby"),

        // Horse: AbstractHorse adds flags (18); the value packs markings into the high byte.
        [67] = new(19, null, ["white", "creamy", "chestnut", "brown", "black", "gray", "dark_brown"], "white"),

        // Llama: AbstractChestedHorse adds chest (18), then strength (19), then variant (20).
        [79] = new(20, null, ["creamy", "white", "brown", "gray"], "creamy"),

        // Wolf: TamableAnimal flags (18), owner (19), interested (20), collar (21), anger end time (22), variant (23).
        [152] = new(23, "wolf_variant",
            ["pale", "spotted", "snowy", "black", "ashen", "rusty", "woods", "chestnut", "striped"], "pale",
            FlagsAccessorIndex: 18, TameMask: 4, AngerAccessorIndex: 22),

        // Fox: type id (18), byte flags (19) with the sleeping bit at 32.
        [55] = new(18, null, ["red", "snow"], "red", FlagsAccessorIndex: 19, SleepingMask: 32),

        // Bee: byte flags (18) with the nectar bit at 8; anger end time follows at 19. No variant registry.
        [11] = new(-1, null, ["default"], "default", FlagsAccessorIndex: 18, NectarMask: 8, AngerAccessorIndex: 19),

        // Frog: first accessor is the frog_variant holder.
        [56] = new(18, "frog_variant", ["temperate", "warm", "cold"], "temperate"),

        // Axolotl: integer variant (LUCY/WILD/GOLD/CYAN/BLUE).
        [7] = new(18, null, ["lucy", "wild", "gold", "cyan", "blue"], "lucy"),

        // Strider: boost time (18), then the boolean "suffocating" value at 19 (cold texture).
        [132] = new(-1, null, ["default"], "default", FlagsAccessorIndex: 19, ColdMask: 1),

        // Vex: byte flags at 16 (no AgeableMob in the chain), charging bit 0.
        [142] = new(-1, null, ["default"], "default", FlagsAccessorIndex: 16, ChargingMask: 1),

        // Parrot: TamableAnimal adds flags (18) and owner (19), then the integer variant at 20.
        [99] = new(20, null, ["red_blue", "blue", "green", "yellow_blue", "gray"], "red_blue"),

        // Shulker: attach face (16), peek (17), dye colour byte at 18 (16 = default colour).
        [115] = new(18, null,
            ["white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
             "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black"],
            "default"),

        // Salmon: first accessor is the size variant (SMALL/MEDIUM/LARGE).
        [113] = new(18, null, ["small", "medium", "large"], "medium"),

        // Pufferfish: puff state 0/1/2 selects a completely different model.
        [110] = new(18, null, ["small", "medium", "large"], "small")
    };

    /// <summary>Number of entity types with a variant field.</summary>
    public static int Count => Specs.Count;

    /// <summary>Every entity type that has a variant, keyed by type id.</summary>
    public static IReadOnlyDictionary<int, EntityVariantSpec> All => Specs;

    /// <summary>Looks up the variant layout of an entity type.</summary>
    public static bool TryGet(int typeId, out EntityVariantSpec spec) => Specs.TryGetValue(typeId, out spec);
}

