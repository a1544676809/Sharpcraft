// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// EndermiteModel: four root segments with fixed sizes/texture offsets, no parent transforms.
/// </summary>
public static class EndermiteModelDefinition
{
    public const int EndermiteEntityTypeId = 43;

    public static readonly EntityModelDefinition Definition = new(
        "endermite",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("segment0", 0f, 21f, -3.5f, -2f, 0f, -1f, 4f, 3f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("segment1", 0f, 20f, 0f, -3f, 0f, -2.5f, 6f, 4f, 5f, 0, 5),
            PlayerModelDefinition.FromVanilla("segment2", 0f, 21f, 3f, -1.5f, 0f, -0.5f, 3f, 3f, 1f, 0, 14),
            PlayerModelDefinition.FromVanilla("segment3", 0f, 22f, 4f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 0, 18)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/endermite/endermite");
}
