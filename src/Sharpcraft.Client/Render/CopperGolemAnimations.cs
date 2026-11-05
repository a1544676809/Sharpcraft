// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// CopperGolemAnimation reference keyframe data (behaviour data; the reference hardcodes these in Java, not JSON).
/// Angles are converted to radians and positions use the reference Y inversion through <see cref="KeyframeAnimations"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.CopperGolemAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/CopperGolemAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CopperGolemAnimations
{
    public static readonly AnimationDefinition COPPER_GOLEM_WALK = BuildCOPPER_GOLEM_WALK();
    public static readonly AnimationDefinition COPPER_GOLEM_IDLE = BuildCOPPER_GOLEM_IDLE();
    public static readonly AnimationDefinition COPPER_GOLEM_WALK_ITEM = BuildCOPPER_GOLEM_WALK_ITEM();
    public static readonly AnimationDefinition COPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP = BuildCOPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP();
    public static readonly AnimationDefinition COPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP = BuildCOPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP();
    public static readonly AnimationDefinition COPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET = BuildCOPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET();
    public static readonly AnimationDefinition COPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET = BuildCOPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET();

    private static AnimationKeyframe K(float t, Vector3 v) => new(t, v, AnimationInterpolation.CatmullRom);
    private static AnimationKeyframe KL(float t, Vector3 v) => new(t, v, AnimationInterpolation.Linear);
    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);
    private static AnimationChannel S(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    private static AnimationDefinition BuildCOPPER_GOLEM_WALK() =>
        new(0.8333f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(10.0f, 15.0f, 0.0f)),
                    K(0.2083f, KeyframeAnimations.DegreeVec(10.0f, -1.87f, -10.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(10.0f, -15.0f, 0.0f)),
                    K(0.625f, KeyframeAnimations.DegreeVec(10.0f, -0.82f, 10.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(10.0f, 15.0f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    K(0.2083f, KeyframeAnimations.DegreeVec(-10.0f, 1.87f, 10.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    K(0.625f, KeyframeAnimations.DegreeVec(-10.0f, 0.82f, -10.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f))
                )
            ],
            ["right_arm"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(70.0f, 0.0f, 0.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(-80.0f, 0.0f, 0.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(70.0f, 0.0f, 0.0f))
                )
            ],
            ["left_arm"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(-80.0f, 0.0f, 0.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(70.0f, 0.0f, 0.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(-80.0f, 0.0f, 0.0f))
                )
            ],
            ["right_leg"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(-60.0f, 0.0f, 0.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(60.0f, 0.0f, 0.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(-60.0f, 0.0f, 0.0f))
                )
            ],
            ["left_leg"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(60.0f, 0.0f, 0.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(-60.0f, 0.0f, 0.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(60.0f, 0.0f, 0.0f))
                )
            ]
        });

    private static AnimationDefinition BuildCOPPER_GOLEM_IDLE() =>
        new(3.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, -35.0f, 0.0f)),
                    KL(0.5f, KeyframeAnimations.DegreeVec(0.0f, -35.0f, 0.0f)),
                    KL(0.625f, KeyframeAnimations.DegreeVec(0.0f, 35.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(0.0f, 35.0f, 0.0f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(0.0f, 35.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.625f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(0.0f, 300.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(0.0f, 300.0f, 0.0f)),
                    KL(1.75f, KeyframeAnimations.DegreeVec(-25.0f, 300.0f, 0.0f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(-25.0f, 300.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 360.0f, 0.0f)),
                    KL(3.5f, KeyframeAnimations.DegreeVec(0.0f, 360.0f, 0.0f))
                )
            ]
        });

    private static AnimationDefinition BuildCOPPER_GOLEM_WALK_ITEM() =>
        new(0.8333f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(10.0f, 7.5f, 0.0f)),
                    K(0.2083f, KeyframeAnimations.DegreeVec(10.0f, -1.87f, -5.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(10.0f, -7.5f, 0.0f)),
                    K(0.625f, KeyframeAnimations.DegreeVec(10.0f, -0.82f, 5.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(10.0f, 7.5f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    K(0.2083f, KeyframeAnimations.DegreeVec(-10.0f, 1.87f, 10.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    K(0.625f, KeyframeAnimations.DegreeVec(-10.0f, 0.82f, -10.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f))
                )
            ],
            ["right_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(-59.78638f, -6.49053f, -3.76613f))
                )
            ],
            ["left_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(-59.78638f, 6.49053f, 3.76613f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(-0.21129f, -0.0212f, -0.07004f))
                )
            ],
            ["right_leg"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(-30.0f, 0.0f, 0.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(30.0f, 0.0f, 0.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(-30.0f, 0.0f, 0.0f))
                )
            ],
            ["left_leg"] =
            [
                R(
                    K(0.0f, KeyframeAnimations.DegreeVec(30.0f, 0.0f, 0.0f)),
                    K(0.4167f, KeyframeAnimations.DegreeVec(-30.0f, 0.0f, 0.0f)),
                    K(0.8333f, KeyframeAnimations.DegreeVec(30.0f, 0.0f, 0.0f))
                )
            ]
        });

    private static AnimationDefinition BuildCOPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP() =>
        new(3.0f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(18.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(24.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(15.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(0.9167f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.125f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(13.92716f, 26.80536f, 6.38918f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(21.43f, 26.81f, 6.39f)),
                    KL(1.7917f, KeyframeAnimations.DegreeVec(21.43f, 26.81f, 6.39f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(12.40725f, 0.0f, 0.00783f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.DegreeVec(-7.5f, 0.0f, 0.0f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(15.0f, 0.0f, 0.0f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(17.5f, 0.0f, 0.0f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(22.5f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.5f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.4f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.01805f, 0.88303f, -0.09783f)),
                    KL(1.7917f, KeyframeAnimations.PosVec(-0.01805f, 0.88303f, -0.09783f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.PosVec(0.0f, 0.0f, -1.0f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, -1.0f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-2.5f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-5.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.6667f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(0.0f, 27.5f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(0.0f, -2.5f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(0.0f, -2.5f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(9.73588f, -1.93433f, -3.73384f)),
                    KL(1.7083f, KeyframeAnimations.DegreeVec(9.73588f, -1.93433f, -3.73384f)),
                    KL(1.7917f, KeyframeAnimations.DegreeVec(10.15255f, -1.93433f, -3.73384f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(17.86088f, -1.93433f, -3.73384f)),
                    KL(1.875f, KeyframeAnimations.DegreeVec(17.23588f, -1.93433f, -3.73384f)),
                    KL(1.9167f, KeyframeAnimations.DegreeVec(17.23588f, -1.93433f, -3.73384f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(17.23588f, -1.93433f, -3.73384f)),
                    KL(2.0833f, KeyframeAnimations.DegreeVec(-0.26f, -1.93f, -3.73f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(-15.0f, 0.0f, 0.0f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(-15.0f, 0.0f, 0.0f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(-5.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.PosVec(0.0f, -0.15451f, 0.47553f)),
                    KL(0.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.875f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.9167f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(2.0833f, KeyframeAnimations.PosVec(-0.39f, 0.52f, -2.21f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.6667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(10.0f, 0.0f, 32.5f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-34.55418f, 11.73507f, 36.8361f)),
                    KL(0.4583f, KeyframeAnimations.DegreeVec(-117.82767f, 2.94538f, 0.22703f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-97.7902f, 0.73403f, 1.39387f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(-95.83405f, 33.18639f, -0.40081f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(-95.83f, 33.19f, -0.4f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(-44.60123f, 10.14454f, 8.66307f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f)),
                    KL(2.0833f, KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f)),
                    KL(2.2083f, KeyframeAnimations.DegreeVec(-113.7629f, 21.38835f, 15.48184f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(-113.76f, 21.39f, 15.48f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(-39.99304f, 7.3511f, 14.05666f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(68.07913f, -3.61348f, 1.39182f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(193.16708f, -1.90441f, -0.43495f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(250.66708f, -1.90441f, -0.43495f)),
                    KL(2.7917f, KeyframeAnimations.DegreeVec(264.19006f, -4.41519f, -0.66792f)),
                    KL(2.8333f, KeyframeAnimations.DegreeVec(270.53693f, 16.03493f, 0.47968f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(319.31668f, 17.97846f, 1.34328f)),
                    KL(2.9167f, KeyframeAnimations.DegreeVec(0.0f, -5.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.PosVec(0.25358f, -0.20153f, 2.21248f)),
                    KL(1.5f, KeyframeAnimations.PosVec(0.25f, -0.2f, 2.21f)),
                    KL(1.5417f, KeyframeAnimations.PosVec(-0.79739f, -0.10573f, 1.70592f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(-0.51052f, -0.38088f, 0.79745f)),
                    KL(2.0833f, KeyframeAnimations.PosVec(-0.51052f, -0.38088f, 0.79745f)),
                    KL(2.125f, KeyframeAnimations.PosVec(-0.51052f, -0.38088f, 0.79745f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(-0.51f, -0.38f, 0.8f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(25.0f, 0.0f, -37.5f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-21.59341f, -12.60837f, -45.69252f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-120.7755f, -5.21988f, -2.02064f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(-98.27419f, -1.79323f, -1.15048f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-93.27f, -1.79f, -1.15f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-93.27419f, -1.79323f, -1.15048f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(0.7083f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(-95.75f, -2.42f, 5.97f)),
                    KL(1.25f, KeyframeAnimations.DegreeVec(-98.4029f, -17.39503f, 6.85104f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(-101.24523f, -29.87096f, 7.69993f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(-101.25f, -29.87f, 7.7f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-88.58526f, -17.10045f, 11.7676f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(-88.59f, -17.1f, 11.77f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(-46.59531f, -16.13694f, -3.85578f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.00677f, -0.76064f, 3.19059f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0512f, -0.76176f, 3.12882f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.DegreeVec(2.5f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.5833f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.PosVec(0.0f, 0.0909f, -0.10834f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0f, 0.09f, -0.11f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(0.0f, 0.09f, -0.11f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.DegreeVec(2.5f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.5833f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.PosVec(0.0f, 0.0909f, -0.10834f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0f, 0.09f, -0.11f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(0.0f, 0.09f, -0.11f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ]
        });

    private static AnimationDefinition BuildCOPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP() =>
        new(3.0f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(18.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(24.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(15.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(0.9167f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.125f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(13.92716f, 26.80536f, 6.38918f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(21.43f, 26.81f, 6.39f)),
                    KL(1.7917f, KeyframeAnimations.DegreeVec(21.43f, 26.81f, 6.39f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(12.40725f, 0.0f, 0.00783f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-2.5f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.5f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.4f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.01805f, 0.88303f, -0.09783f)),
                    KL(1.7917f, KeyframeAnimations.PosVec(-0.01805f, 0.88303f, -0.09783f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-2.5f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-5.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.6667f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(0.0f, 27.5f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(0.0f, -2.5f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(0.0f, -2.5f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(9.73588f, -1.93433f, -3.73384f)),
                    KL(1.7083f, KeyframeAnimations.DegreeVec(9.73588f, -1.93433f, -3.73384f)),
                    KL(1.7917f, KeyframeAnimations.DegreeVec(10.15255f, -1.93433f, -3.73384f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(17.86088f, -1.93433f, -3.73384f)),
                    KL(1.875f, KeyframeAnimations.DegreeVec(17.23588f, -1.93433f, -3.73384f)),
                    KL(1.9167f, KeyframeAnimations.DegreeVec(17.23588f, -1.93433f, -3.73384f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(17.23588f, -1.93433f, -3.73384f)),
                    KL(2.0833f, KeyframeAnimations.DegreeVec(-0.26f, -1.93f, -3.73f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(1.25f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(2.5f, 0.0f, 0.0f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, -360.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.PosVec(0.0f, -0.15451f, 0.47553f)),
                    KL(0.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.875f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.9167f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(2.0833f, KeyframeAnimations.PosVec(-0.39f, 0.52f, -2.21f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.5f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, -0.01091f, -0.02988f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, -0.01f, -0.03f)),
                    KL(2.6667f, KeyframeAnimations.PosVec(0.0f, -0.01f, -0.03f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, -0.01f, -0.03f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(10.0f, 0.0f, 32.5f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-34.55418f, 11.73507f, 36.8361f)),
                    KL(0.4583f, KeyframeAnimations.DegreeVec(-117.82767f, 2.94538f, 0.22703f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-97.7902f, 0.73403f, 1.39387f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(-95.83405f, 33.18639f, -0.40081f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(-95.83f, 33.19f, -0.4f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(-44.60123f, 10.14454f, 8.66307f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f)),
                    KL(2.0833f, KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(-6.53898f, 13.96898f, 14.34786f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(3.50393f, -4.70737f, 8.3608f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(3.50393f, -4.70737f, 8.3608f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(3.90089f, -4.3843f, 3.35549f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(3.9f, -4.38f, 3.36f)),
                    KL(2.9167f, KeyframeAnimations.DegreeVec(3.9f, -4.38f, 3.36f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(3.90089f, -4.3843f, 3.35549f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.PosVec(0.25358f, -0.20153f, 2.21248f)),
                    KL(1.5f, KeyframeAnimations.PosVec(0.25f, -0.2f, 2.21f)),
                    KL(1.5417f, KeyframeAnimations.PosVec(-0.79739f, -0.10573f, 1.70592f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(-0.51052f, -0.38088f, 0.79745f)),
                    KL(2.0833f, KeyframeAnimations.PosVec(-0.51052f, -0.38088f, 0.79745f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(-0.51052f, -0.38088f, 0.79745f)),
                    KL(2.25f, KeyframeAnimations.PosVec(-0.46f, -0.34f, 0.72f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(-0.46f, 0.1159f, -0.30086f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(-0.46f, 0.1159f, -0.30086f)),
                    KL(2.5f, KeyframeAnimations.PosVec(-0.46f, 0.1159f, -0.30086f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(-0.46f, -0.88f, -0.3f)),
                    KL(2.9167f, KeyframeAnimations.PosVec(-0.46f, -0.88f, -0.3f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(-0.46f, 0.1159f, -0.30086f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(25.0f, 0.0f, -37.5f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-21.59341f, -12.60837f, -45.69252f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-120.7755f, -5.21988f, -2.02064f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(-98.27419f, -1.79323f, -1.15048f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-93.27f, -1.79f, -1.15f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-93.27419f, -1.79323f, -1.15048f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(0.7083f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(-95.75f, -2.42f, 5.97f)),
                    KL(1.25f, KeyframeAnimations.DegreeVec(-98.4029f, -17.39503f, 6.85104f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(-101.24523f, -29.87096f, 7.69993f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(-101.25f, -29.87f, 7.7f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, -20.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(2.47864f, -0.32621f, -12.50706f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(2.47864f, -0.32621f, -12.50706f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(2.41492f, -0.64686f, -5.01363f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(2.41f, -0.65f, -5.01f)),
                    KL(2.9167f, KeyframeAnimations.DegreeVec(2.41f, -0.65f, -5.01f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(2.41492f, -0.64686f, -5.01363f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.00677f, -0.76064f, 3.19059f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(-0.00677f, -0.76064f, 3.19059f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(-0.00677f, -0.76064f, 3.19059f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.03f, -0.76f, 0.45f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.03f, -0.28229f, -0.07133f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.03f, -0.28229f, -0.07133f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.03f, -0.28229f, -0.07133f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(0.03f, -1.28f, -0.07f)),
                    KL(2.9167f, KeyframeAnimations.PosVec(0.03f, -1.28f, -0.07f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.03f, -0.28229f, -0.07133f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_leg"] =
            [
                R(
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                ),
                S(
                    KL(0.0f, KeyframeAnimations.ScaleVec(1.0f, 1.0f, 1.0f)),
                    KL(3.0f, KeyframeAnimations.ScaleVec(1.0f, 1.0f, 1.0f))
                )
            ]
        });

    private static AnimationDefinition BuildCOPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET() =>
        new(3.0f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(18.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(24.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(15.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(0.9167f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.125f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(13.92716f, 26.80536f, 6.38918f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(21.43f, 26.81f, 6.39f)),
                    KL(1.7917f, KeyframeAnimations.DegreeVec(21.43f, 26.81f, 6.39f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(12.40725f, 0.0f, 0.00783f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.375f, KeyframeAnimations.DegreeVec(15.0f, 0.0f, 0.0f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(17.5f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(22.5f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(24.14867f, -20.70481f, -9.00717f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(24.14867f, -20.70481f, -9.00717f)),
                    KL(2.75f, KeyframeAnimations.DegreeVec(22.5f, 0.0f, 0.0f)),
                    KL(2.7917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.5f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.4f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.01805f, 0.88303f, -0.09783f)),
                    KL(1.7917f, KeyframeAnimations.PosVec(-0.01805f, 0.88303f, -0.09783f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.PosVec(0.0f, 0.46194f, -0.19134f)),
                    KL(2.7083f, KeyframeAnimations.PosVec(0.0f, 0.46194f, -0.19134f)),
                    KL(2.7917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-2.5f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-5.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.6667f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(0.0f, 27.5f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(0.0f, -2.5f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(10.16381f, -16.71134f, -6.35306f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(10.16381f, -16.71134f, -6.35306f)),
                    KL(1.9167f, KeyframeAnimations.DegreeVec(10.16381f, -16.71134f, -6.35306f)),
                    KL(1.9583f, KeyframeAnimations.DegreeVec(10.16381f, -16.71134f, -6.35306f)),
                    KL(2.0f, KeyframeAnimations.DegreeVec(5.16381f, -16.71134f, -6.35306f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(0.16381f, -16.71134f, -6.35306f)),
                    KL(2.0833f, KeyframeAnimations.DegreeVec(0.15732f, -4.21139f, -6.31751f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(0.07901f, 5.3943f, -3.15187f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 7.5f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(4.53867f, 7.47675f, 0.59181f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-2.53852f, 9.99038f, -0.44067f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(-12.68664f, 9.76061f, -2.18558f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(-15.19938f, 22.36971f, -3.52259f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(-3.02173f, 22.37156f, -2.41802f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(-0.52173f, 22.37156f, -2.41802f)),
                    KL(2.75f, KeyframeAnimations.DegreeVec(-12.40598f, -0.4674f, -1.79838f)),
                    KL(2.8333f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.PosVec(0.0f, -0.15451f, 0.47553f)),
                    KL(0.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.9167f, KeyframeAnimations.PosVec(-0.22438f, 0.82319f, -1.27252f)),
                    KL(1.9583f, KeyframeAnimations.PosVec(-0.52521f, 0.96725f, -0.32978f)),
                    KL(2.0f, KeyframeAnimations.PosVec(-0.52521f, 0.96725f, -0.32978f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(-0.5345f, 1.16541f, -0.37206f)),
                    KL(2.0833f, KeyframeAnimations.PosVec(-0.5345f, 1.16541f, -0.37206f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.8333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(10.0f, 0.0f, 32.5f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-34.55418f, 11.73507f, 36.8361f)),
                    KL(0.4583f, KeyframeAnimations.DegreeVec(-82.47403f, 17.82361f, 2.17224f)),
                    KL(0.5f, KeyframeAnimations.DegreeVec(-85.08388f, 14.26971f, 1.99595f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-85.16266f, 13.19102f, 2.43976f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(-95.83405f, 33.18639f, -0.40081f)),
                    KL(1.25f, KeyframeAnimations.DegreeVec(-95.83f, 33.19f, -0.4f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(-98.33f, 33.19f, -0.4f)),
                    KL(1.5417f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(2.0f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(-84.12204f, 8.95753f, 14.11779f)),
                    KL(2.2083f, KeyframeAnimations.DegreeVec(-84.12204f, 8.95753f, 14.11779f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(-93.6065f, 13.90544f, 15.98524f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-124.48661f, 66.29146f, -7.28605f)),
                    KL(2.375f, KeyframeAnimations.DegreeVec(-129.4866f, 66.29146f, -7.28605f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(-108.91607f, 1.79762f, 20.93924f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(-102.18303f, 4.35881f, 17.40962f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(-98.33642f, -0.70114f, 4.09322f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(-98.39385f, 6.71929f, 3.00137f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(-98.33981f, 1.77244f, 3.7307f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(-100.70987f, 3.48829f, 7.1138f)),
                    KL(2.75f, KeyframeAnimations.DegreeVec(-97.95f, 6.92f, 13.88f)),
                    KL(2.7917f, KeyframeAnimations.DegreeVec(-87.95f, 6.92f, 13.88f)),
                    KL(2.8333f, KeyframeAnimations.DegreeVec(-97.95f, 6.92f, 13.88f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(-102.95f, 6.92f, 13.88f)),
                    KL(2.9167f, KeyframeAnimations.DegreeVec(-76.475f, 3.46f, 6.94f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(-26.475f, 3.46f, 6.94f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.PosVec(0.25358f, -0.20153f, 2.21248f)),
                    KL(1.25f, KeyframeAnimations.PosVec(0.25f, -0.2f, 2.21f)),
                    KL(1.2917f, KeyframeAnimations.PosVec(0.25f, -0.2f, 2.21f)),
                    KL(1.5417f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(2.0f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(2.25f, KeyframeAnimations.PosVec(-0.51f, -0.38f, 0.8f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(-0.51f, -0.38f, 0.8f)),
                    KL(2.375f, KeyframeAnimations.PosVec(-0.51f, -0.38f, 0.8f)),
                    KL(2.4167f, KeyframeAnimations.PosVec(-2.14094f, 0.69619f, 1.23422f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(-0.97932f, 0.38244f, 0.12884f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(-1.55232f, 1.79904f, 0.37956f)),
                    KL(2.625f, KeyframeAnimations.PosVec(-1.53125f, 1.64598f, 1.41168f)),
                    KL(2.6667f, KeyframeAnimations.PosVec(-1.57256f, 1.05375f, 1.32469f)),
                    KL(2.75f, KeyframeAnimations.PosVec(-1.33f, 0.16f, 1.02f)),
                    KL(2.7917f, KeyframeAnimations.PosVec(-1.33f, 0.16f, 1.02f)),
                    KL(2.8333f, KeyframeAnimations.PosVec(-1.33f, 0.16f, 1.02f)),
                    KL(2.875f, KeyframeAnimations.PosVec(-1.33f, 0.16f, 1.02f)),
                    KL(2.9167f, KeyframeAnimations.PosVec(-0.5748f, 0.38848f, 1.45646f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(-0.67f, 0.08f, 0.51f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(25.0f, 0.0f, -37.5f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-21.59341f, -12.60837f, -45.69252f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-120.7755f, -5.21988f, -2.02064f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(-98.27419f, -1.79323f, -1.15048f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-93.27f, -1.79f, -1.15f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-93.27419f, -1.79323f, -1.15048f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(0.7083f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(-95.75f, -2.42f, 5.97f)),
                    KL(1.25f, KeyframeAnimations.DegreeVec(-98.4029f, -17.39503f, 6.85104f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(-101.24523f, -29.87096f, 7.69993f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(-101.25f, -29.87f, 7.7f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(1.9583f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.2083f, KeyframeAnimations.DegreeVec(-88.58526f, -17.10045f, 11.7676f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-88.59f, -17.1f, 11.77f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(-46.59531f, -16.13694f, -3.85578f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(-24.5317f, -19.0214f, -13.70805f)),
                    KL(2.8333f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.00677f, -0.76064f, 3.19059f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0512f, -0.76176f, 3.12882f)),
                    KL(2.8333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.7083f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.7917f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.8333f, KeyframeAnimations.DegreeVec(5.0f, 0.0f, 0.0f)),
                    KL(2.875f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.7083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.7917f, KeyframeAnimations.PosVec(0.0f, 0.09f, -0.11f)),
                    KL(2.8333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.625f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ]
        });

    private static AnimationDefinition BuildCOPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET() =>
        new(3.0f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(18.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(24.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(15.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(12.5f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(0.9167f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(14.72765f, -31.63886f, -7.85085f)),
                    KL(1.125f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(12.40525f, -4.4E-4f, 0.00829f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(13.92716f, 26.80536f, 6.38918f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(13.93f, 26.81f, 6.39f)),
                    KL(1.7083f, KeyframeAnimations.DegreeVec(12.40725f, 0.00444f, 0.00783f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(12.40725f, 0.00444f, 0.00783f)),
                    KL(2.125f, KeyframeAnimations.DegreeVec(12.40725f, 0.0f, 0.00783f)),
                    KL(2.25f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.6667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.6f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.5f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.4f, 0.0f)),
                    KL(1.7083f, KeyframeAnimations.PosVec(0.0f, 0.34f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(0.0f, 0.34f, 0.0f)),
                    KL(2.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.6667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["head"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-20.0f, 0.0f, 0.0f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-2.5f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-5.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.6667f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(0.0f, -20.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(0.0f, 10.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(0.0f, 27.5f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.DegreeVec(0.0f, -2.5f, 0.0f)),
                    KL(1.7083f, KeyframeAnimations.DegreeVec(0.57f, -1.25f, 0.07f)),
                    KL(1.75f, KeyframeAnimations.DegreeVec(0.89798f, -18.12465f, -0.16276f)),
                    KL(1.7917f, KeyframeAnimations.DegreeVec(1.21328f, -21.15422f, -0.2148f)),
                    KL(1.875f, KeyframeAnimations.DegreeVec(1.21328f, -21.15422f, -0.2148f)),
                    KL(2.0f, KeyframeAnimations.DegreeVec(1.21328f, -21.15422f, -0.2148f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(2.56546f, 0.76525f, 0.57246f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(4.53867f, 7.47675f, 0.59181f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(4.53867f, 7.47675f, 0.59181f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, -360.0f, 0.0f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.PosVec(0.0f, -0.15451f, 0.47553f)),
                    KL(0.25f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.0417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.4583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.625f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.7083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, -0.01f, -0.03f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.1667f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(10.0f, 0.0f, 32.5f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-34.55418f, 11.73507f, 36.8361f)),
                    KL(0.4583f, KeyframeAnimations.DegreeVec(-82.47403f, 17.82361f, 2.17224f)),
                    KL(0.5f, KeyframeAnimations.DegreeVec(-85.08388f, 14.26971f, 1.99595f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-85.16266f, 13.19102f, 2.43976f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.75f, KeyframeAnimations.DegreeVec(-92.79f, 0.73f, 1.39f)),
                    KL(0.8333f, KeyframeAnimations.DegreeVec(-95.83405f, 33.18639f, -0.40081f)),
                    KL(1.25f, KeyframeAnimations.DegreeVec(-95.83f, 33.19f, -0.4f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(-98.33f, 33.19f, -0.4f)),
                    KL(1.5417f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(2.0f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(3.9f, -4.38f, 3.36f)),
                    KL(2.9167f, KeyframeAnimations.DegreeVec(3.9f, -4.38f, 3.36f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(3.90089f, -4.3843f, 3.35549f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.375f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.75f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.8333f, KeyframeAnimations.PosVec(0.25358f, -0.20153f, 2.21248f)),
                    KL(1.25f, KeyframeAnimations.PosVec(0.25f, -0.2f, 2.21f)),
                    KL(1.2917f, KeyframeAnimations.PosVec(0.25f, -0.2f, 2.21f)),
                    KL(1.5417f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(1.8333f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(2.0f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(-0.26323f, -1.46323f, 0.66566f)),
                    KL(2.1667f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(-0.46f, -0.88f, -0.3f)),
                    KL(2.9167f, KeyframeAnimations.PosVec(-0.46f, -0.88f, -0.3f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(-0.46f, 0.1159f, -0.30086f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_arm"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(-2.5f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(25.0f, 0.0f, -37.5f)),
                    KL(0.25f, KeyframeAnimations.DegreeVec(-21.59341f, -12.60837f, -45.69252f)),
                    KL(0.2917f, KeyframeAnimations.DegreeVec(-120.7755f, -5.21988f, -2.02064f)),
                    KL(0.375f, KeyframeAnimations.DegreeVec(-98.27419f, -1.79323f, -1.15048f)),
                    KL(0.4167f, KeyframeAnimations.DegreeVec(-93.27f, -1.79f, -1.15f)),
                    KL(0.5417f, KeyframeAnimations.DegreeVec(-93.27419f, -1.79323f, -1.15048f)),
                    KL(0.5833f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(0.7083f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.1667f, KeyframeAnimations.DegreeVec(-93.55693f, -22.3224f, 3.64383f)),
                    KL(1.2083f, KeyframeAnimations.DegreeVec(-95.75f, -2.42f, 5.97f)),
                    KL(1.25f, KeyframeAnimations.DegreeVec(-98.4029f, -17.39503f, 6.85104f)),
                    KL(1.2917f, KeyframeAnimations.DegreeVec(-101.24523f, -29.87096f, 7.69993f)),
                    KL(1.5833f, KeyframeAnimations.DegreeVec(-101.25f, -29.87f, 7.7f)),
                    KL(1.6667f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(1.8333f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.0833f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.1667f, KeyframeAnimations.DegreeVec(-88.17772f, -42.09094f, 10.96195f)),
                    KL(2.2083f, KeyframeAnimations.DegreeVec(-88.58526f, -17.10045f, 11.7676f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(-88.59f, -17.1f, 11.77f)),
                    KL(2.4167f, KeyframeAnimations.DegreeVec(-46.59531f, -16.13694f, -3.85578f)),
                    KL(2.4583f, KeyframeAnimations.DegreeVec(-24.5317f, -19.0214f, -13.70805f)),
                    KL(2.5f, KeyframeAnimations.DegreeVec(-24.5317f, -19.0214f, -13.70805f)),
                    KL(2.5417f, KeyframeAnimations.DegreeVec(2.41f, -0.65f, -5.01f)),
                    KL(2.9167f, KeyframeAnimations.DegreeVec(2.41f, -0.65f, -5.01f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(2.41492f, -0.64686f, -5.01363f)),
                    KL(3.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.4167f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.5833f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(1.6667f, KeyframeAnimations.PosVec(-0.00677f, -0.76064f, 3.19059f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0512f, -0.76176f, 3.12882f)),
                    KL(2.4583f, KeyframeAnimations.PosVec(0.03f, -0.51f, 2.09f)),
                    KL(2.5f, KeyframeAnimations.PosVec(0.03f, -0.51f, 2.09f)),
                    KL(2.5417f, KeyframeAnimations.PosVec(0.03f, -1.28f, -0.07f)),
                    KL(2.9167f, KeyframeAnimations.PosVec(0.03f, -1.28f, -0.07f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.03f, -0.28229f, -0.07133f)),
                    KL(3.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["right_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(7.5f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ],
            ["left_leg"] =
            [
                R(
                    KL(0.0f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.DegreeVec(-10.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.DegreeVec(0.0f, 0.0f, 0.0f))
                ),
                P(
                    KL(0.0f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.125f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(0.2083f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.0417f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.2917f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.3333f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f)),
                    KL(2.9583f, KeyframeAnimations.PosVec(0.0f, 0.0f, 0.0f))
                )
            ]
        });

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "head", "right_arm", "left_arm", "right_leg", "left_leg"
    };

    public static readonly KeyframeAnimation WalkBaked = COPPER_GOLEM_WALK.Bake(PartNames);
    public static readonly KeyframeAnimation IdleBaked = COPPER_GOLEM_IDLE.Bake(PartNames);
    public static readonly KeyframeAnimation WalkItemBaked = COPPER_GOLEM_WALK_ITEM.Bake(PartNames);
    public static readonly KeyframeAnimation ItemDropBaked = COPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP.Bake(PartNames);
    public static readonly KeyframeAnimation ItemNoDropBaked = COPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP.Bake(PartNames);
    public static readonly KeyframeAnimation NoItemGetBaked = COPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET.Bake(PartNames);
    public static readonly KeyframeAnimation NoItemNoGetBaked = COPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET.Bake(PartNames);
}
