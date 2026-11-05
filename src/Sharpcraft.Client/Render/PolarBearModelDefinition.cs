// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PolarBearModel: head with mouth/ears, two body cubes, and four legs; the layer scales by 1.2.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.polarbear.PolarBearModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/polarbear/PolarBearModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PolarBearModelDefinition
{
    public const int PolarBearEntityTypeId = 105;

    public static readonly EntityModelDefinition Definition = new(
        "polar_bear",
        128,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 10f, -16f, -3.5f, -3f, -3f, 7f, 7f, 7f, 0, 0),
            PlayerModelDefinition.FromVanilla("mouth", 0f, 10f, -16f, -2.5f, 1f, -6f, 5f, 3f, 3f, 0, 44),
            PlayerModelDefinition.FromVanilla("right_ear", 0f, 10f, -16f, -4.5f, -4f, -1f, 2f, 2f, 1f, 26, 0),
            PlayerModelDefinition.FromVanilla("left_ear", 0f, 10f, -16f, 2.5f, -4f, -1f, 2f, 2f, 1f, 26, 0, mirror: true),
            PlayerModelDefinition.FromVanilla("body", -2f, 9f, 12f, -5f, -13f, -7f, 14f, 14f, 11f, 0, 19, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("body_upper", -2f, 9f, 12f, -4f, -25f, -7f, 12f, 12f, 10f, 39, 0, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -4.5f, 14f, 6f, -2f, 0f, -2f, 4f, 10f, 8f, 50, 22),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 4.5f, 14f, 6f, -2f, 0f, -2f, 4f, 10f, 8f, 50, 22),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3.5f, 14f, -8f, -2f, 0f, -2f, 4f, 10f, 6f, 50, 40),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3.5f, 14f, -8f, -2f, 0f, -2f, 4f, 10f, 6f, 50, 40)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bear/polarbear",
        1.2f);
}
