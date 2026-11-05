// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Dedicated baby humanoid armor geometry (reference <c>HumanoidModel.createBabyArmorMesh</c>): its own head/body/
/// waist/arm/leg/foot boxes with baby UVs. Deformation is applied per slot when the armor layer is built.
/// </summary>
internal static class BabyHumanoidArmorParts
{
    public static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 15f, 0f, -4.5f, -7f, -4.5f, 9f, 8f, 8f, 0, 0),
        PlayerModelDefinition.FromVanilla("body", 0f, 18f, 0f, -3f, -3f, -1.5f, 6f, 5f, 3f, 0, 17),
        PlayerModelDefinition.FromVanilla("waist", 0f, 19f, 0f, -3f, -1.2f, -1.49f, 5.9f, 2f, 2.9f, 0, 36),
        PlayerModelDefinition.FromVanilla("right_arm", -3.5f, 15.5f, 0f, -1f, 0f, -1.53f, 2f, 5f, 3f, 30, 25),
        PlayerModelDefinition.FromVanilla("left_arm", 3.5f, 15.5f, 0f, -1f, 0f, -1.53f, 2f, 5f, 3f, 30, 17),
        PlayerModelDefinition.FromVanilla("left_leg", 1.5f, 20f, 0.5f, -2f, -0.2f, -2f, 3f, 4f, 3f, 18, 24),
        PlayerModelDefinition.FromVanilla("right_leg", -1.5f, 20f, 0.5f, -1f, -0.2f, -2f, 3f, 4f, 3f, 18, 17),
        PlayerModelDefinition.FromVanilla("right_foot", 1.5f, 20f, 0.5f, -2f, 2.9f, -2f, 3f, 1f, 3f, 0, 25),
        PlayerModelDefinition.FromVanilla("left_foot", -1.5f, 20f, 0.5f, -1f, 2.9f, -2f, 3f, 1f, 3f, 0, 29, mirror: true),
    ];
}
