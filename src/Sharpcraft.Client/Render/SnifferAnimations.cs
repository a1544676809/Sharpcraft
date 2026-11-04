// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference SnifferAnimation data, written as behaviour data (the reference hardcodes these clips in Java, not JSON).
/// The five state-driven clips (feeling_happy, scenting, sniffing, digging, rising) are exposed through
/// <see cref="SnifferAnimationProvider.ApplyClip"/>; the walk and sniff-search cycles are driven every frame by
/// <c>applyWalk(walkPos, walkSpeed, 9, 100)</c>. Part names match <see cref="SnifferModelDefinition"/>; positions use
/// the reference Y inversion through <see cref="KeyframeAnimations.PosVec"/>, angles are converted from degrees to
/// radians through <see cref="KeyframeAnimations.DegreeVec"/>, and scales are stored additively through
/// <see cref="KeyframeAnimations.ScaleVec"/>.
/// </summary>
/// <remarks>
/// Clip mapping: feeling_happy = SNIFFER_HAPPY (looping), scenting = SNIFFER_SNIFFSNIFF (looping, nose only),
/// sniffing = SNIFFER_LONGSNIFF (nose + head), digging = SNIFFER_DIG, rising = SNIFFER_STAND_UP. The walk and
/// sniff-search cycles are both 2.0 s loops. A keyframe's interpolation value applies to the segment that ends at that
/// keyframe (the reference evaluates <c>nextFrame.interpolation()</c>), so each timestamp carries the reference's own
/// interpolation rather than a per-channel default.
/// </remarks>
public static class SnifferAnimations
{
    /// <summary>Feeling-happy clip (reference <c>SNIFFER_HAPPY</c>, 2.0 s loop).</summary>
    public static readonly AnimationDefinition FeelingHappy = BuildFeelingHappy();

    /// <summary>Scenting clip (reference <c>SNIFFER_SNIFFSNIFF</c>, 8.0 s loop, nose only).</summary>
    public static readonly AnimationDefinition Scenting = BuildScenting();

    /// <summary>Sniffing clip (reference <c>SNIFFER_LONGSNIFF</c>, 1.0 s one-shot).</summary>
    public static readonly AnimationDefinition Sniffing = BuildSniffing();

    /// <summary>Digging clip (reference <c>SNIFFER_DIG</c>, 8.0 s one-shot).</summary>
    public static readonly AnimationDefinition Digging = BuildDigging();

    /// <summary>Rising clip (reference <c>SNIFFER_STAND_UP</c>, 3.0 s one-shot).</summary>
    public static readonly AnimationDefinition Rising = BuildRising();

    /// <summary>Walk cycle (reference <c>SNIFFER_WALK</c>, 2.0 s loop) driven by <c>applyWalk</c>.</summary>
    public static readonly AnimationDefinition Walk = BuildWalk();

    /// <summary>Sniff-search cycle (reference <c>SNIFFER_SNIFF_SEARCH</c>, 2.0 s loop) selected while searching.</summary>
    public static readonly AnimationDefinition SniffSearch = BuildSniffSearch();

    private static AnimationKeyframe Lin(float t, Vector3 v) => new(t, v, AnimationInterpolation.Linear);
    private static AnimationKeyframe Cat(float t, Vector3 v) => new(t, v, AnimationInterpolation.CatmullRom);

    private static AnimationChannel R(params AnimationKeyframe[] frames) => new(AnimationTarget.Rotation, frames);
    private static AnimationChannel P(params AnimationKeyframe[] frames) => new(AnimationTarget.Position, frames);
    private static AnimationChannel S(params AnimationKeyframe[] frames) => new(AnimationTarget.Scale, frames);

    private static IReadOnlyDictionary<string, IReadOnlyList<AnimationChannel>> Tracks(
        params (string Part, AnimationChannel[] Channels)[] tracks)
    {
        var map = new Dictionary<string, IReadOnlyList<AnimationChannel>>(tracks.Length, StringComparer.Ordinal);
        foreach (var (part, channels) in tracks)
        {
            map[part] = channels;
        }

        return map;
    }

