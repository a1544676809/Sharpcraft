// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultStriderModel: two long legs, a big body, and six flat bristles splayed out from the body's sides.
/// </summary>
/// <remarks>
/// Bind pose only. The reference swaps to a cold texture when the strider is suffocating (overworld), driven by the
/// boolean synced value; the saddle layer is not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.strider.StriderModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/strider/StriderModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class StriderModelDefinition
{
    public const int StriderEntityTypeId = 132;

    public static readonly EntityModelDefinition Definition = new(
        "strider",
        64,
        128,
        [
            PlayerModelDefinition.FromVanilla("right_leg", -4f, 8f, 0f, -2f, 0f, -2f, 4f, 16f, 4f, 0, 32),
            PlayerModelDefinition.FromVanilla("left_leg", 4f, 8f, 0f, -2f, 0f, -2f, 4f, 16f, 4f, 0, 55),
            PlayerModelDefinition.FromVanilla("body", 0f, 1f, 0f, -8f, -6f, -8f, 16f, 14f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_bottom_bristle", "body", -8f, 4f, -8f, -12f, 0f, 0f, 12f, 0f, 16f, 16, 65,
                mirror: true, rotationZ: -1.2217305f),
            PlayerModelDefinition.FromVanillaChild("right_middle_bristle", "body", -8f, -1f, -8f, -12f, 0f, 0f, 12f, 0f, 16f, 16, 49,
                mirror: true, rotationZ: -1.134464f),
            PlayerModelDefinition.FromVanillaChild("right_top_bristle", "body", -8f, -5f, -8f, -12f, 0f, 0f, 12f, 0f, 16f, 16, 33,
                mirror: true, rotationZ: -0.87266463f),
            PlayerModelDefinition.FromVanillaChild("left_top_bristle", "body", 8f, -6f, -8f, 0f, 0f, 0f, 12f, 0f, 16f, 16, 33,
                rotationZ: 0.87266463f),
            PlayerModelDefinition.FromVanillaChild("left_middle_bristle", "body", 8f, -2f, -8f, 0f, 0f, 0f, 12f, 0f, 16f, 16, 49,
                rotationZ: 1.134464f),
            PlayerModelDefinition.FromVanillaChild("left_bottom_bristle", "body", 8f, 3f, -8f, 0f, 0f, 0f, 12f, 0f, 16f, 16, 65,
                rotationZ: 1.2217305f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/strider/strider",
        textureSelector: state => state.IsCold ? "entity/strider/strider_cold" : "entity/strider/strider",
        additionalTexturePaths: ["entity/strider/strider_cold"]);
}
