// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// HoglinModel: a long body with a mane, a tilted head carrying ears and two horns, and four legs.
/// </summary>
/// <remarks>
/// Bind pose only. Zoglin reuses the geometry with its own texture. Headbutt/walk animations are not ported.
/// </remarks>
public static class HoglinModelDefinition
{
    public const int HoglinEntityTypeId = 65;
    public const int ZoglinEntityTypeId = 153;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 7f, 0f, -8f, -7f, -13f, 16f, 14f, 26f, 1, 1),
        PlayerModelDefinition.FromVanillaChild("mane", "body", 0f, -14f, -7f, 0f, 0f, -9f, 0f, 10f, 19f, 90, 33, grow: 0.001f),
        PlayerModelDefinition.FromVanilla("head", 0f, 2f, -12f, -7f, -3f, -19f, 14f, 6f, 19f, 61, 1,
            rotationX: -0.87266463f),
        PlayerModelDefinition.FromVanillaChild("right_ear", "head", -6f, -2f, -3f, -6f, -1f, -2f, 6f, 1f, 4f, 1, 1,
            rotationZ: -MathF.PI * 2f / 9f),
        PlayerModelDefinition.FromVanillaChild("left_ear", "head", 6f, -2f, -3f, 0f, -1f, -2f, 6f, 1f, 4f, 1, 6,
            rotationZ: MathF.PI * 2f / 9f),
        PlayerModelDefinition.FromVanillaChild("right_horn", "head", -7f, 2f, -12f, -1f, -11f, -1f, 2f, 11f, 2f, 10, 13),
        PlayerModelDefinition.FromVanillaChild("left_horn", "head", 7f, 2f, -12f, -1f, -11f, -1f, 2f, 11f, 2f, 1, 13),
        PlayerModelDefinition.FromVanilla("right_front_leg", -4f, 10f, -8.5f, -3f, 0f, -3f, 6f, 14f, 6f, 66, 42),
        PlayerModelDefinition.FromVanilla("left_front_leg", 4f, 10f, -8.5f, -3f, 0f, -3f, 6f, 14f, 6f, 41, 42),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -5f, 13f, 10f, -2.5f, 0f, -2.5f, 5f, 11f, 5f, 21, 45),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 5f, 13f, 10f, -2.5f, 0f, -2.5f, 5f, 11f, 5f, 0, 45)
    ];

    public static readonly EntityModelDefinition Definition = new(
        "hoglin",
        128,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/hoglin/hoglin");

    public static readonly EntityModelDefinition Zoglin = new(
        "zoglin",
        128,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/hoglin/zoglin");
}
