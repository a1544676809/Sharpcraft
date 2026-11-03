// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference CamelAnimation data, expressed as behaviour data (the reference hardcodes these in Java, not JSON).
/// All angles are converted to radians and positions use the reference Y inversion through <see cref="KeyframeAnimations"/>.
/// </summary>
public static class CamelAnimations
{
    public static readonly AnimationDefinition Walk = BuildWalk();

    private static AnimationKeyframe K(float t, Vector3 v) =>
        new(t, v, AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe KL(float t, Vector3 v) =>
        new(t, v, AnimationInterpolation.Linear);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);

    private static AnimationDefinition BuildWalk() =>
        new(1.5f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["root"] = [R(
                K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
                K(1f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
                K(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)))],
            ["head"] = [R(
                K(0f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
                K(0.375f, KeyframeAnimations.DegreeVec(-2.5f, 0f, 0f)),
                K(0.75f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
                K(1.125f, KeyframeAnimations.DegreeVec(-2.5f, 0f, 0f)),
                K(1.5f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)))],
            ["right_front_leg"] =
            [
                R(K(0f, KeyframeAnimations.DegreeVec(22.5f, 0f, 0f)),
                  K(0.75f, KeyframeAnimations.DegreeVec(-22.5f, 0f, 0f)),
                  K(1.5f, KeyframeAnimations.DegreeVec(22.5f, 0f, 0f))),
                P(K(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(0.4583f, KeyframeAnimations.PosVec(0f, 4f, 0f)),
                  K(0.75f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(1.5f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
            ],
            ["left_front_leg"] =
            [
                R(K(0f, KeyframeAnimations.DegreeVec(-22.5f, 0f, 0f)),
                  K(0.75f, KeyframeAnimations.DegreeVec(22.5f, 0f, 0f)),
                  K(1.5f, KeyframeAnimations.DegreeVec(-22.5f, 0f, 0f))),
                P(K(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(0.75f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(1.2083f, KeyframeAnimations.PosVec(0f, 4f, 0f)),
                  K(1.5f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
            ],
            ["left_hind_leg"] =
            [
                R(K(0f, KeyframeAnimations.DegreeVec(-20.4f, 0f, 0f)),
                  K(0.75f, KeyframeAnimations.DegreeVec(22.5f, 0f, 0f)),
                  K(1.375f, KeyframeAnimations.DegreeVec(-22.5f, 0f, 0f)),
                  K(1.5f, KeyframeAnimations.DegreeVec(-20.4f, 0f, 0f))),
                P(K(0f, KeyframeAnimations.PosVec(0f, -0.21f, 0f)),
                  K(0.75f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(1.0833f, KeyframeAnimations.PosVec(0f, 4f, 0f)),
                  K(1.375f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  KL(1.5f, KeyframeAnimations.PosVec(0f, -0.21f, 0f)))
            ],
            ["right_hind_leg"] =
            [
                R(K(0f, KeyframeAnimations.DegreeVec(22.5f, 0f, 0f)),
                  K(0.625f, KeyframeAnimations.DegreeVec(-22.5f, 0f, 0f)),
                  K(1.5f, KeyframeAnimations.DegreeVec(22.5f, 0f, 0f))),
                P(K(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(0.375f, KeyframeAnimations.PosVec(0f, 4f, 0f)),
                  K(0.625f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                  K(1.5f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
            ],
            ["left_ear"] = [R(
                K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                K(0.375f, KeyframeAnimations.DegreeVec(0f, 0f, -22.5f)),
                K(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                K(1.125f, KeyframeAnimations.DegreeVec(0f, 0f, -22.5f)),
                K(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))],
            ["right_ear"] = [R(
                K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                K(0.375f, KeyframeAnimations.DegreeVec(0f, 0f, 22.5f)),
                K(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                K(1.125f, KeyframeAnimations.DegreeVec(0f, 0f, 22.5f)),
                K(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))],
            ["tail"] = [R(
                K(0f, KeyframeAnimations.DegreeVec(15.94102f, -8.42106f, 20.94102f)),
                K(0.75f, KeyframeAnimations.DegreeVec(15.94102f, 8.42106f, -20.94102f)),
                K(1.5f, KeyframeAnimations.DegreeVec(15.94102f, -8.42106f, 20.94102f)))]
        });

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "root", "head", "right_front_leg", "left_front_leg", "left_hind_leg", "right_hind_leg",
        "left_ear", "right_ear", "tail"
    };

    public static readonly KeyframeAnimation WalkBaked = Walk.Bake(PartNames);
}
