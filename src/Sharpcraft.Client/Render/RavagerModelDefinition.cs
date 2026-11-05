// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// RavagerModel: a neck carrying the big head with snout, two horns and a mouth; a rotated two-slab body and four tall
/// legs. Bind pose only; walk/attack/roar animations are not ported.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.ravager.RavagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ravager/RavagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class RavagerModelDefinition
{
    public const int RavagerEntityTypeId = 112;

    public static readonly EntityModelDefinition Definition = new(
        "ravager",
        128,
        128,
        [
            PlayerModelDefinition.FromVanilla("neck", 0f, -7f, 5.5f, -5f, -1f, -18f, 10f, 10f, 18f, 68, 73),
            PlayerModelDefinition.FromVanillaChild("head", "neck", 0f, 16f, -17f, -8f, -20f, -14f, 16f, 20f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_snout", "head", 0f, 0f, 0f, -2f, -6f, -18f, 4f, 8f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_horn", "head", -10f, -14f, -8f, 0f, -14f, -2f, 2f, 14f, 4f, 74, 55,
                rotationX: 1.0995574f),
            PlayerModelDefinition.FromVanillaChild("left_horn", "head", 8f, -14f, -8f, 0f, -14f, -2f, 2f, 14f, 4f, 74, 55,
                mirror: true, rotationX: 1.0995574f),
            PlayerModelDefinition.FromVanillaChild("mouth", "head", 0f, -2f, 2f, -8f, 0f, -16f, 16f, 3f, 16f, 0, 36),
            PlayerModelDefinition.FromVanilla("body", 0f, 1f, 2f, -7f, -10f, -7f, 14f, 16f, 20f, 0, 55, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanillaChild("body_lower", "body", 0f, 0f, 0f, -6f, 6f, -7f, 12f, 13f, 18f, 0, 91),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -8f, -13f, 18f, -4f, 0f, -4f, 8f, 37f, 8f, 96, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 8f, -13f, 18f, -4f, 0f, -4f, 8f, 37f, 8f, 96, 0, mirror: true),
            PlayerModelDefinition.FromVanilla("right_front_leg", -8f, -13f, -5f, -4f, 0f, -4f, 8f, 37f, 8f, 64, 0),
            PlayerModelDefinition.FromVanilla("left_front_leg", 8f, -13f, -5f, -4f, 0f, -4f, 8f, 37f, 8f, 64, 0, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/illager/ravager");
}
