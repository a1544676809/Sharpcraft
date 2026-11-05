// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>NautilusAnimation.SWIMMING</c>: a 1 s looping mouth/body squash cycle. Values are behaviour data
/// re-expressed with our additive pose conventions (SCALE is x-1, rotations are degrees converted to radians).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.NautilusAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/NautilusAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class NautilusAnimations
{
    public static readonly AnimationDefinition Swimming = BuildSwimming();

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "upper_mouth", "inner_mouth", "lower_mouth"
    };

    public static readonly KeyframeAnimation SwimmingBaked = Swimming.Bake(PartNames);

    private static AnimationKeyframe K(float t, Vector3 v) => new(t, v, AnimationInterpolation.Linear);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel S(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    private static AnimationDefinition BuildSwimming() =>
        new(1.0f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                S(
                    K(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(0.5f, KeyframeAnimations.ScaleVec(1f, 1f, 1.2f)),
                    K(0.75f, KeyframeAnimations.ScaleVec(1f, 1f, 0.9f)),
                    K(0.875f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(1.0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["upper_mouth"] =
            [
                R(
                    K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                    K(0.5f, KeyframeAnimations.DegreeVec(30f, 0f, 0f)),
                    K(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                    K(0.875f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                    K(1.0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
                S(
                    K(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(0.5f, KeyframeAnimations.ScaleVec(1f, 1f, 1.4f)),
                    K(0.75f, KeyframeAnimations.ScaleVec(1f, 1f, 0.9f)),
                    K(0.875f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(1.0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["inner_mouth"] =
            [
                S(
                    K(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(0.5f, KeyframeAnimations.ScaleVec(0.8f, 0.8f, 1f)),
                    K(0.75f, KeyframeAnimations.ScaleVec(1f, 1f, 0.9f)),
                    K(0.875f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(1.0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ],
            ["lower_mouth"] =
            [
                R(
                    K(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                    K(0.5f, KeyframeAnimations.DegreeVec(-30f, 0f, 0f)),
                    K(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                    K(0.875f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                    K(1.0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
                S(
                    K(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(0.5f, KeyframeAnimations.ScaleVec(1f, 1f, 1.4f)),
                    K(0.75f, KeyframeAnimations.ScaleVec(1f, 1f, 0.9f)),
                    K(0.875f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                    K(1.0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
            ]
        });
}
