// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultFoxModel: head with ears/nose, rotated body, four legs and a tail.
/// </summary>
public static class FoxModelDefinition
{
    public const int FoxEntityTypeId = 55;

    public static readonly EntityModelDefinition Definition = new(
        "fox",
        48,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", -1f, 16.5f, -3f, -3f, -2f, -5f, 8f, 6f, 6f, 1, 5),
            PlayerModelDefinition.FromVanilla("right_ear", -1f, 16.5f, -3f, -3f, -4f, -4f, 2f, 2f, 1f, 8, 1),
            PlayerModelDefinition.FromVanilla("left_ear", -1f, 16.5f, -3f, 3f, -4f, -4f, 2f, 2f, 1f, 15, 1),
            PlayerModelDefinition.FromVanilla("nose", -1f, 16.5f, -3f, -1f, 2.01f, -8f, 4f, 2f, 3f, 6, 18),
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, -6f, -3f, 3.999f, -3.5f, 6f, 11f, 6f, 24, 15, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -5f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24),
            PlayerModelDefinition.FromVanilla("left_hind_leg", -1f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24),
            PlayerModelDefinition.FromVanilla("right_front_leg", -5f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24),
            PlayerModelDefinition.FromVanilla("left_front_leg", -1f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24),
            PlayerModelDefinition.FromVanilla("tail", 0f, 16f, -6f, 2f, 0f, -1f, 4f, 9f, 5f, 30, 0,
                rotationX: -MathF.PI / 2f, rotationZ: 0.05235988f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fox/fox");
}
