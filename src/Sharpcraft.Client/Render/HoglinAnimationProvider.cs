// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>HoglinModel.setupAnim</c>: a fully procedural pose driven by the generic walk inputs, the head yaw,
/// and a per-entity attack countdown. Ears sway with the walk, the head looks along the render-state yaw, the headbutt
/// eases the head between its two reference angles, and the four legs follow a cosine gait.
/// </summary>
/// <remarks>
/// The reference writes absolute model rotations while this pose system is additive over the bind pose, so each
/// assigned value is reduced to its delta from the reference rest value (the model's bind pose):
/// <list type="bullet">
/// <item>ears: rest zRot = ±2π/9, so the delta is ∓speed·sin(pos);</item>
/// <item>head yaw: rest yRot = 0, so the delta is yaw in radians;</item>
/// <item>headbutt: rest xRot = 0.87266463, so the delta is lerp(f, 0.87266463, −π/9) − 0.87266463;</item>
/// <item>legs: rest xRot = 0, so the delta is the reference cosine value.</item>
/// </list>
/// The attack countdown is ten ticks: entity event 4 (START_ATTACKING) arms it, and it decrements once per tick, the
/// same lifecycle as the reference's <c>attackAnimationRemainingTicks</c>. The render state's <c>isConverting</c> flag
/// is not read by <c>setupAnim</c> and is therefore not an animation input.
/// </remarks>
public sealed class HoglinAnimationProvider(int typeId) : IEntityAnimationProvider
{
    /// <summary>Reference <c>DEFAULT_HEAD_X_ROT</c>.</summary>
    private const float DefaultHeadXRot = 0.87266463f;

    /// <summary>Reference <c>ATTACK_HEAD_X_ROT_END</c> = −π/9.</summary>
    private const float AttackHeadXRotEnd = -MathF.PI / 9f;

    private static readonly string[] NominalClips = ["idle", "walk", "attack"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var attackTicks = (state as State)?.AttackAnimationRemainingTicks ?? 0;
        ApplyPose(frame, attackTicks, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "walk":
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, 0, pose);
                break;
            case "attack":
                // Ten ticks span 0.5 s; the clip previews the headbutt as the counter runs 10 → 0.
                var ticks = Math.Clamp(10f - (seconds * 20f), 0f, 10f);
                ApplyPose(frame, ticks, pose);
                break;
            case "idle":
                ApplyPose(frame with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, 0, pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, float attackTicks, EntityPose pose)
    {
        var pos = frame.WalkAnimationPos;
        var speed = frame.WalkAnimationSpeed;

        // Ears: right rest zRot is −2π/9 and left rest zRot is +2π/9, so only the sway survives the delta.
        var sway = speed * MathF.Sin(pos);
        pose.Add("right_ear", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, -sway), Vector3.Zero));
        pose.Add("left_ear", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, sway), Vector3.Zero));

        // Head: yaw plus the headbutt delta; headbutt factor peaks in the middle of the ten-tick counter.
        var yaw = frame.Yaw * (MathF.PI / 180f);
        var headbuttFactor = 1f - MathF.Abs(10f - (2f * attackTicks)) / 10f;
        var headbutt = (AttackHeadXRotEnd - DefaultHeadXRot) * headbuttFactor;
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headbutt, yaw, 0f), Vector3.Zero));

        // Legs: reference cosine gait, hind legs copying the opposite front leg.
        var amplitude = 1.2f * speed;
        var rightFront = MathF.Cos(pos) * amplitude;
        var leftFront = MathF.Cos(pos + MathF.PI) * amplitude;
        var rightHind = leftFront;
        var leftHind = rightFront;

        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(rightFront, 0f, 0f), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(leftFront, 0f, 0f), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHind, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHind, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState, IEntityEventAnimationState, IEntityTickAnimationState
    {
        /// <summary>Reference <c>attackAnimationRemainingTicks</c>: armed to 10 by event 4, decremented every tick.</summary>
        public int AttackAnimationRemainingTicks;

        public void OnEntityEvent(int eventId, int gameTime)
        {
            if (eventId == 4) // START_ATTACKING
            {
                AttackAnimationRemainingTicks = 10;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            if (AttackAnimationRemainingTicks > 0)
            {
                AttackAnimationRemainingTicks--;
            }
        }
    }
}
