// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SpiderModel: head + two body cubes + eight rotated 16x2x2 legs. The leg PartPose rotations are carried by
/// EntityModelPart.Pivot*/Rotation* and applied by the mesher around the model pivot.
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
            new("head", 0f, 5f, 8f, 8f, 8f, 32, 4, false, null, -7f),
            new("body0", 0f, 6f, 6f, 6f, 6f, 0, 0),
            new("body1", 0f, 5f, 10f, 8f, 12f, 0, 12, false, null, 9f),
            Leg("right_hind_leg", -11f, 2f, -4f, 2f, Pi / 4f, -Pi / 4f, false),
            Leg("left_hind_leg", 11f, 2f, 4f, 2f, -Pi / 4f, Pi / 4f, true),
            Leg("right_middle_hind_leg", -11f, 1f, -4f, 1f, Pi / 8f, -0.58119464f, false),
            Leg("left_middle_hind_leg", 11f, 1f, 4f, 1f, -Pi / 8f, 0.58119464f, true),
            Leg("right_middle_front_leg", -11f, 0f, -4f, 0f, -Pi / 8f, -0.58119464f, false),
            Leg("left_middle_front_leg", 11f, 0f, 4f, 0f, Pi / 8f, 0.58119464f, true),
            Leg("right_front_leg", -11f, -1f, -4f, -1f, -Pi / 4f, -Pi / 4f, false),
            Leg("left_front_leg", 11f, -1f, 4f, -1f, Pi / 4f, Pi / 4f, true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/spider/spider");

    private static EntityModelPart Leg(
        string name,
        float centreX,
        float centreZ,
        float pivotX,
        float pivotZ,
        float rotationY,
        float rotationZ,
        bool mirror) =>
        new(name, centreX, 8f, 16f, 2f, 2f, 18, 0, mirror, null,
            CentreZ: centreZ, PivotX: pivotX, PivotY: 9f, PivotZ: pivotZ,
            RotationY: rotationY, RotationZ: -rotationZ);
}
