// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CreeperModel's static cubes: head/body plus four 4x6x4 legs offset in Z. This is the first non-humanoid
/// definition that uses CentreZ and no PartPose rotation.
/// </summary>
public static class CreeperModelDefinition
{
    public const int CreeperEntityTypeId = 32;

    public static readonly EntityModelDefinition Definition = new(
        "creeper",
        64,
        32,
        [
            new("head", 0f, 18f, 8f, 8f, 8f, 0, 0),
            new("body", 0f, 6f, 8f, 12f, 4f, 16, 16),
            new("left_hind_leg", -2f, 0f, 4f, 6f, 4f, 0, 16, false, null, 4f),
            new("right_hind_leg", 2f, 0f, 4f, 6f, 4f, 0, 16, false, null, 4f),
            new("left_front_leg", -2f, 0f, 4f, 6f, 4f, 0, 16, false, null, -4f),
            new("right_front_leg", 2f, 0f, 4f, 6f, 4f, 0, 16, false, null, -4f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/creeper/creeper");
}
