// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference CreakingAnimation data, written as behaviour data (the reference hardcodes these in Java, not a resource).
/// Angles are converted to radians, positions use the reference Y inversion and scale is the additive offset produced
/// by <see cref="KeyframeAnimations"/>; every keyframe in these clips uses linear interpolation.
/// </summary>
/// <remarks>
/// The walk clip is driven by the walk animation position/speed while the creature can move. Attack, invulnerable and
/// death are one-shot clips sampled from per-entity <see cref="AnimationState"/> clocks in the reference; this data is
/// clock-agnostic and the controller decides how each clip is fed. All bone names match parts in
/// <see cref="CreakingModelDefinition"/>.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.animation.definitions.CreakingAnimation", "ref://mc-26.3-analysis/src/net/minecraft/client/animation/definitions/CreakingAnimation.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CreakingAnimations
{
    public static readonly AnimationDefinition Walk = BuildWalk();
    public static readonly AnimationDefinition Attack = BuildAttack();
    public static readonly AnimationDefinition Invulnerable = BuildInvulnerable();
    public static readonly AnimationDefinition Death = BuildDeath();

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "root", "upper_body", "head", "body", "right_arm", "left_arm", "left_leg", "right_leg",
    };

    public static readonly KeyframeAnimation WalkBaked = Walk.Bake(PartNames);
    public static readonly KeyframeAnimation AttackBaked = Attack.Bake(PartNames);
    public static readonly KeyframeAnimation InvulnerableBaked = Invulnerable.Bake(PartNames);
    public static readonly KeyframeAnimation DeathBaked = Death.Bake(PartNames);

    private static AnimationKeyframe Rot(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.DegreeVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationKeyframe Pos(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.PosVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationKeyframe Sca(float t, float x, float y, float z) =>
        new(t, KeyframeAnimations.ScaleVec(x, y, z), AnimationInterpolation.Linear);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);
    private static AnimationChannel S(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    private static AnimationDefinition BuildWalk() =>
        new(1.125f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["upper_body"] = [R(
                Rot(0f, 26.8802f, -23.399f, -9.0616f),
                Rot(0.125f, -2.2093f, 5.9119f, 0.0675f),
                Rot(0.5417f, 23.0778f, 14.2906f, 4.6066f),
                Rot(0.7083f, -10f, 0f, 0f),
                Rot(0.875f, 7.5f, 0f, 0f),
                Rot(1.125f, 26.8802f, -23.399f, -9.0616f))],
            ["head"] = [R(
                Rot(0f, 0f, 0f, 0f),
                Rot(0.0417f, -17.5f, -62.5f, 0f),
                Rot(0.0833f, 0f, 0f, 0f),
                Rot(0.4167f, 0f, 0f, 0f),
                Rot(0.4583f, 0f, 15f, 0f),
                Rot(0.5f, 0f, 0f, 0f),
                Rot(1.0417f, 0f, 0f, 0f),
                Rot(1.0833f, -37.1532f, 81.1131f, -28.3621f),
                Rot(1.125f, 0f, 0f, 0f))],
            ["right_arm"] = [R(
                Rot(0f, 12.5f, 0f, 0f),
                Rot(0.25f, -32f, 0f, 0f),
                Rot(0.875f, 12f, 0f, 0f),
                Rot(1.125f, -15f, 0f, 0f))],
            ["left_arm"] = [R(
                Rot(0f, -15f, 0f, 0f),
                Rot(0.125f, 10f, 0f, 0f),
                Rot(0.5417f, -25f, 0f, 0f),
                Rot(0.75f, -9.0923f, 0f, 0f),
                Rot(0.7917f, -15.137f, -66.7758f, 13.9603f),
                Rot(0.8333f, -9.0923f, 0f, 0f),
                Rot(1f, 10f, 0f, 0f),
                Rot(1.125f, -15f, 0f, 0f))],
            ["left_leg"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.25f, 30f, 0f, 0f),
                    Rot(0.375f, 49.8924f, -3.8282f, 3.2187f),
                    Rot(0.5f, 17.5f, 0f, 0f),
                    Rot(0.625f, -56.5613f, -12.2403f, -8.7374f),
                    Rot(0.9167f, 0f, 0f, 0f),
                    Rot(1.125f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 2f),
                    Pos(0.25f, 0f, 0.1846f, 0.5979f),
                    Pos(0.375f, 0f, -0.0665f, -2.2177f),
                    Pos(0.5f, 0f, 1.3563f, -4.3474f),
                    Pos(0.625f, 0f, 0.1047f, -1.6556f),
                    Pos(0.9167f, 0f, 0f, -1f),
                    Pos(1.125f, 0f, 0f, 2f))
            ],
            ["right_leg"] =
            [
                R(
                    Rot(0f, 25.5305f, 11.3125f, 5.3525f),
                    Rot(0.125f, -49.5628f, 7.3556f, 6.7933f),
                    Rot(0.25f, 0f, 0f, 0f),
                    Rot(0.4583f, 0f, 0f, 0f),
                    Rot(0.9167f, 30f, 0f, 0f),
                    Rot(1.0417f, 55f, 0f, 0f),
                    Rot(1.125f, 25.5305f, 11.3125f, 5.3525f)),
                P(
                    Pos(0f, 0f, 0.9674f, -3.6578f),
                    Pos(0.125f, 0f, -0.2979f, -0.9411f),
                    Pos(0.25f, 0f, -0.3f, -0.94f),
                    Pos(0.4583f, 0f, -0.3f, 1.06f),
                    Pos(1.125f, 0f, 0.9674f, -3.6578f))
            ],
        });

    private static AnimationDefinition BuildAttack() =>
        new(0.7083f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["upper_body"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.0833f, 0f, 45f, 0f),
                    Rot(0.1667f, -115f, 67.5f, -90f),
                    Rot(0.375f, 67.5f, 0f, 0f),
                    Rot(0.5417f, 0f, 45f, 0f),
                    Rot(0.7083f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.0833f, 0f, 0f, 0f),
                    Pos(0.2917f, 0f, -2.7716f, -1.1481f),
                    Pos(0.375f, 0f, 0f, 0f),
                    Pos(0.5417f, 0f, 0f, 0f),
                    Pos(0.7083f, 0f, 0f, 0f)),
                S(
                    Sca(0f, 1f, 1f, 1f),
                    Sca(0.7083f, 1f, 1f, 1f))
            ],
            ["head"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.1667f, 0f, -45f, 0f),
                    Rot(0.25f, -11.25f, -45f, 0f),
                    Rot(0.2917f, -117.3939f, 76.6331f, -130.1483f),
                    Rot(0.4167f, -45f, -45f, 0f),
                    Rot(0.5f, 60f, -45f, 0f),
                    Rot(0.5833f, 60f, -45f, 0f),
                    Rot(0.625f, 0f, -45f, 0f),
                    Rot(0.7083f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.1667f, 0f, 0f, 0f),
                    Pos(0.4167f, 0f, 0f, 0f),
                    Pos(0.5f, 0.3827f, 0.5133f, -0.7682f),
                    Pos(0.5833f, 0.3827f, 0.5133f, -0.7682f),
                    Pos(0.625f, 0f, 0f, 0f),
                    Pos(0.7083f, 0f, 0f, 0f)),
                S(
                    Sca(0.1667f, 1f, 1f, 1f),
                    Sca(0.4167f, 1f, 1f, 1f),
                    Sca(0.5f, 1f, 1.3f, 1f),
                    Sca(0.625f, 1f, 1f, 1f))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.1667f, 0f, 0f, 0f),
                    Rot(0.25f, 7.5f, 0f, 0f),
                    Rot(0.4583f, 55f, 0f, 0f),
                    Rot(0.625f, 0f, 0f, 0f),
                    Rot(0.7083f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.1667f, 0f, 0f, 0f),
                    Pos(0.625f, 0f, 0f, 0f),
                    Pos(0.7083f, 0f, 0f, 0f))
            ],
            ["left_leg"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.1667f, 0f, 0f, 0f),
                    Rot(0.625f, 0f, 0f, 0f),
                    Rot(0.7083f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.1667f, 0f, 0f, -2f),
                    Pos(0.625f, 0f, 0f, -2f),
                    Pos(0.7083f, 0f, 0f, 0f))
            ],
            ["right_leg"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.1667f, 0f, 45f, 0f),
                    Rot(0.625f, 0f, 45f, 0f),
                    Rot(0.7083f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.1667f, 0.7071f, 0f, 0f),
                    Pos(0.625f, 0.7071f, 0f, 0f),
                    Pos(0.7083f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.1667f, 0f, 0f, 0f),
                    Rot(0.25f, 10.3453f, 14.7669f, 2.664f),
                    Rot(0.4583f, 57.5f, 0f, 0f),
                    Rot(0.625f, 0f, 0f, 0f),
                    Rot(0.7083f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.7083f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildInvulnerable() =>
        new(0.2917f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["upper_body"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.0833f, -5f, 0f, 0f),
                    Rot(0.1667f, 5f, 0f, 0f),
                    Rot(0.25f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.0833f, 0f, 0f, 0f),
                    Pos(0.25f, 0f, 0f, 0f))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.0833f, 17.5f, 0f, 0f),
                    Rot(0.1667f, -15f, 0f, 0f),
                    Rot(0.25f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.25f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.0833f, 20f, 0f, 0f),
                    Rot(0.1667f, -15f, 0f, 0f),
                    Rot(0.25f, 0f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.25f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildDeath() =>
        new(2.25f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["upper_body"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.0833f, -40f, 0f, 0f),
                    Rot(0.1667f, -5f, 0f, 0f),
                    Rot(0.2917f, 7.5f, 0f, 0f),
                    Rot(0.5833f, 16.25f, 0f, 0f),
                    Rot(0.6667f, 29.0814f, 62.5516f, 26.5771f),
                    Rot(0.75f, 12.2115f, 0f, 0f),
                    Rot(1f, 10.25f, 0f, 0f),
                    Rot(1.0417f, -47.64f, 0f, 0f),
                    Rot(1.125f, 21.96f, 0f, 0f),
                    Rot(1.25f, 12.5f, 0f, 0f),
                    Rot(2.25f, 17.3266f, 7.9022f, -0.1381f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.0833f, 0f, 0.557f, 1.2659f),
                    Pos(0.1667f, 0f, -2.0889f, -0.3493f),
                    Pos(0.2917f, 0f, 0f, 0f)),
                S(
                    Sca(0f, 1f, 1f, 1f),
                    Sca(0.0833f, 1f, 1.1f, 1f),
                    Sca(0.1667f, 1f, 0.9f, 1f),
                    Sca(0.2917f, 1f, 1f, 1f))
            ],
            ["right_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.2917f, -10f, 0f, 0f),
                    Rot(0.5f, 0f, 0f, 0f),
                    Rot(1.25f, -10f, 0f, 0f),
                    Rot(1.5417f, -10f, 0f, 0f),
                    Rot(1.5833f, -12.1479f, -34.3927f, 6.9326f),
                    Rot(1.6667f, -10f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.2917f, 0f, 0f, 0f))
            ],
            ["left_arm"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.2917f, -10f, 0f, 0f),
                    Rot(0.5f, 0f, 0f, 0f),
                    Rot(0.8333f, -4.4444f, 0f, 0f),
                    Rot(0.875f, -26.7402f, -78.831f, 26.3025f),
                    Rot(0.9583f, -5.5556f, 0f, 0f),
                    Rot(1.25f, -10f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.2917f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(
                    Rot(0f, 0f, 0f, 0f),
                    Rot(0.0833f, -5f, 0f, 0f),
                    Rot(0.2917f, 10f, 0f, 0f),
                    Rot(0.5f, 2.5f, 0f, 0f),
                    Rot(0.5417f, 5.5f, 0f, 0f),
                    Rot(0.5833f, -67.4168f, -12.9552f, -8.0231f),
                    Rot(0.6667f, 8.5f, 0f, 0f),
                    Rot(1f, 10.773f, -29.5608f, -5.3627f),
                    Rot(1.25f, 10f, 0f, 0f),
                    Rot(1.7917f, 10f, 0f, 0f),
                    Rot(1.8333f, 12.9625f, 39.2735f, 8.2901f),
                    Rot(1.9167f, 10f, 0f, 0f)),
                P(
                    Pos(0f, 0f, 0f, 0f),
                    Pos(0.2917f, 0f, 0f, 0f))
            ],
        });
}
