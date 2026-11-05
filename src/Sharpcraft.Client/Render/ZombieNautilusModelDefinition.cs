// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Zombie Nautilus: the nautilus body with the zombie texture; the warm variant adds the coral shell growths.
/// Bind pose only; the coral layer is hidden in the reference when body armour is worn (armour layers are not ported).
/// </summary>
public static class ZombieNautilusModelDefinition
{
    public const int ZombieNautilusEntityTypeId = 156;

    /// <summary>Variant ids: TEMPERATE (normal model), WARM (coral model).</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["temperate", "warm"];

    private static readonly EntityModelPart[] CoralParts =
    [
        PlayerModelDefinition.FromVanillaChild("corals", "shell", 8f, 4.5f, -8f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("yellow_coral", "corals", 0f, -11f, 11f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("yellow_coral_second", "yellow_coral", 0f, 0f, 2f, -4.5f, -3.5f, 0f, 6f, 8f, 0f, 0, 85, rotationY: -0.7854f),
        PlayerModelDefinition.FromVanillaChild("yellow_coral_first", "yellow_coral", 0f, 0f, 0f, -4.5f, -3.5f, 0f, 6f, 8f, 0f, 0, 85, rotationY: 0.7854f),
        PlayerModelDefinition.FromVanillaChild("pink_coral", "corals", -12.5f, -18f, 11f, -4.5f, 4.5f, 0f, 6f, 0f, 8f, -8, 94),
        PlayerModelDefinition.FromVanillaChild("pink_coral_second", "pink_coral", -1.5f, 4.5f, 4f, -3f, 0f, -4f, 6f, 0f, 8f, -8, 94, rotationZ: 1.5708f),
        PlayerModelDefinition.FromVanillaChild("blue_coral", "corals", -14f, 0f, 5.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("blue_second", "blue_coral", 0f, 0f, -2f, -3.5f, -5.5f, 0f, 5f, 10f, 0f, 0, 102, rotationY: 0.7854f),
        PlayerModelDefinition.FromVanillaChild("blue_first", "blue_coral", 0f, 0f, 0f, -3.5f, -5.5f, 0f, 5f, 10f, 0f, 0, 102, rotationY: -0.7854f),
        PlayerModelDefinition.FromVanillaChild("red_coral", "corals", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("red_coral_second", "red_coral", -0.5f, -1f, 1.5f, -2.5f, -5.5f, 0f, 4f, 10f, 0f, 0, 112, rotationY: -0.829f),
        PlayerModelDefinition.FromVanillaChild("red_coral_first", "red_coral", 0f, 0f, 0f, -4.5f, -5.5f, 0f, 6f, 10f, 0f, 0, 112, rotationY: 0.7854f)
    ];

    // Reference ZombieNautilusCoralModel: the coral growths are hidden while body armour is worn.
    private static readonly EntityModelPart[] CoralPartsWhileUnarmored = HideWhileArmored();

    private static EntityModelPart[] HideWhileArmored()
    {
        var result = new EntityModelPart[CoralParts.Length];
        for (var i = 0; i < CoralParts.Length; i++)
        {
            result[i] = CoralParts[i] with { VisibleWhen = static state => state.Equipment[6] <= 0 };
        }

        return result;
    }

    public static readonly EntityModelDefinition Definition = new(
        "zombie_nautilus",
        128,
        128,
        NautilusModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/nautilus/zombie_nautilus");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new(
                "zombie_nautilus_coral",
                128,
                128,
                [.. NautilusModelDefinition.Definition.Parts, .. CoralPartsWhileUnarmored],
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/nautilus/zombie_nautilus_coral")
        };
}
