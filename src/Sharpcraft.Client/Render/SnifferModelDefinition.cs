// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SnifferModel: an empty bone root, body with a 0.5-grown outer layer and a flat belly, six legs, and a head with two
/// thin ears, nose and lower beak. Bind pose only; the sniff/dig/walk animations are not ported.
/// </summary>
public static class SnifferModelDefinition
{
    public const int SnifferEntityTypeId = 122;

    public static readonly EntityModelDefinition Definition = new(
        "sniffer",
        192,
        192,
        [
            PlayerModelDefinition.FromVanilla("bone", 0f, 5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "bone", 0f, 0f, 0f, -12.5f, -14f, -20f, 25f, 29f, 40f, 62, 68),
            PlayerModelDefinition.FromVanillaChild("body_outer", "body", 0f, 0f, 0f, -12.5f, -14f, -20f, 25f, 24f, 40f, 62, 0, grow: 0.5f),
            PlayerModelDefinition.FromVanillaChild("body_bottom", "body", 0f, 0f, 0f, -12.5f, 12f, -20f, 25f, 0f, 40f, 87, 68),
            PlayerModelDefinition.FromVanillaChild("right_front_leg", "bone", -7.5f, 10f, -15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 32, 87),
            PlayerModelDefinition.FromVanillaChild("right_mid_leg", "bone", -7.5f, 10f, 0f, -3.5f, -1f, -4f, 7f, 10f, 8f, 32, 105),
            PlayerModelDefinition.FromVanillaChild("right_hind_leg", "bone", -7.5f, 10f, 15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 32, 123),
            PlayerModelDefinition.FromVanillaChild("left_front_leg", "bone", 7.5f, 10f, -15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 0, 87),
            PlayerModelDefinition.FromVanillaChild("left_mid_leg", "bone", 7.5f, 10f, 0f, -3.5f, -1f, -4f, 7f, 10f, 8f, 0, 105),
            PlayerModelDefinition.FromVanillaChild("left_hind_leg", "bone", 7.5f, 10f, 15f, -3.5f, -1f, -4f, 7f, 10f, 8f, 0, 123),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 6.5f, -19.48f, -6.5f, -7.5f, -11.5f, 13f, 18f, 11f, 8, 15),
            PlayerModelDefinition.FromVanillaChild("head_flat", "head", 0f, 0f, 0f, -6.5f, 7.5f, -11.5f, 13f, 0f, 11f, 8, 4),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 6.51f, -7.5f, -4.51f, 0f, 0f, -3f, 1f, 19f, 7f, 2, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -6.51f, -7.5f, -4.51f, -1f, 0f, -3f, 1f, 19f, 7f, 48, 0),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -4.5f, -11.5f, -6.5f, -2f, -9f, 13f, 2f, 9f, 10, 45),
            PlayerModelDefinition.FromVanillaChild("lower_beak", "head", 0f, 2.5f, -12.5f, -6.5f, -7f, -8f, 13f, 12f, 9f, 10, 57)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/sniffer/sniffer");
}
