// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// WardenModel base geometry: bone → body (with ribcages, head/tendrils, arms) and two legs.
/// The bioluminescent, pulsating-spot, heart and tendril emissive layers are not ported yet.
/// </summary>
public static class WardenModelDefinition
{
    public const int WardenEntityTypeId = 146;

    public static readonly EntityModelDefinition Definition = new(
        "warden",
        128,
        128,
        [
            PlayerModelDefinition.FromVanilla("bone", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "bone", 0f, -21f, 0f, -9f, -13f, -4f, 18f, 21f, 11f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_ribcage", "body", -7f, -2f, -4f, -2f, -11f, -0.1f, 9f, 21f, 0f, 90, 11),
            PlayerModelDefinition.FromVanillaChild("left_ribcage", "body", 7f, -2f, -4f, -7f, -11f, -0.1f, 9f, 21f, 0f, 90, 11),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -13f, 0f, -8f, -16f, -5f, 16f, 16f, 10f, 0, 32),
            PlayerModelDefinition.FromVanillaChild("right_tendril", "head", -8f, -12f, 0f, -16f, -13f, 0f, 16f, 16f, 0f, 52, 32),
            PlayerModelDefinition.FromVanillaChild("left_tendril", "head", 8f, -12f, 0f, 0f, -13f, 0f, 16f, 16f, 0f, 58, 0),
            PlayerModelDefinition.FromVanillaChild("right_arm", "body", -13f, -13f, 1f, -4f, 0f, -4f, 8f, 28f, 8f, 44, 50),
            PlayerModelDefinition.FromVanillaChild("left_arm", "body", 13f, -13f, 1f, -4f, 0f, -4f, 8f, 28f, 8f, 0, 58),
            PlayerModelDefinition.FromVanillaChild("right_leg", "bone", -5.9f, -13f, 0f, -3.1f, 0f, -3f, 6f, 13f, 6f, 76, 48),
            PlayerModelDefinition.FromVanillaChild("left_leg", "bone", 5.9f, -13f, 0f, -2.9f, 0f, -3f, 6f, 13f, 6f, 76, 76)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/warden/warden");
}
