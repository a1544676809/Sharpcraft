// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>Reference BatAnimation data (hardcoded behaviour data, not a resource).</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.BatAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/BatAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BatAnimations
{
    public static readonly AnimationDefinition Resting = BuildResting();
    public static readonly AnimationDefinition Flying = BuildFlying();

    private static readonly IReadOnlySet<string> Parts = new HashSet<string>(StringComparer.Ordinal)
    {
        "head", "body", "feet", "right_wing", "right_wing_tip", "left_wing", "left_wing_tip"
    };

    public static readonly KeyframeAnimation RestingBaked = Resting.Bake(Parts);
    public static readonly KeyframeAnimation FlyingBaked = Flying.Bake(Parts);

    private static AnimationKeyframe K(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationKeyframe P(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel Pc(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);

    private static AnimationDefinition BuildResting() =>
        new(0.5f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["head"] = [R(K(0f, 180f, 0f, 0f)), Pc(P(0f, 0f, 0.5f, 0f))],
            ["body"] = [R(K(0f, 180f, 0f, 0f)), Pc(P(0f, 0f, 0.5f, 0f))],
            ["feet"] = [R(K(0f, 0f, 0f, 0f))],
            ["right_wing"] = [R(K(0f, 0f, -10f, 0f)), Pc(P(0f, 0f, 0f, 1f))],
            ["right_wing_tip"] = [R(K(0f, 0f, -120f, 0f))],
            ["left_wing"] = [R(K(0f, 0f, 10f, 0f)), Pc(P(0f, 0f, 0f, 1f))],
            ["left_wing_tip"] = [R(K(0f, 0f, 120f, 0f))]
        });

    private static AnimationDefinition BuildFlying() =>
        new(0.5f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["head"] =
            [
                R(K(0f, 0f, 0f, 0f), K(0.125f, 20f, 0f, 0f), K(0.5f, 0f, 0f, 0f)),
                Pc(P(0f, 0f, 0f, 0f), P(0.125f, 0f, 2f, 0f), P(0.25f, 0f, 1f, 0f), P(0.375f, 0f, 0f, 0f), P(0.4583f, 0f, -1f, 0f), P(0.5f, 0f, 0f, 0f))
            ],
            ["body"] =
            [
                R(K(0f, 40f, 0f, 0f), K(0.25f, 52.5f, 0f, 0f), K(0.5f, 40f, 0f, 0f)),
                Pc(P(0f, 0f, 0f, 0f), P(0.125f, 0f, 2f, 0f), P(0.25f, 0f, 1f, 0f), P(0.375f, 0f, 0f, 0f), P(0.4583f, 0f, -1f, 0f), P(0.5f, 0f, 0f, 0f))
            ],
            ["feet"] = [R(K(0f, 10f, 0f, 0f), K(0.125f, -21.25f, 0f, 0f), K(0.25f, -12.5f, 0f, 0f), K(0.5f, 10f, 0f, 0f))],
            ["right_wing"] = [R(K(0f, 0f, 85f, 0f), K(0.125f, 0f, -55f, 0f), K(0.25f, 0f, 50f, 0f), K(0.375f, 0f, 70f, 0f), K(0.5f, 0f, 85f, 0f))],
            ["right_wing_tip"] = [R(K(0f, 0f, 10.5f, 0f), K(0.0417f, 0f, 65.5f, 0f), K(0.2083f, 0f, -135f, 0f), K(0.5f, 0f, 10.5f, 0f))],
            ["left_wing"] = [R(K(0f, 0f, -85f, 0f), K(0.125f, 0f, 55f, 0f), K(0.25f, 0f, -50f, 0f), K(0.375f, 0f, -70f, 0f), K(0.5f, 0f, -85f, 0f))],
            ["left_wing_tip"] = [R(K(0f, 0f, -10.5f, 0f), K(0.0417f, 0f, -65.5f, 0f), K(0.2083f, 0f, 135f, 0f), K(0.5f, 0f, -10.5f, 0f))]
        });
}
