// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// IllagerModel: shared geometry for Vindicator/Evoker/Pillager. Head/hat/nose, body + 20px outer body,
/// crossed-arm group and four Humanoid limbs.
/// </summary>
public static class IllagerModelDefinition
{
    public const int VindicatorEntityTypeId = 144;
    public const int EvokerEntityTypeId = 47;
    public const int PillagerEntityTypeId = 104;

    public static readonly EntityModelDefinition Vindicator = Create("vindicator", "entity/illager/vindicator");
    public static readonly EntityModelDefinition Evoker = Create("evoker", "entity/illager/evoker");
    public static readonly EntityModelDefinition Pillager = Create("pillager", "entity/illager/pillager");

    private static EntityModelDefinition Create(string name, string texturePath) => new(
        name,
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0,
                overlays: [new("hat", 0.45f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("nose", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20),
            PlayerModelDefinition.FromVanilla("body_outer", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.5f),
            PlayerModelDefinition.FromVanilla("arms_right", 0f, 3f, -1f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22,
                rotationX: 0.75f),
            PlayerModelDefinition.FromVanilla("arms_cross", 0f, 3f, -1f, -4f, 2f, -2f, 8f, 4f, 4f, 40, 38,
                rotationX: 0.75f),
            PlayerModelDefinition.FromVanilla("arms_left", 0f, 3f, -1f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22,
                mirror: true, rotationX: 0.75f),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 46),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 46, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath);
}
