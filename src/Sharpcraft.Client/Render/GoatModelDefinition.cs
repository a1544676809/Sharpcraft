// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// GoatModel: head parts (ears/goatee/horns/nose), two body cubes and four 3x6x3/3x10x3 legs.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.goat.GoatModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/goat/GoatModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class GoatModelDefinition
{
    public const int GoatEntityTypeId = 63;

    public static readonly EntityModelDefinition Definition = new(
        "goat",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("right_ear", 1f, 14f, 0f, -6f, -11f, -10f, 3f, 2f, 1f, 2, 61),
            PlayerModelDefinition.FromVanilla("left_ear", 1f, 14f, 0f, 2f, -11f, -10f, 3f, 2f, 1f, 2, 61, mirror: true),
            PlayerModelDefinition.FromVanilla("goatee", 1f, 14f, 0f, -0.5f, -3f, -14f, 0f, 7f, 5f, 23, 52),
            PlayerModelDefinition.FromVanilla("nose", 1f, 6f, -8f, -3f, -4f, -8f, 5f, 7f, 10f, 34, 46, rotationX: -0.9599f),
            PlayerModelDefinition.FromVanilla("left_horn", 1f, 14f, 0f, -0.01f, -16f, -10f, 2f, 7f, 2f, 12, 55),
            PlayerModelDefinition.FromVanilla("right_horn", 1f, 14f, 0f, -2.99f, -16f, -10f, 2f, 7f, 2f, 12, 55),
            PlayerModelDefinition.FromVanilla("body_lower", 0f, 24f, 0f, -4f, -17f, -7f, 9f, 11f, 16f, 1, 1),
            PlayerModelDefinition.FromVanilla("body_upper", 0f, 24f, 0f, -5f, -18f, -8f, 11f, 14f, 11f, 0, 28),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 1f, 14f, 4f, 0f, 4f, 0f, 3f, 6f, 3f, 36, 29),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 14f, 4f, 0f, 4f, 0f, 3f, 6f, 3f, 49, 29),
            PlayerModelDefinition.FromVanilla("left_front_leg", 1f, 14f, -6f, 0f, 0f, 0f, 3f, 10f, 3f, 49, 2),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 14f, -6f, 0f, 0f, 0f, 3f, 10f, 3f, 35, 2)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/goat/goat");
}
