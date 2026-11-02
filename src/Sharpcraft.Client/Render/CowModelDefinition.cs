// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CowModel / QuadrupedModel with legSize 12: head with snout and horns, rotated body with udder, four legs.
/// </summary>
public static class CowModelDefinition
{
    public const int CowEntityTypeId = 30;

    public static readonly EntityModelDefinition Definition = new(
        "cow",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
            PlayerModelDefinition.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 1, 33),
            PlayerModelDefinition.FromVanilla("right_horn", 0f, 4f, -8f, -5f, -5f, -5f, 1f, 3f, 1f, 22, 0),
            PlayerModelDefinition.FromVanilla("left_horn", 0f, 4f, -8f, 4f, -5f, -5f, 1f, 3f, 1f, 22, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -4f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 4f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_front_leg", -4f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_front_leg", 4f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cow/cow_temperate");
}
