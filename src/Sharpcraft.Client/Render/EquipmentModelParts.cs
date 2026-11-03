// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Extra boxes that the reference's equipment-layer models add on top of the entity model (saddle, harness, armor
/// shells). Base-model copies remain responsible for painting the equipment texture over the body.
/// </summary>
public static class EquipmentModelParts
{
    public static IReadOnlyList<EntityModelPart> For(EquipmentAssetKind kind) => kind switch
    {
        EquipmentAssetKind.HorseSaddle => HorseSaddle,
        EquipmentAssetKind.CamelSaddle => CamelSaddle,
        EquipmentAssetKind.NautilusBody => NautilusArmor,
        EquipmentAssetKind.NautilusSaddle => NautilusSaddle,
        EquipmentAssetKind.HappyGhastBody => HappyHarness,
        _ => []
    };

    private static readonly EntityModelPart[] HorseSaddle =
    [
        PlayerModelDefinition.FromVanillaChild("saddle_model", "body", 0f, 0f, 0f, -5f, -8f, -9f, 10f, 9f, 9f, 26, 0, grow: 0.5f),
        PlayerModelDefinition.FromVanillaChild("left_saddle_mouth", "head_parts", 0f, 0f, 0f, 2f, -9f, -6f, 1f, 2f, 2f, 29, 5),
        PlayerModelDefinition.FromVanillaChild("right_saddle_mouth", "head_parts", 0f, 0f, 0f, -3f, -9f, -6f, 1f, 2f, 2f, 29, 5),
        PlayerModelDefinition.FromVanillaChild("left_saddle_line", "head_parts", 0f, 0f, 0f, 3.1f, -6f, -8f, 0f, 3f, 16f, 32, 2, rotationX: -MathF.PI / 6f),
        PlayerModelDefinition.FromVanillaChild("right_saddle_line", "head_parts", 0f, 0f, 0f, -3.1f, -6f, -8f, 0f, 3f, 16f, 32, 2, rotationX: -MathF.PI / 6f),
        PlayerModelDefinition.FromVanillaChild("head_saddle", "head_parts", 0f, 0f, 0f, -3f, -11f, -1.9f, 6f, 5f, 6f, 1, 1, grow: 0.22f),
        PlayerModelDefinition.FromVanillaChild("mouth_saddle_wrap", "head_parts", 0f, 0f, 0f, -2f, -11f, -4f, 4f, 5f, 2f, 19, 0, grow: 0.2f)
    ];

    private static readonly EntityModelPart[] CamelSaddle =
    [
        PlayerModelDefinition.FromVanillaChild("saddle_lower", "body", 0f, 0f, 0f, -4.5f, -17f, -15.5f, 9f, 5f, 11f, 74, 64, grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("saddle_upper", "body", 0f, 0f, 0f, -3.5f, -20f, -15.5f, 7f, 3f, 11f, 92, 114, grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("saddle_base", "body", 0f, 0f, 0f, -7.5f, -12f, -23.5f, 15f, 12f, 27f, 0, 89, grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("reins_right", "head", 0f, 0f, 0f, 3.51f, -18f, -17f, 0f, 7f, 15f, 98, 42),
        PlayerModelDefinition.FromVanillaChild("reins_front", "head", 0f, 0f, 0f, -3.5f, -18f, -2f, 7f, 7f, 0f, 84, 57),
        PlayerModelDefinition.FromVanillaChild("reins_left", "head", 0f, 0f, 0f, -3.51f, -18f, -17f, 0f, 7f, 15f, 98, 42),
        PlayerModelDefinition.FromVanillaChild("bridle_lower", "head", 0f, 0f, 0f, -3.5f, -7f, -15f, 7f, 8f, 19f, 60, 87, grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("bridle_upper", "head", 0f, 0f, 0f, -3.5f, -21f, -15f, 7f, 14f, 7f, 21, 64, grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("bridle_snout", "head", 0f, 0f, 0f, -2.5f, -21f, -21f, 5f, 5f, 6f, 50, 64, grow: 0.05f),
        PlayerModelDefinition.FromVanillaChild("bridle_right", "head", 0f, 0f, 0f, 2.5f, -19f, -18f, 1f, 2f, 2f, 74, 70),
        PlayerModelDefinition.FromVanillaChild("bridle_left", "head", 0f, 0f, 0f, -3.5f, -19f, -18f, 1f, 2f, 2f, 74, 70, mirror: true)
    ];

    private static readonly EntityModelPart[] NautilusArmor =
    [
        PlayerModelDefinition.FromVanillaChild("armor_shell", "root", 0f, -13f, 5f, -7f, -10f, -7f, 14f, 10f, 16f, 0, 0, grow: 0.01f),
        PlayerModelDefinition.FromVanillaChild("armor_shell_mid", "root", 0f, -13f, 5f, -7f, 0f, -7f, 14f, 8f, 20f, 0, 26, grow: 0.01f),
        PlayerModelDefinition.FromVanillaChild("armor_shell_back", "root", 0f, -13f, 5f, -7f, 0f, 6f, 14f, 8f, 0f, 48, 26)
    ];

    private static readonly EntityModelPart[] NautilusSaddle =
    [
        PlayerModelDefinition.FromVanillaChild("saddle_shell", "root", 0f, -13f, 5f, -7f, -10f, -7f, 14f, 10f, 16f, 0, 0, grow: 0.2f)
    ];

    private static readonly EntityModelPart[] HappyHarness =
    [
        PlayerModelDefinition.FromVanilla("harness", 0f, 24f, 0f, -8f, -16f, -8f, 16f, 16f, 16f, 0, 0),
        PlayerModelDefinition.FromVanilla("goggles", 0f, 14f, -5.5f, -8f, -2.5f, -2.5f, 16f, 5f, 5f, 0, 32, grow: 0.15f)
    ];
}


