// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SpiderModel: head + two body cubes + eight rotated 16x2x2 legs, all expressed as vanilla PartPose/cube values.
/// </summary>
public static class SpiderModelDefinition
{
    public const int SpiderEntityTypeId = 127;
    private const float Pi = MathF.PI;

    public static readonly EntityModelDefinition Definition = new(
        "spider",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 15f, -3f, -4f, -4f, -8f, 8f, 8f, 8f, 32, 4, mirrorX: false),
            PlayerModelDefinition.FromVanilla("body0", 0f, 15f, 0f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 0, mirrorX: false),
            PlayerModelDefinition.FromVanilla("body1", 0f, 15f, 9f, -5f, -4f, -6f, 10f, 8f, 12f, 0, 12, mirrorX: false),
            Leg("right_hind_leg", -4f, 2f, false, Pi / 4f, -Pi / 4f),
            Leg("left_hind_leg", 4f, 2f, true, -Pi / 4f, Pi / 4f),
            Leg("right_middle_hind_leg", -4f, 1f, false, Pi / 8f, -0.58119464f),
            Leg("left_middle_hind_leg", 4f, 1f, true, -Pi / 8f, 0.58119464f),
            Leg("right_middle_front_leg", -4f, 0f, false, -Pi / 8f, -0.58119464f),
            Leg("left_middle_front_leg", 4f, 0f, true, Pi / 8f, 0.58119464f),
            Leg("right_front_leg", -4f, -1f, false, -Pi / 4f, -Pi / 4f),
            Leg("left_front_leg", 4f, -1f, true, Pi / 4f, Pi / 4f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/spider/spider", 1f, "entity/spider/spider_eyes");

    private static EntityModelPart Leg(string name, float pivotX, float pivotZ, bool mirror, float rotationY, float rotationZ) =>
        PlayerModelDefinition.FromVanilla(
            name,
            pivotX,
            15f,
            pivotZ,
            mirror ? -1f : -15f,
            -1f,
            -1f,
            16f,
            2f,
            2f,
            18,
            0,
            mirror: mirror,
            mirrorX: false,
            rotationY: rotationY,
            rotationZ: -rotationZ);
}

