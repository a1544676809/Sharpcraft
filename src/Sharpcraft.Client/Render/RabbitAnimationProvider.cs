// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Rabbit animation. The reference <c>RabbitModel.setupAnim</c> applies a HOP clip while the jump clock is active, an
/// IDLE_HEAD_TILT clip on the random idle timer, and otherwise lets the head look along the render-state pitch/yaw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hop:</b> looping 0.75 s clip sampled at <c>jumpTicks * 50 ms</c>; the jump is 15 ticks = 0.75 s, so elapsed seconds
/// = <c>JumpCompletion * 0.75</c> exactly. Driven from <see cref="EntityAnimationFrame.IsJumping"/>.
/// </para>
/// <para>
/// <b>Idle head tilt:</b> non-looping 4 s clip; sampled at <c>IdleHeadTiltAgeTicks / 20</c> seconds (AnimationState-style
/// elapsed ticks). Driven from <see cref="EntityAnimationFrame.IsIdleHeadTiltStarted"/>; the random start tick is owned by
/// the per-entity RNG state (ADR-0025), not faked here. When started, the head-look branch is suppressed, matching the
/// reference.
/// </para>
/// <para>Keyframe data written as behaviour data from <c>RabbitAnimation</c>; no Java copied. Baby rabbits use a different
/// mesh/animation and are out of scope.</para>
/// </remarks>
public sealed class RabbitAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "hop", "idle_head_tilt"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference HOP length in seconds (0.75 s = 15 ticks).</summary>
    private const float HopLengthSeconds = 0.75f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        // The head look applies only while the idle-head-tilt clock is not started (reference branch).
        if (!frame.IsIdleHeadTiltStarted)
        {
            pose.Add("head", new PoseOffset(
                Vector3.Zero,
                new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
                Vector3.Zero));
        }

        if (frame.IsJumping)
        {
            HopBaked.Apply(frame.JumpCompletion * HopLengthSeconds, 1f, pose);
        }

        if (frame.IsIdleHeadTiltStarted)
        {
            IdleHeadTiltBaked.Apply(frame.IdleHeadTiltAgeTicks / 20f, 1f, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "hop", StringComparison.Ordinal))
        {
            HopBaked.Apply(seconds, 1f, pose);
        }
        else if (string.Equals(clip, "idle_head_tilt", StringComparison.Ordinal))
        {
            IdleHeadTiltBaked.Apply(seconds, 1f, pose);
        }
        else
        {
            pose.Add("head", new PoseOffset(
                Vector3.Zero,
                new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
                Vector3.Zero));
        }
    }

    private static AnimationKeyframe KL(float t, Vector3 v) => new(t, v, AnimationInterpolation.Linear);
    private static AnimationKeyframe KC(float t, Vector3 v) => new(t, v, AnimationInterpolation.CatmullRom);

    private static AnimationKeyframe PosL(float t, float x, float y, float z) => KL(t, KeyframeAnimations.PosVec(x, y, z));
    private static AnimationKeyframe PosC(float t, float x, float y, float z) => KC(t, KeyframeAnimations.PosVec(x, y, z));
    private static AnimationKeyframe RotL(float t, float x, float y, float z) => KL(t, KeyframeAnimations.DegreeVec(x, y, z));
    private static AnimationKeyframe RotC(float t, float x, float y, float z) => KC(t, KeyframeAnimations.DegreeVec(x, y, z));
    private static AnimationKeyframe ScaleL(float t) => KL(t, KeyframeAnimations.ScaleVec(1f, 1f, 1f));

    private static AnimationChannel R(params AnimationKeyframe[] f) => new(AnimationTarget.Rotation, f);
    private static AnimationChannel P(params AnimationKeyframe[] f) => new(AnimationTarget.Position, f);
    private static AnimationChannel S(params AnimationKeyframe[] f) => new(AnimationTarget.Scale, f);

    private static readonly IReadOnlySet<string> HopPartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "head", "backlegs", "frontlegs", "right_front_leg", "left_front_leg",
        "left_ear", "right_ear", "right_hind_leg", "left_hind_leg", "tail"
    };

    private static readonly IReadOnlySet<string> IdlePartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "tail", "head", "left_ear", "right_ear", "right_front_leg", "left_front_leg", "frontlegs"
    };

    private static AnimationDefinition BuildHop() =>
        new(0.75f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.125f, 0f, 0f, 0f), RotC(0.2083f, 4f, 0f, 0f),
                  RotL(0.2917f, 32.5f, 0f, 0f), RotC(0.4167f, 33f, 0f, 0f), RotC(0.5833f, 18f, 0f, 0f),
                  RotL(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.125f, 0f, 0f, 0f), RotL(0.2083f, -4f, 0f, 0f),
                  RotL(0.2917f, -32.17f, 0f, 0f), RotL(0.375f, -34.58f, 0f, 0f), RotL(0.5833f, -20f, 0f, 0f),
                  RotL(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.125f, 0f, 0f, 0f), PosL(0.375f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["backlegs"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.125f, 0f, 0f, 0f), RotL(0.25f, 125f, 0f, 0f),
                  RotL(0.2917f, 125f, 0f, 0f), RotL(0.375f, 120f, 0f, 0f), RotL(0.4583f, 95f, 0f, 0f),
                  RotL(0.5417f, 42f, 0f, 0f), RotL(0.6667f, 0f, 0f, 0f), RotL(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.6667f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["frontlegs"] =
            [
                R(RotL(0f, -0.17f, 0f, 0f), RotL(0.125f, -0.17f, 0f, 0f), RotL(0.2083f, 25.25f, 0f, 0f),
                  RotC(0.2917f, -65f, 0f, 0f), RotC(0.4583f, -67.5f, 0f, 0f), RotL(0.625f, -1.25f, 0f, 0f),
                  RotL(0.749f, -1.25f, 0f, 0f), RotL(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.125f, 0f, 0f, 0f), PosL(0.3333f, 0f, 0.5f, 0.6f),
                  PosL(0.4167f, 0f, 0.9f, 0.4f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["right_front_leg"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.125f, 0f, 0f, 0f), RotC(0.2083f, 0f, 0f, 0f),
                  RotC(0.3333f, 0f, 0f, -17.5f), RotC(0.5f, 0f, 0f, -17.5f), RotC(0.5833f, 0f, 0f, -2f),
                  RotC(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.125f, 0f, 0f, 0f), PosL(0.2083f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["left_front_leg"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.125f, 0f, 0f, 0f), RotC(0.2083f, 0f, 0f, 0f),
                  RotC(0.3333f, 0f, 0f, 20f), RotC(0.5f, 0f, 0f, 20f), RotC(0.5833f, 0f, 0f, 2f),
                  RotC(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.125f, 0f, 0f, 0f), PosL(0.2083f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["left_ear"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.125f, 2.5f, 0f, 0f), RotC(0.375f, -48.5f, 0f, 0f),
                  RotC(0.5417f, -41.24f, 0f, 0f), RotL(0.75f, 0f, 0f, 0f)),
                P(PosC(0f, -0.025f, 0f, 0f), PosC(0.2083f, -0.025f, -0.2f, 0f), PosC(0.375f, -0.02f, -0.3f, 0f),
                  PosC(0.75f, -0.025f, 0f, 0f))
            ],
            ["right_ear"] =
            [
                R(RotC(0f, 0f, 0f, 0f), RotC(0.125f, 7.5f, 0f, 0f), RotC(0.375f, -31.5f, 0f, 0f),
                  RotC(0.5f, -35.33f, 0f, 0f), RotC(0.75f, 0f, 0f, 0f)),
                P(PosC(0f, 0.025f, 0f, 0f), PosC(0.2083f, 0.025f, -0.3f, 0f), PosC(0.375f, 0.02f, -0.23f, 0f),
                  PosC(0.75f, 0.025f, 0f, 0f))
            ],
            ["right_hind_leg"] =
            [
                R(RotL(0f, 0f, -2.5f, 0f), RotL(0.1667f, 0f, -2.5f, 0f), RotC(0.2083f, 47.5f, 0f, 0f),
                  RotC(0.4167f, 47.5f, 0f, 0f), RotL(0.4583f, 0f, 0f, 0f), RotL(0.75f, 0f, -2.5f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.6667f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["left_hind_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 0f, 0f, 0f), RotL(0.2083f, 47.5f, 0f, 0f),
                  RotC(0.4167f, 47.5f, 0f, 0f), RotL(0.4583f, 0f, 0f, 0f), RotL(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.6667f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
            ["tail"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.125f, -25f, 0f, 0f), RotL(0.3333f, 15f, 0f, 0f),
                  RotL(0.375f, 27.5f, 0f, 0f), RotL(0.75f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.75f, 0f, 0f, 0f))
            ],
        });

    private static AnimationDefinition BuildIdleHeadTilt() =>
        new(4.0f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>(StringComparer.Ordinal)
        {
            ["body"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.25f, -62f, 0f, 0f), RotL(0.3333f, -57f, 0f, 0f), RotL(0.7083f, -57f, 0f, 0f),
                  RotL(0.875f, -57f, 12f, 2f), RotL(0.9583f, -57f, 5f, 1f), RotL(1.5417f, -57f, 5f, 1f),
                  RotL(1.7083f, -56.5f, -16f, -3f), RotL(1.7917f, -57f, -7.5f, -1.5f), RotL(2.5417f, -57f, -7.5f, -1.5f),
                  RotL(2.7083f, -57f, 11f, 2f), RotL(2.7917f, -57f, 5f, 1f), RotL(2.9583f, -57f, 5f, 1f),
                  RotL(3.1667f, -57f, 5f, 1f), RotL(3.25f, -57.5f, 5f, 1f), RotL(3.4583f, -60f, 5f, 0.5f),
                  RotL(3.5833f, -60f, 5f, 0.5f), RotL(3.6667f, -39.5f, 3.5f, 0.5f), RotL(3.8333f, 10f, 0f, 0f),
                  RotL(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 0.7f, 0f), PosL(0.25f, 0f, 0f, 0f),
                  PosL(3.8333f, 0f, 0f, 0f), PosL(3.9167f, 0f, 0f, 0f)),
                S(ScaleL(0f), ScaleL(3.8333f), ScaleL(3.9167f))
            ],
            ["tail"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotC(0.0833f, 33f, 0f, 0f), RotC(0.2083f, 54.5f, 0f, 0f), RotC(0.2917f, 55f, 0f, 0f),
                  RotL(0.375f, 55f, 0f, 0f), RotL(0.7083f, 55f, 0f, 0f), RotC(0.8333f, 55f, -3.5f, -19.5f),
                  RotC(0.9167f, 55f, -6f, 7.5f), RotL(1.0417f, 55f, 0f, 0f), RotL(1.5417f, 55f, 0f, 0f),
                  RotC(1.625f, 55f, 2.5f, 12.5f), RotC(1.7083f, 55f, 4.5f, 25.5f), RotC(1.7917f, 55f, 19.5f, -16f),
                  RotL(1.875f, 55f, 0f, 0f), RotL(2.5417f, 54.5f, 0f, 0f), RotL(3.5833f, 54.5f, 0f, 0f),
                  RotC(3.8333f, -46f, 0f, 0f), RotC(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.7083f, 0f, 0f, 0f), PosC(0.8333f, 0f, 0f, 0f), PosC(0.9167f, 0f, 0f, 0f),
                  PosL(1.0417f, 0f, 0f, 0f), PosL(1.5417f, 0f, 0f, 0f), PosL(3.8333f, 0f, 0f, 0f), PosL(3.9167f, 0f, 0f, 0f))
            ],
            ["head"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.25f, 54.5f, 0f, 0f), RotL(0.3333f, 40f, 0f, 0f), RotL(0.75f, 40f, 0f, 0f),
                  RotC(0.9167f, 40f, 20.5f, -11f), RotC(1.0f, 39.5f, 11.5f, -6f), RotL(1.2917f, 39.3f, 11.5f, -6f),
                  RotL(1.75f, 39.5f, 11.5f, -6f), RotL(1.9167f, 36f, -28.5f, 16.5f), RotL(2.0f, 38f, -18.5f, 10f),
                  RotL(2.625f, 38f, -18.5f, 10f), RotL(2.7083f, 40f, 0f, 0f), RotL(2.7917f, 40f, 0f, 0f),
                  RotL(2.9583f, 40f, 0f, 0f), RotL(3.1667f, 40f, 0f, 0f), RotL(3.25f, 42.5f, -0.25f, 0f),
                  RotL(3.5833f, 45.5f, -0.5f, 0f), RotC(3.8333f, -20f, 0f, 0f), RotL(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.25f, 0f, 0f, -2.5f), PosL(0.3333f, 0f, 0f, -2f), PosL(0.5833f, 0f, 0f, -2f),
                  PosL(0.75f, 0f, 0f, -2f), PosL(1.75f, 0f, 0f, -2f), PosL(2.7083f, 0f, 0f, -2f), PosL(3.5833f, 0f, 0f, -2f),
                  PosC(3.8333f, 0f, -1f, 0f), PosL(3.9167f, 0f, 0f, 0f))
            ],
            ["left_ear"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.2083f, 35f, 0f, 0f), RotL(0.2917f, 19f, 0f, 0f), RotL(0.3333f, 0f, 0f, 0f),
                  RotL(0.75f, 0f, 0f, 0f), RotL(0.9167f, -15f, 0f, 7.5f), RotL(1.0f, -7.5f, 0f, -5f), RotL(1.0833f, 0f, 0f, 0f),
                  RotL(1.6667f, 0f, 0f, 0f), RotL(1.75f, -3f, 1.5f, -17f), RotL(1.8333f, 0f, 0f, 0f),
                  RotL(1.9167f, -48.75f, 0f, 5f), RotL(2.0f, -65f, 2.5f, 0.5f), RotL(2.0833f, -45f, 0f, 0f),
                  RotL(2.1667f, -40f, 0f, 0f), RotL(2.5417f, -40f, 0f, 0f), RotL(2.625f, 11f, 0f, 0f),
                  RotL(2.7083f, 23.25f, 0f, 3.75f), RotL(2.75f, 25.5f, 2f, 6f), RotL(2.8333f, 10f, -0.5f, 0.75f),
                  RotL(3.625f, 10f, -0.5f, 0.75f), RotL(3.7083f, -25f, 0.25f, -1f), RotL(3.7917f, -19f, 0f, -1f),
                  RotL(3.8333f, 2.5f, 0.2f, -0.5f), RotL(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, -0.025f, 0f, 0f), PosL(0.2083f, -0.125f, -0.6553f, 0.1589f), PosL(0.3333f, -0.025f, 0f, 0f),
                  PosL(0.75f, -0.025f, 0f, 0f), PosL(0.9167f, -0.0502f, -0.1915f, -0.0518f),
                  PosL(1.0f, -0.2423f, -0.1025f, 0.0261f), PosL(1.0833f, -0.025f, 0f, 0f), PosL(1.6667f, -0.025f, 0f, 0f),
                  PosL(1.75f, -0.3f, -0.3f, 0f), PosL(1.7917f, -0.095f, -0.15f, 0f), PosL(1.8333f, -0.025f, 0f, 0f),
                  PosL(2.0f, -0.025f, -0.575f, 0f), PosL(2.0833f, -0.025f, -0.575f, 0f), PosL(2.1667f, -0.025f, -0.575f, 0f),
                  PosL(2.5417f, -0.065f, -0.575f, 0f), PosL(2.625f, -0.025f, -0.25f, 0f), PosL(2.75f, -0.025f, -0.25f, 0.25f),
                  PosL(2.7917f, -0.025f, -0.25f, 0f), PosL(2.8333f, -0.025f, -0.25f, 0f), PosL(3.625f, -0.025f, -0.25f, 0f),
                  PosL(3.9167f, -0.025f, 0f, 0f))
            ],
            ["right_ear"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 35f, 0f, 0f), RotL(0.3333f, 0f, 0f, 0f), RotL(0.75f, 0f, 0f, 0f),
                  RotL(0.9167f, -22.3423f, -2.5587f, 5.4476f), RotL(1.0f, -11.17f, -1.28f, -21.03f),
                  RotL(1.0833f, -4.1101f, 3.0703f, -18.1443f), RotL(1.125f, -0.0844f, 3.027f, -9.5338f),
                  RotL(1.5417f, -0.0844f, 3.027f, -9.5338f), RotL(1.6667f, 0f, 0f, 0f), RotL(1.75f, -1.5f, 1f, -14.5f),
                  RotL(1.8333f, 0f, 0f, 0f), RotL(1.9167f, -45f, 0f, 5f), RotL(2.0f, -60f, 1f, 5f), RotL(2.0833f, -50f, 0f, 0f),
                  RotL(2.5417f, -50f, 0f, 0f), RotL(2.625f, 16f, 0f, 0f), RotL(2.7083f, 25.5f, 0f, 0f),
                  RotL(2.7917f, 5.25f, 0.5f, -2f), RotL(2.8333f, 8.5f, 1f, -4f), RotL(3.625f, 8.5f, 1f, -4f),
                  RotL(3.7083f, -37.5f, 0.25f, -1.5f), RotL(3.7917f, -23.5f, 0f, -1f), RotL(3.8333f, 10f, 0.25f, -0.5f),
                  RotL(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, 0.025f, 0f, 0f), PosL(0.1667f, 0.07f, -0.59f, 0.14f), PosL(0.2083f, 0.075f, -0.4587f, 0.1769f),
                  PosL(0.3333f, 0.025f, 0f, 0f), PosL(0.75f, 0.025f, 0f, 0f), PosL(0.875f, 0.13f, -0.15f, 0f),
                  PosL(0.9167f, 0.125f, -0.2f, 0f), PosL(1.0f, 0.0182f, -0.4258f, 0.0581f), PosL(1.0833f, 0.02f, -0.36f, 0.02f),
                  PosL(1.125f, 0.025f, -0.175f, 0f), PosL(1.5417f, 0.025f, -0.175f, 0f), PosL(1.6667f, 0.025f, 0f, 0f),
                  PosL(1.75f, 0.025f, 0f, 0f), PosL(1.8333f, 0.025f, 0f, 0f), PosL(2.0f, 0.1f, -0.55f, 0f),
                  PosL(2.0833f, 0.025f, -0.5f, 0f), PosL(2.5417f, 0.025f, -0.5f, 0f), PosL(2.5833f, 0.025f, -0.25f, 0f),
                  PosL(2.7083f, 0.025f, -0.25f, 0f), PosL(2.8333f, 0.025f, -0.25f, 0f), PosL(3.625f, 0.025f, -0.25f, 0f),
                  PosL(3.9167f, 0.025f, 0f, 0f))
            ],
            ["right_front_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.1667f, 0f, 0f, 0f), RotL(0.3333f, 60f, 0f, 0f), RotL(0.4167f, 40f, 0f, 0f),
                  RotL(0.7083f, 40f, 0f, 0f), RotL(0.8333f, 38.5f, 15f, -9.5f), RotL(0.9167f, 39.5f, 7f, -4.5f),
                  RotL(1.0f, 40f, 0f, 0f), RotL(1.6667f, 40f, 0f, 0f), RotL(1.75f, 36.5f, 28.5f, -16f),
                  RotL(1.8333f, 38.5f, 20f, -10.5f), RotL(3.5f, 38.5f, 20f, -10.5f), RotL(3.5833f, 40f, 0f, 0f),
                  RotL(3.75f, 12.5f, 0f, 0f), RotL(3.8333f, 0f, 0f, 0f), RotL(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.1667f, 0f, 0f, 0f), PosL(0.3333f, 0f, 0.6f, 0f), PosL(0.4167f, 0f, 0.6f, 0f),
                  PosL(0.7083f, 0f, 0.6f, 0f), PosL(1.0f, 0f, 0.6f, 0f), PosL(1.6667f, 0f, 0.6f, 0f),
                  PosL(3.5833f, 0f, 0.6f, 0f), PosL(3.75f, 0f, 0.5f, 0.5f), PosL(3.8333f, 0f, 1f, -0.5f), PosL(3.9167f, 0f, 0f, 0f))
            ],
            ["left_front_leg"] =
            [
                R(RotL(0f, 0f, 0f, 0f), RotL(0.2083f, 0f, 0f, 0f), RotL(0.375f, 70f, 0f, 0f), RotL(0.4583f, 40f, 0f, 0f),
                  RotL(0.7083f, 40f, 0f, 0f), RotL(0.8333f, 40f, 9f, -1f), RotL(0.9167f, 39.5f, 5f, -4.5f),
                  RotL(1.0f, 40f, 0f, 0f), RotL(1.6667f, 40f, 0f, 0f), RotL(1.75f, 37.5f, -24.5f, 13f),
                  RotL(1.8333f, 39.5f, -15.5f, 8f), RotL(3.5f, 39.5f, -15.5f, 8f), RotL(3.5833f, 40f, 0f, 0f),
                  RotL(3.75f, 10.5f, 0f, 0f), RotL(3.8333f, 0f, 0f, 0f), RotL(3.9167f, 0f, 0f, 0f)),
                P(PosL(0f, 0f, 0f, 0f), PosL(0.2083f, 0f, 0f, 0f), PosL(0.375f, 0f, 0.8f, 0f), PosL(0.4583f, 0f, 0.6f, 0f),
                  PosL(0.7083f, 0f, 0.6f, 0f), PosL(1.0f, 0f, 0.6f, 0f), PosL(1.6667f, 0f, 0.6f, 0f),
                  PosL(3.5833f, 0f, 0.6f, 0f), PosL(3.75f, 0f, 0.5f, 0f), PosL(3.8333f, 0f, 1f, -0.5f), PosL(3.9167f, 0f, 0f, 0f))
            ],
            ["frontlegs"] =
            [
                P(PosL(0f, 0f, 0f, 0f), PosC(0.2917f, 0f, 0.1981f, 0.0278f), PosC(1.5f, 0f, 0.2f, 0.03f),
                  PosL(1.8333f, -0.0261f, 0.005f, -0.0061f), PosL(3.625f, -0.03f, 0.01f, -0.01f), PosL(3.9167f, 0f, 0f, 0f))
            ],
        });

    private static readonly AnimationDefinition Hop = BuildHop();
    private static readonly KeyframeAnimation HopBaked = Hop.Bake(HopPartNames);
    private static readonly AnimationDefinition IdleHeadTilt = BuildIdleHeadTilt();
    private static readonly KeyframeAnimation IdleHeadTiltBaked = IdleHeadTilt.Bake(IdlePartNames);

    private sealed class State : IEntityAnimationState
    {
    }
}
