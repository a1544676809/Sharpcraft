// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Rabbit animation. The reference <c>RabbitModel.setupAnim</c> applies a HOP clip while the jump clock is active and an
/// IDLE_HEAD_TILT clip on a random idle timer; otherwise the head looks along the render-state pitch/yaw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hop (implemented):</b> the jump clock is now on the frame — <see cref="EntityAnimationFrame.IsJumping"/> and
/// <see cref="EntityAnimationFrame.JumpCompletion"/> (0..1), driven by entity event id 1 (15-tick jump). The reference
/// hop is a looping 0.75 s clip sampled at <c>jumpTicks * 50 ms</c>; since the jump lasts 15 ticks, elapsed seconds =
/// <c>JumpCompletion * 0.75</c>, so the HOP keyframes below are sampled at exactly that age. Keyframe data written as
/// behaviour data from <c>RabbitAnimation.HOP</c>.
/// </para>
/// <para>
/// <b>Idle-head-tilt (blocked, not faked):</b> <c>idleAnimationTimeout = random.nextInt(40) + 180</c> is seeded from the
/// entity's private RNG and <c>shouldPlayIdleAnimation</c> also checks leash/no-AI state — none on the frame, so the idle
/// clip cannot start at the exact tick. Because it never starts, the reference's "idle not started" head-look branch
/// applies and the head always looks along the pitch/yaw. Baby rabbits use a different mesh/animation and are out of scope.
/// </para>
/// </remarks>
public sealed class RabbitAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "hop"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference HOP length in seconds (0.75 s = 15 ticks).</summary>
    private const float HopLengthSeconds = 0.75f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        // Idle head tilt cannot start (blocked), so the reference head-look branch always applies.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        if (frame.IsJumping)
        {
            // jumpCompletion * 15 ticks * 50 ms = jumpCompletion * 0.75 s.
            HopBaked.Apply(frame.JumpCompletion * HopLengthSeconds, 1f, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "hop", StringComparison.Ordinal))
        {
            HopBaked.Apply(seconds, 1f, pose);
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

    private static AnimationChannel R(params AnimationKeyframe[] f) => new(AnimationTarget.Rotation, f);
    private static AnimationChannel P(params AnimationKeyframe[] f) => new(AnimationTarget.Position, f);

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

    private static readonly IReadOnlySet<string> HopPartNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "body", "head", "backlegs", "frontlegs", "right_front_leg", "left_front_leg",
        "left_ear", "right_ear", "right_hind_leg", "left_hind_leg", "tail"
    };

    private static readonly AnimationDefinition Hop = BuildHop();
    private static readonly KeyframeAnimation HopBaked = Hop.Bake(HopPartNames);

    private sealed class State : IEntityAnimationState
    {
    }
}