    private static AnimationDefinition BuildFeelingHappy() => new(2f, true, Tracks(
        ("head", [R(
            Cat(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Cat(0.5f, KeyframeAnimations.DegreeVec(-32.00206f, 19.3546f, -11.70092f)),
            Cat(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Cat(1.5f, KeyframeAnimations.DegreeVec(-32.00206f, -19.3546f, 11.70092f)),
            Cat(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))]),
        ("left_ear", [R(
            Cat(0.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Cat(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, -67.5f)),
            Cat(0.9583f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Cat(1.125f, KeyframeAnimations.DegreeVec(0f, 0f, -67.5f)),
            Cat(1.2917f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))]),
        ("right_ear", [R(
            Cat(0.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Cat(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 67.5f)),
            Cat(0.9583f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Cat(1.125f, KeyframeAnimations.DegreeVec(0f, 0f, 67.5f)),
            Cat(1.2917f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))])));

    private static AnimationDefinition BuildScenting() => new(8f, true, Tracks(
        ("nose", [S(
            Lin(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Lin(0.5417f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.5833f, KeyframeAnimations.ScaleVec(1f, 0.5f, 1f)),
            Cat(0.6667f, KeyframeAnimations.ScaleVec(1f, 2.5f, 1f)),
            Cat(0.7917f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.9167f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(1f, KeyframeAnimations.ScaleVec(1f, 3f, 1f)),
            Lin(1.125f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Lin(2f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))])));

    private static AnimationDefinition BuildSniffing() => new(1f, false, Tracks(
        ("nose", [S(
            Cat(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.0833f, KeyframeAnimations.ScaleVec(1f, 0.7f, 1f)),
            Cat(0.125f, KeyframeAnimations.ScaleVec(1f, 3f, 1f)),
            Cat(0.25f, KeyframeAnimations.ScaleVec(1f, 3f, 1f)),
            Cat(0.7083f, KeyframeAnimations.ScaleVec(1f, 4f, 1f)),
            Cat(0.8333f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(1f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))]),
        ("head", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Lin(0.125f, KeyframeAnimations.DegreeVec(-5f, 0f, 0f)),
            Lin(0.875f, KeyframeAnimations.DegreeVec(-20f, 0f, 0f)),
            Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)))])));

    private static AnimationDefinition BuildDigging() => new(8f, false, Tracks(
        ("body",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.5f, KeyframeAnimations.DegreeVec(1.5f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.DegreeVec(-5f, 0f, 0f)),
                Lin(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2.5f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
                Lin(3f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(3.5f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
                Lin(4f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(4.5f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
                Lin(5.6667f, KeyframeAnimations.DegreeVec(5f, 0f, 0f)),
                Lin(5.8333f, KeyframeAnimations.DegreeVec(-2.5f, 0f, 0f)),
                Lin(6f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(1.5f, KeyframeAnimations.PosVec(0f, -7f, 0f))),
            S(
                Lin(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                Lin(1.5f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
                Lin(1.5417f, KeyframeAnimations.ScaleVec(1.04f, 0.98f, 1.02f)),
                Lin(1.5833f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))
        ]),
        ("head",
        [
            R(
                Cat(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Cat(1.1667f, KeyframeAnimations.DegreeVec(10f, 0f, 0f)),
                Cat(1.4167f, KeyframeAnimations.DegreeVec(-10f, 0f, 0f)),
                Cat(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Cat(1.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Cat(1.875f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Cat(2.0833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Cat(2.5f, KeyframeAnimations.DegreeVec(47.5f, 0f, 0f)),
                Cat(2.6667f, KeyframeAnimations.DegreeVec(38.44f, 0f, 0f)),
                Cat(2.875f, KeyframeAnimations.DegreeVec(10.95951f, 13.57454f, -14.93501f)),
                Cat(3.2083f, KeyframeAnimations.DegreeVec(47.5f, 0f, 0f)),
                Cat(3.5833f, KeyframeAnimations.DegreeVec(55f, 0f, 0f)),
                Cat(3.7917f, KeyframeAnimations.DegreeVec(4.2932f, -16.187f, 10.90042f)),
                Cat(4.125f, KeyframeAnimations.DegreeVec(47.5f, 0f, 0f)),
                Cat(4.4167f, KeyframeAnimations.DegreeVec(54.71135f, 7.98009f, -5.56662f)),
                Cat(4.5f, KeyframeAnimations.DegreeVec(55.72895f, -6.77684f, 4.46197f)),
                Cat(4.5833f, KeyframeAnimations.DegreeVec(54.71135f, 7.98009f, -5.56662f)),
                Cat(4.6667f, KeyframeAnimations.DegreeVec(55.72895f, -6.77684f, 4.46197f)),
                Cat(4.75f, KeyframeAnimations.DegreeVec(54.71135f, 7.98009f, -5.56662f)),
                Cat(4.8333f, KeyframeAnimations.DegreeVec(55.72895f, -6.77684f, 4.46197f)),
                Cat(5f, KeyframeAnimations.DegreeVec(65f, 0f, 0f)),
                Cat(5.75f, KeyframeAnimations.DegreeVec(65f, 0f, 0f)),
                Cat(5.9167f, KeyframeAnimations.DegreeVec(-32.5f, 0f, 0f)),
                Lin(6.25f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.625f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.375f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(1.5f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(1.5833f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(1.875f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(2.0833f, KeyframeAnimations.PosVec(0f, 3f, 0f)),
                Lin(2.2917f, KeyframeAnimations.PosVec(0f, 6f, 0f)),
                Lin(2.6667f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(3.2083f, KeyframeAnimations.PosVec(0f, 4f, 0f)),
                Lin(3.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(4.125f, KeyframeAnimations.PosVec(0f, 4f, 0f)),
                Lin(5f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(5.75f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(6f, KeyframeAnimations.PosVec(0f, 1.5f, 0f)),
                Lin(6.25f, KeyframeAnimations.PosVec(0f, 1f, 0f)))
        ]),
        ("left_ear", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Lin(1.25f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Lin(1.4167f, KeyframeAnimations.DegreeVec(0f, 0f, -50f)),
            Lin(1.5833f, KeyframeAnimations.DegreeVec(0f, 0f, -30f)),
            Lin(5.9167f, KeyframeAnimations.DegreeVec(0f, 0f, -30f)),
            Lin(6.0833f, KeyframeAnimations.DegreeVec(0f, 0f, -65f)),
            Lin(6.3333f, KeyframeAnimations.DegreeVec(0f, 0f, -30f)))]),
        ("right_ear", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Lin(1.25f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Lin(1.4167f, KeyframeAnimations.DegreeVec(0f, 0f, 50f)),
            Lin(1.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 30f)),
            Lin(5.9167f, KeyframeAnimations.DegreeVec(0f, 0f, 30f)),
            Lin(6.0833f, KeyframeAnimations.DegreeVec(0f, 0f, 65f)),
            Lin(6.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 30f)))]),
        ("right_front_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.2083f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.375f, KeyframeAnimations.DegreeVec(0f, 0f, 90f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.2083f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.2917f, KeyframeAnimations.PosVec(-2f, -0.75f, 0f)),
                Lin(1.375f, KeyframeAnimations.PosVec(-4f, -5.5f, 0f)))
        ]),
        ("right_mid_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.25f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.4167f, KeyframeAnimations.DegreeVec(0f, 0f, 90f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.25f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(-2f, -0.75f, 0f)),
                Lin(1.4167f, KeyframeAnimations.PosVec(-4f, -5.5f, 0f)))
        ]),
        ("right_hind_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 90f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.4167f, KeyframeAnimations.PosVec(-2f, -0.75f, 0f)),
                Lin(1.5f, KeyframeAnimations.PosVec(-4f, -5.5f, 0f)))
        ]),
        ("left_front_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.2083f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.375f, KeyframeAnimations.DegreeVec(0f, 0f, -90f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.2083f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.2917f, KeyframeAnimations.PosVec(2f, -0.75f, 0f)),
                Lin(1.375f, KeyframeAnimations.PosVec(4f, -5.5f, 0f)))
        ]),
        ("left_mid_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.25f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.4167f, KeyframeAnimations.DegreeVec(0f, 0f, -90f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.25f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(2f, -0.75f, 0f)),
                Lin(1.4167f, KeyframeAnimations.PosVec(4f, -5.5f, 0f)))
        ]),
        ("left_hind_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, -90f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.4167f, KeyframeAnimations.PosVec(2f, -0.75f, 0f)),
                Lin(1.5f, KeyframeAnimations.PosVec(4f, -5.5f, 0f)))
        ])));

    private static AnimationDefinition BuildRising() => new(3f, false, Tracks(
        ("body",
        [
            R(
                Lin(0.25f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
                Lin(1.5f, KeyframeAnimations.DegreeVec(-2.5f, 0f, 0f)),
                Lin(1.7083f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0.25f, KeyframeAnimations.PosVec(0f, -7f, 0f)),
                Lin(0.75f, KeyframeAnimations.PosVec(0f, -7f, 0f)),
                Lin(1.5f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.7083f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("head",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.3333f, KeyframeAnimations.DegreeVec(-5f, 0f, 0f)),
                Lin(0.7083f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(10f, 0f, 0f)),
                Lin(1.375f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 1f, 0f)),
                Lin(1.375f, KeyframeAnimations.PosVec(0f, 1f, 0f)))
        ]),
        ("left_ear", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, -30f)),
            Lin(0.9167f, KeyframeAnimations.DegreeVec(0f, 0f, -30f)),
            Lin(1.2083f, KeyframeAnimations.DegreeVec(0f, 0f, -5f)))]),
        ("right_ear", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 30f)),
            Lin(0.9167f, KeyframeAnimations.DegreeVec(0f, 0f, 30f)),
            Lin(1.2083f, KeyframeAnimations.DegreeVec(0f, 0f, 5f)))]),
        ("right_front_leg",
        [
            R(
                Cat(0f, KeyframeAnimations.DegreeVec(0f, 0f, 90f)),
                Cat(0.4583f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Cat(0f, KeyframeAnimations.PosVec(-4f, -5.5f, 0f)),
                Cat(0.2083f, KeyframeAnimations.PosVec(6f, -5.5f, 0f)),
                Cat(0.4583f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("right_mid_leg",
        [
            R(
                Cat(0.0833f, KeyframeAnimations.DegreeVec(0f, 0f, 90f)),
                Cat(0.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Cat(0.0833f, KeyframeAnimations.PosVec(-4f, -5.5f, 0f)),
                Cat(0.3333f, KeyframeAnimations.PosVec(6f, -5.5f, 0f)),
                Cat(0.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("right_hind_leg",
        [
            R(
                Cat(0.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 90f)),
                Cat(0.6667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Cat(0.1667f, KeyframeAnimations.PosVec(-4f, -5.5f, 0f)),
                Cat(0.4167f, KeyframeAnimations.PosVec(6f, -5.5f, 0f)),
                Cat(0.6667f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("left_front_leg",
        [
            R(
                Cat(0f, KeyframeAnimations.DegreeVec(0f, 0f, -90f)),
                Cat(0.4583f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Cat(0f, KeyframeAnimations.PosVec(4f, -5.5f, 0f)),
                Cat(0.2083f, KeyframeAnimations.PosVec(-6f, -5.5f, 0f)),
                Cat(0.4583f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("left_mid_leg",
        [
            R(
                Cat(0.0833f, KeyframeAnimations.DegreeVec(0f, 0f, -90f)),
                Cat(0.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Cat(0.0833f, KeyframeAnimations.PosVec(4f, -5.5f, 0f)),
                Cat(0.3333f, KeyframeAnimations.PosVec(-6f, -5.5f, 0f)),
                Cat(0.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("left_hind_leg",
        [
            R(
                Cat(0.1667f, KeyframeAnimations.DegreeVec(0f, 0f, -90f)),
                Cat(0.6667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Cat(0.1667f, KeyframeAnimations.PosVec(4f, -5.5f, 0f)),
                Cat(0.4167f, KeyframeAnimations.PosVec(-6f, -5.5f, 0f)),
                Cat(0.6667f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ])));

    private static AnimationDefinition BuildWalk() => new(2f, true, Tracks(
        ("right_front_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.5833f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(0.75f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.PosVec(0f, 0f, -1f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 3f)))
        ]),
        ("right_mid_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(-7f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(0.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.75f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(-7f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 2.67f, -0.67f)),
                Lin(0.1667f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.3333f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(1.1667f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(1.9167f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 2.67f, -0.67f)))
        ]),
        ("right_hind_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(25f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(1.5833f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(1.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, -0.5f)),
                Lin(0.5833f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 2.22f, 0.78f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(1.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.75f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, -0.5f)))
        ]),
        ("left_front_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.5833f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.PosVec(0f, 0f, -1f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(1.75f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("left_mid_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(0.1667f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(0.9167f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(1.1667f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 2f)))
        ]),
        ("left_hind_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(25f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(0.5833f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(25f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 2.22f, 0.78f)),
                Lin(0.3333f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(0.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, -0.5f)),
                Lin(1.5833f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 2.22f, 0.78f)))
        ]),
        ("body",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(1f, 0f, -2.5f)),
                Lin(0.5f, KeyframeAnimations.DegreeVec(-1f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(1f, 0f, 2.5f)),
                Lin(1.5f, KeyframeAnimations.DegreeVec(-1f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(1f, 0f, -2.5f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.2083f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.375f, KeyframeAnimations.PosVec(0f, -1f, 0f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.2083f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.375f, KeyframeAnimations.PosVec(0f, -1f, 0f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("head", [R(
            Cat(0f, KeyframeAnimations.DegreeVec(7.5f, 0f, 0f)),
            Cat(0.1667f, KeyframeAnimations.DegreeVec(9f, 0f, 0f)),
            Cat(0.875f, KeyframeAnimations.DegreeVec(-1f, 0f, 0f)),
            Cat(1.25f, KeyframeAnimations.DegreeVec(7f, 0f, 0f)),
            Cat(1.75f, KeyframeAnimations.DegreeVec(5f, 0f, 0f)),
            Cat(2f, KeyframeAnimations.DegreeVec(7.5f, 0f, 0f)))]),
        ("left_ear", [R(
            Cat(0f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Cat(0.5f, KeyframeAnimations.DegreeVec(0f, 0f, -7.5f)),
            Cat(1f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Cat(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, -7.5f)),
            Cat(2f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)))]),
        ("right_ear", [R(
            Cat(0f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Cat(0.5f, KeyframeAnimations.DegreeVec(0f, 0f, 7.5f)),
            Cat(1f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Cat(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 7.5f)),
            Cat(2f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)))])));

    private static AnimationDefinition BuildSniffSearch() => new(2f, true, Tracks(
        ("right_front_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.5833f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(0.75f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.PosVec(0f, 0f, -1f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 3f)))
        ]),
        ("right_mid_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(-7f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(0.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.75f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(-7f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 2.67f, -0.67f)),
                Lin(0.1667f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.3333f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(1.1667f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(1.9167f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 2.67f, -0.67f)))
        ]),
        ("right_hind_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(25f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(1.5833f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(1.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, -0.5f)),
                Lin(0.5833f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 2.22f, 0.78f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(1.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.75f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, -0.5f)))
        ]),
        ("left_front_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.5833f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.PosVec(0f, 0f, -1f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(1.75f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 0f)))
        ]),
        ("left_mid_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(1.1667f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(0.1667f, KeyframeAnimations.PosVec(0f, 0f, 3f)),
                Lin(0.9167f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(1.1667f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(1.3333f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 0f, 2f)))
        ]),
        ("left_hind_leg",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(25f, 0f, 0f)),
                Lin(0.1667f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(0.5833f, KeyframeAnimations.DegreeVec(-35f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(1.5833f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
                Lin(2f, KeyframeAnimations.DegreeVec(25f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, 2.22f, 0.78f)),
                Lin(0.3333f, KeyframeAnimations.PosVec(0f, 4f, -1f)),
                Lin(0.5833f, KeyframeAnimations.PosVec(0f, 0f, 0f)),
                Lin(0.75f, KeyframeAnimations.PosVec(0f, 0f, -2f)),
                Lin(1f, KeyframeAnimations.PosVec(0f, 0f, -0.5f)),
                Lin(1.5833f, KeyframeAnimations.PosVec(0f, 0f, 2f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, 2.22f, 0.78f)))
        ]),
        ("body", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)),
            Lin(0.5f, KeyframeAnimations.DegreeVec(1.25f, 0f, 0f)),
            Lin(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 0f)),
            Lin(2f, KeyframeAnimations.DegreeVec(2.5f, 0f, 0f)))]),
        ("head",
        [
            R(
                Lin(0f, KeyframeAnimations.DegreeVec(35f, 0f, 0f)),
                Lin(0.125f, KeyframeAnimations.DegreeVec(33.61503f, 11.46526f, 9.803f)),
                Lin(0.875f, KeyframeAnimations.DegreeVec(34.71128f, 17.67415f, 14.15251f)),
                Lin(1.125f, KeyframeAnimations.DegreeVec(37.21128f, -17.67415f, -14.15251f)),
                Lin(1.875f, KeyframeAnimations.DegreeVec(38.30529f, -21.62827f, -17.40292f)),
                Lin(2f, KeyframeAnimations.DegreeVec(35f, 0f, 0f))),
            P(
                Lin(0f, KeyframeAnimations.PosVec(0f, -2f, 0f)),
                Lin(2f, KeyframeAnimations.PosVec(0f, -2f, 0f)))
        ]),
        ("left_ear", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Lin(0.25f, KeyframeAnimations.DegreeVec(0f, 0f, -15f)),
            Lin(0.5f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Lin(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, -15f)),
            Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Lin(1.25f, KeyframeAnimations.DegreeVec(0f, 0f, -15f)),
            Lin(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)),
            Lin(1.75f, KeyframeAnimations.DegreeVec(0f, 0f, -15f)),
            Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, -2.5f)))]),
        ("right_ear", [R(
            Lin(0f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Lin(0.25f, KeyframeAnimations.DegreeVec(0f, 0f, 15f)),
            Lin(0.5f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Lin(0.75f, KeyframeAnimations.DegreeVec(0f, 0f, 15f)),
            Lin(1f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Lin(1.25f, KeyframeAnimations.DegreeVec(0f, 0f, 15f)),
            Lin(1.5f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)),
            Lin(1.75f, KeyframeAnimations.DegreeVec(0f, 0f, 15f)),
            Lin(2f, KeyframeAnimations.DegreeVec(0f, 0f, 2.5f)))]),
        ("nose", [S(
            Cat(0f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.0833f, KeyframeAnimations.ScaleVec(1f, 1.5f, 1f)),
            Cat(0.2083f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.375f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.4583f, KeyframeAnimations.ScaleVec(1f, 2.5f, 1f)),
            Cat(0.625f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.8333f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(0.9167f, KeyframeAnimations.ScaleVec(1f, 2.5f, 1f)),
            Cat(1.0833f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(1.2917f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(1.3333f, KeyframeAnimations.ScaleVec(1f, 2.5f, 1f)),
            Cat(1.5f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(1.625f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(1.6667f, KeyframeAnimations.ScaleVec(1f, 3.5f, 1f)),
            Cat(1.8333f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)),
            Cat(2f, KeyframeAnimations.ScaleVec(1f, 1f, 1f)))])));

    private static readonly IReadOnlySet<string> PartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "bone", "body", "body_outer", "body_bottom",
        "right_front_leg", "right_mid_leg", "right_hind_leg",
        "left_front_leg", "left_mid_leg", "left_hind_leg",
        "head", "head_flat", "left_ear", "right_ear", "nose", "lower_beak"
    };

    public static readonly KeyframeAnimation FeelingHappyBaked = FeelingHappy.Bake(PartNames);
    public static readonly KeyframeAnimation ScentingBaked = Scenting.Bake(PartNames);
    public static readonly KeyframeAnimation SniffingBaked = Sniffing.Bake(PartNames);
    public static readonly KeyframeAnimation DiggingBaked = Digging.Bake(PartNames);
    public static readonly KeyframeAnimation RisingBaked = Rising.Bake(PartNames);
    public static readonly KeyframeAnimation WalkBaked = Walk.Bake(PartNames);
    public static readonly KeyframeAnimation SniffSearchBaked = SniffSearch.Bake(PartNames);
}
