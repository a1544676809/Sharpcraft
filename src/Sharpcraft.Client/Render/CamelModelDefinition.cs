// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultCamelModel: long body with hump, tail, a three-cube head with ears, and four 5x21x5 legs.
/// </summary>
/// <remarks>
/// The reference puts hump/tail/head under the body, and the three head cubes plus both ears under that head part; this
/// definition mirrors that hierarchy instead of composing absolute pivots (docs/modules/entity-render.md 9).
/// </remarks>
public static class CamelModelDefinition
{
    public const int CamelEntityTypeId = 19;

    public static readonly EntityModelDefinition Definition = new(
        "camel",
        128,
        128,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 4f, 9.5f, -7.5f, -12f, -23.5f, 15f, 12f, 27f, 0, 25),
            PlayerModelDefinition.FromVanillaChild("hump", "body", 0f, -12f, -10f, -4.5f, -5f, -5.5f, 9f, 5f, 11f, 74, 0),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -9f, 3.5f, -1.5f, 0f, 0f, 3f, 14f, 0f, 122, 0),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -3f, -19.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_lower", "head", 0f, 0f, 0f, -3.5f, -7f, -15f, 7f, 8f, 19f, 60, 24),
            PlayerModelDefinition.FromVanillaChild("head_upper", "head", 0f, 0f, 0f, -3.5f, -21f, -15f, 7f, 14f, 7f, 21, 0),
            PlayerModelDefinition.FromVanillaChild("head_snout", "head", 0f, 0f, 0f, -2.5f, -21f, -21f, 5f, 5f, 6f, 50, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 2.5f, -21f, -9.5f, -0.5f, 0.5f, -1f, 3f, 1f, 2f, 45, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -2.5f, -21f, -9.5f, -2.5f, 0.5f, -1f, 3f, 1f, 2f, 67, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 4.9f, 1f, 9.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 58, 16),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -4.9f, 1f, 9.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 94, 16),
            PlayerModelDefinition.FromVanilla("left_front_leg", 4.9f, 1f, -10.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -4.9f, 1f, -10.5f, -2.5f, 2f, -2.5f, 5f, 21f, 5f, 0, 26)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/camel/camel");
}