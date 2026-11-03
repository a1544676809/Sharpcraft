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
public readonly record struct EntityVariantSpec(
    int AccessorIndex,
    string? RegistryPath,
    IReadOnlyList<string> Keys,
    string DefaultKey,
    int ValueMask = 0xFF);

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
        [79] = new(20, null, ["creamy", "white", "brown", "gray"], "creamy")
    };

    /// <summary>Number of entity types with a variant field.</summary>
    public static int Count => Specs.Count;

    /// <summary>Every entity type that has a variant, keyed by type id.</summary>
    public static IReadOnlyDictionary<int, EntityVariantSpec> All => Specs;

    /// <summary>Looks up the variant layout of an entity type.</summary>
    public static bool TryGet(int typeId, out EntityVariantSpec spec) => Specs.TryGetValue(typeId, out spec);
}
