// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference FrogAnimation data (hardcoded behaviour data in the reference, not a resource).
/// Angles are converted to radians, positions use the reference Y inversion and scale is the additive
/// offset produced by <see cref="KeyframeAnimations"/>. Every bone name matches a part in
/// <see cref="FrogModelDefinition"/>.
/// </summary>
/// <remarks>
/// The reference drives these clips as: jump/croak/tongue/idle-water from an <see cref="AnimationState"/>
/// elapsed clock, swim and walk from the walk animation position/speed. The definition data below is
/// independent of how the controller feeds the clock.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.FrogAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/FrogAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class FrogAnimations
{
    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "croaking_body", "left_arm", "right_arm", "left_leg", "right_leg", "body", "head", "tongue"
    };

    public static readonly AnimationDefinition Croak = BuildCroak();
    public static readonly AnimationDefinition Walk = BuildWalk();
    public static readonly AnimationDefinition Jump = BuildJump();
    public static readonly AnimationDefinition Tongue = BuildTongue();
    public static readonly AnimationDefinition Swim = BuildSwim();
    public static readonly AnimationDefinition IdleWater = BuildIdleWater();

    public static readonly KeyframeAnimation CroakBaked = Croak.Bake(PartNames);
    public static readonly KeyframeAnimation WalkBaked = Walk.Bake(PartNames);
    public static readonly KeyframeAnimation JumpBaked = Jump.Bake(PartNames);
    public static readonly KeyframeAnimation TongueBaked = Tongue.Bake(PartNames);
    public static readonly KeyframeAnimation SwimBaked = Swim.Bake(PartNames);
    public static readonly KeyframeAnimation IdleWaterBaked = IdleWater.Bake(PartNames);

    private static AnimationKeyframe Rot(float t, float x, float y, float z, AnimationInterpolation interpolation) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), interpolation);

    private static AnimationKeyframe Pos(float t, float x, float y, float z, AnimationInterpolation interpolation) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), interpolation);

    private static AnimationKeyframe Sca(float t, float x, float y, float z, AnimationInterpolation interpolation) =>
        new(t, KeyframeAnimations.ScaleVec(x, y, z), interpolation);

    // The tongue's head scale track reuses degreeVec in the reference; reproduce that literal construction.
    private static AnimationKeyframe ScaDeg(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);
    private static AnimationChannel S(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    private static AnimationDefinition BuildCroak() =>
        new(3f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["croaking_body"] =
            [
                P(
                    Pos(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Pos(0.375f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Pos(0.4167f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Pos(0.4583f, 0f, 1f, 0f, AnimationInterpolation.Linear),
                    Pos(2.9583f, 0f, 1f, 0f, AnimationInterpolation.Linear),
                    Pos(3f, 0f, 0f, 0f, AnimationInterpolation.Linear)),
                S(
                    Sca(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Sca(0.375f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Sca(0.4167f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(0.4583f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(0.5417f, 1.3f, 2.1f, 1.6f, AnimationInterpolation.Linear),
                    Sca(0.625f, 1.3f, 2.1f, 1.6f, AnimationInterpolation.Linear),
                    Sca(0.7083f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(2.25f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(2.3333f, 1.3f, 2.1f, 1.6f, AnimationInterpolation.Linear),
                    Sca(2.4167f, 1.3f, 2.1f, 1.6f, AnimationInterpolation.Linear),
                    Sca(2.5f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(2.5833f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(2.6667f, 1.3f, 2.1f, 1.6f, AnimationInterpolation.Linear),
                    Sca(2.875f, 1.3f, 2.1f, 1.6f, AnimationInterpolation.Linear),
                    Sca(2.9583f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(3f, 0f, 0f, 0f, AnimationInterpolation.Linear))
            ]
        });

    private static AnimationDefinition BuildWalk() =>
        new(1.25f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["left_arm"] =
            [
                R(
                    Rot(0f, 0f, -5f, 0f, AnimationInterpolation.Linear),
                    Rot(0.2917f, 7.5f, -2.67f, -7.5f, AnimationInterpolation.Linear),
                    Rot(0.625f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.7917f, 22.5f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(1.125f, -45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(1.25f, 0f, -5f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 0.1f, -2f, AnimationInterpolation.Linear),
                    Pos(0.2917f, -0.5f, -0.25f, -0.13f, AnimationInterpolation.Linear),
                    Pos(0.625f, -0.5f, 0.1f, 2f, AnimationInterpolation.Linear),
                    Pos(0.9583f, 0.5f, 1f, -0.11f, AnimationInterpolation.Linear),
                    Pos(1.25f, 0f, 0.1f, -2f, AnimationInterpolation.Linear))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.125f, 22.5f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.4583f, -45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.625f, 0f, 5f, 0f, AnimationInterpolation.Linear),
                    Rot(0.9583f, 7.5f, 2.33f, 7.5f, AnimationInterpolation.Linear),
                    Rot(1.25f, 0f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0.5f, 0.1f, 2f, AnimationInterpolation.Linear),
                    Pos(0.2917f, -0.5f, 1f, 0.12f, AnimationInterpolation.Linear),
                    Pos(0.625f, 0f, 0.1f, -2f, AnimationInterpolation.Linear),
                    Pos(0.9583f, 0.5f, -0.25f, -0.13f, AnimationInterpolation.Linear),
                    Pos(1.25f, 0.5f, 0.1f, 2f, AnimationInterpolation.Linear))
            ],
            ["left_leg"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.1667f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.2917f, 45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.625f, -45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.7917f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(1.25f, 0f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 0.1f, 1.2f, AnimationInterpolation.Linear),
                    Pos(0.1667f, 0f, 0.1f, 2f, AnimationInterpolation.Linear),
                    Pos(0.4583f, 0f, 2f, 1.06f, AnimationInterpolation.Linear),
                    Pos(0.7917f, 0f, 0.1f, -1f, AnimationInterpolation.Linear),
                    Pos(1.25f, 0f, 0.1f, 1.2f, AnimationInterpolation.Linear))
            ],
            ["right_leg"] =
            [
                R(
                    Rot(0f, -33.75f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.0417f, -45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.1667f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.7917f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.9583f, 45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(1.25f, -33.75f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 1.14f, 0.11f, AnimationInterpolation.Linear),
                    Pos(0.1667f, 0f, 0.1f, -1f, AnimationInterpolation.Linear),
                    Pos(0.7917f, 0f, 0.1f, 2f, AnimationInterpolation.Linear),
                    Pos(1.125f, 0f, 2f, 0.95f, AnimationInterpolation.Linear),
                    Pos(1.25f, 0f, 1.14f, 0.11f, AnimationInterpolation.Linear))
            ],
            ["body"] =
            [
                R(
                    Rot(0f, 0f, 5f, 0f, AnimationInterpolation.Linear),
                    Rot(0.2917f, -7.5f, 0.33f, 7.5f, AnimationInterpolation.Linear),
                    Rot(0.625f, 0f, -5f, 0f, AnimationInterpolation.Linear),
                    Rot(0.9583f, -7.5f, 0.33f, -7.5f, AnimationInterpolation.Linear),
                    Rot(1.25f, 0f, 5f, 0f, AnimationInterpolation.Linear))
            ]
        });

    private static AnimationDefinition BuildJump() =>
        new(0.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    Rot(0f, -22.5f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, -22.5f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Pos(0.5f, 0f, 0f, 0f, AnimationInterpolation.Linear))
            ],
            ["left_arm"] =
            [
                R(
                    Rot(0f, -56.14f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, -56.14f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 1f, 0f, AnimationInterpolation.Linear),
                    Pos(0.5f, 0f, 1f, 0f, AnimationInterpolation.Linear))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, -56.14f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, -56.14f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 1f, 0f, AnimationInterpolation.Linear),
                    Pos(0.5f, 0f, 1f, 0f, AnimationInterpolation.Linear))
            ],
            ["left_leg"] =
            [
                R(
                    Rot(0f, 45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, 45f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Pos(0.5f, 0f, 0f, 0f, AnimationInterpolation.Linear))
            ],
            ["right_leg"] =
            [
                R(
                    Rot(0f, 45f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, 45f, 0f, 0f, AnimationInterpolation.Linear)),
                P(
                    Pos(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Pos(0.5f, 0f, 0f, 0f, AnimationInterpolation.Linear))
            ]
        });

    private static AnimationDefinition BuildTongue() =>
        new(0.5f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["head"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.0833f, -60f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.4167f, -60f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, 0f, 0f, 0f, AnimationInterpolation.Linear)),
                S(
                    ScaDeg(0f, 1f, 1f, 1f),
                    ScaDeg(0.0833f, 0.998f, 1f, 1f),
                    ScaDeg(0.4167f, 0.998f, 1f, 1f),
                    ScaDeg(0.5f, 1f, 1f, 1f))
            ],
            ["tongue"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.0833f, 0f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.4167f, -18f, 0f, 0f, AnimationInterpolation.Linear),
                    Rot(0.5f, 0f, 0f, 0f, AnimationInterpolation.Linear)),
                S(
                    Sca(0.0833f, 1f, 1f, 1f, AnimationInterpolation.Linear),
                    Sca(0.1667f, 0.5f, 1f, 5f, AnimationInterpolation.Linear),
                    Sca(0.4167f, 1f, 1f, 1f, AnimationInterpolation.Linear))
            ]
        });

    private static AnimationDefinition BuildSwim() =>
        new(1.04167f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.3333f, 10f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.6667f, -10f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(1.0417f, 0f, 0f, 0f, AnimationInterpolation.CatmullRom))
            ],
            ["left_arm"] =
            [
                R(
                    Rot(0f, 90f, 22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.4583f, 45f, 22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.6667f, -22.5f, -22.5f, -22.5f, AnimationInterpolation.CatmullRom),
                    Rot(0.875f, -45f, -22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.9583f, 22.5f, 0f, 22.5f, AnimationInterpolation.CatmullRom),
                    Rot(1.0417f, 90f, 22.5f, 0f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, 0f, -0.64f, 2f, AnimationInterpolation.CatmullRom),
                    Pos(0.4583f, 0f, -0.64f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(0.6667f, 0f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(0.875f, 0f, -0.27f, -1.14f, AnimationInterpolation.CatmullRom),
                    Pos(0.9583f, 0f, -1.45f, 0.43f, AnimationInterpolation.CatmullRom),
                    Pos(1.0417f, 0f, -0.64f, 2f, AnimationInterpolation.CatmullRom))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, 90f, -22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.4583f, 45f, -22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.6667f, -22.5f, 22.5f, 22.5f, AnimationInterpolation.CatmullRom),
                    Rot(0.875f, -45f, 22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.9583f, 22.5f, 0f, -22.5f, AnimationInterpolation.CatmullRom),
                    Rot(1.0417f, 90f, -22.5f, 0f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, 0f, -0.64f, 2f, AnimationInterpolation.CatmullRom),
                    Pos(0.4583f, 0f, -0.64f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(0.6667f, 0f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(0.875f, 0f, -0.27f, -1.14f, AnimationInterpolation.CatmullRom),
                    Pos(0.9583f, 0f, -1.45f, 0.43f, AnimationInterpolation.CatmullRom),
                    Pos(1.0417f, 0f, -0.64f, 2f, AnimationInterpolation.CatmullRom))
            ],
            ["left_leg"] =
            [
                R(
                    Rot(0f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.25f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.4583f, 67.5f, -45f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.7917f, 90f, 45f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.9583f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(1.0417f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, -2.5f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(0.25f, -2f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(0.4583f, 1f, -2f, -1f, AnimationInterpolation.CatmullRom),
                    Pos(0.7917f, 0.58f, 0f, -2.83f, AnimationInterpolation.CatmullRom),
                    Pos(0.9583f, -2.5f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(1.0417f, -2.5f, 0f, 1f, AnimationInterpolation.CatmullRom))
            ],
            ["right_leg"] =
            [
                R(
                    Rot(0f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.25f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.4583f, 67.5f, 45f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.7917f, 90f, -45f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(0.9583f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(1.0417f, 90f, 0f, 0f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, 2.5f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(0.25f, 2f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(0.4583f, -1f, -2f, -1f, AnimationInterpolation.CatmullRom),
                    Pos(0.7917f, -0.58f, 0f, -2.83f, AnimationInterpolation.CatmullRom),
                    Pos(0.9583f, 2.5f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(1.0417f, 2.5f, 0f, 1f, AnimationInterpolation.CatmullRom))
            ]
        });

    private static AnimationDefinition BuildIdleWater() =>
        new(3f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(1.625f, -10f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(3f, 0f, 0f, 0f, AnimationInterpolation.CatmullRom))
            ],
            ["left_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, -22.5f, AnimationInterpolation.CatmullRom),
                    Rot(2.2083f, 0f, 0f, -45f, AnimationInterpolation.CatmullRom),
                    Rot(3f, 0f, 0f, -22.5f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, -1f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(2.2083f, -1f, -0.5f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(3f, -1f, 0f, 0f, AnimationInterpolation.CatmullRom))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 22.5f, AnimationInterpolation.CatmullRom),
                    Rot(2.2083f, 0f, 0f, 45f, AnimationInterpolation.CatmullRom),
                    Rot(3f, 0f, 0f, 22.5f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, 1f, 0f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(2.2083f, 1f, -0.5f, 0f, AnimationInterpolation.CatmullRom),
                    Pos(3f, 1f, 0f, 0f, AnimationInterpolation.CatmullRom))
            ],
            ["left_leg"] =
            [
                R(
                    Rot(0f, 22.5f, -22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(1f, 22.5f, -22.5f, -45f, AnimationInterpolation.CatmullRom),
                    Rot(3f, 22.5f, -22.5f, 0f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, 0f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(1f, 0f, -1f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(3f, 0f, 0f, 1f, AnimationInterpolation.CatmullRom))
            ],
            ["right_leg"] =
            [
                R(
                    Rot(0f, 22.5f, 22.5f, 0f, AnimationInterpolation.CatmullRom),
                    Rot(1f, 22.5f, 22.5f, 45f, AnimationInterpolation.CatmullRom),
                    Rot(3f, 22.5f, 22.5f, 0f, AnimationInterpolation.CatmullRom)),
                P(
                    Pos(0f, 0f, 0f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(1f, 0f, -1f, 1f, AnimationInterpolation.CatmullRom),
                    Pos(3f, 0f, 0f, 1f, AnimationInterpolation.CatmullRom))
            ]
        });
}
