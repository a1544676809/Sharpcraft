// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>RavagerModel.setupAnim</c>: fully procedural, no keyframe data. The neck lifts and the mouth opens over
/// the 10-tick attack, the neck sways and tilts while stunned, the mouth roars open during the 20-tick roar, and the four
/// legs swing from the walk phase. Head pitch/yaw come from the generic frame.
/// </summary>
/// <remarks>
/// The reference derives three render-state floats from the entity counters every frame:
/// <c>attackTicksRemaining = attackTick - partialTick</c>, <c>stunnedTicksRemaining = stunnedTick - partialTick</c>, and
/// <c>roarAnimation = (20 - roarTick + partialTick) / 20</c>. Those counters are not on the generic frame, so a private
/// per-entity state replicates them from the client-side entity events (4 = attack, 39 = stunned, 69 = roar effects) and
/// the per-tick decrement (including the stun-finished → <c>roarTick = 20</c> transition). The heavy hit on the mouth
/// only while roaring uses that derived <c>roarAnimation</c>. Attack/stun/roar counters are advanced by the tracker via
/// <see cref="IEntityTickAnimationState"/>; <see cref="Apply"/> only reads them.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.ravager.RavagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ravager/RavagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class RavagerAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk", "attack", "stun", "roar"];

    /// <summary>Neck bind-pose z pivot; the attack branch writes an absolute z, so offsets subtract this.</summary>
    private const float NeckBindZ = 5.5f;

    private const int AttackDurationTicks = 10;
    private const int StunDurationTicks = 40;
    private const int RoarDurationTicks = 20;

    public RavagerAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var counters = state as State;
        var partialTick = frame.PartialTick;

        var attackTicksRemaining = counters is { AttackTick: > 0 } ? counters.AttackTick - partialTick : 0f;
        var stunnedTicksRemaining = counters is { StunnedTick: > 0 } ? counters.StunnedTick - partialTick : 0f;
        var roarAnimation = counters is { RoarTick: > 0 } ? (RoarDurationTicks - counters.RoarTick + partialTick) / 20f : 0f;

        ApplyPose(frame, attackTicksRemaining, stunnedTicksRemaining, roarAnimation, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "idle":
                ApplyPose(frame with { WalkAnimationSpeed = 0f }, 0f, 0f, 0f, pose);
                break;
            case "walk":
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, 0f, 0f, 0f, pose);
                break;
            case "attack":
                ApplyPose(frame, MathF.Max(0f, AttackDurationTicks - (seconds * 20f)), 0f, 0f, pose);
                break;
            case "stun":
                ApplyPose(frame, 0f, MathF.Max(0f, StunDurationTicks - (seconds * 20f)), 0f, pose);
                break;
            case "roar":
                ApplyPose(frame, 0f, 0f, Math.Clamp(seconds, 0f, 0.95f), pose);
                break;
        }
    }

    private static void ApplyPose(
        in EntityAnimationFrame frame,
        float attackTicksRemaining,
        float stunnedTicksRemaining,
        float roarAnimation,
        EntityPose pose)
    {
        // The reference calls Model.setupAnim first, which resets every part to its bind pose. That leaves neck.xRot at 0
        // when it is read below, so the neck lift's sine-derived y offset is always zero and only z/x move.
        float neckX = 0f;
        float neckZ = 0f;
        float neckRotX = 0f;
        float mouthRotX;

        if (attackTicksRemaining > 0f)
        {
            var headAnim = TriangleWave(attackTicksRemaining, AttackDurationTicks);
            var scaled = (1f + headAnim) * 0.5f;
            var headPos = scaled * scaled * scaled * 12f;
            neckZ = (-6.5f + headPos) - NeckBindZ;
            mouthRotX = attackTicksRemaining > 5f
                ? MathF.Sin((-4f + attackTicksRemaining) / 4f) * MathF.PI * 0.4f
                : (MathF.PI / 20f) * MathF.Sin(MathF.PI * attackTicksRemaining / AttackDurationTicks);
        }
        else
        {
            var isStunned = stunnedTicksRemaining > 0f;
            neckRotX = isStunned ? 0.21991149f : 0f;
            mouthRotX = MathF.PI * (isStunned ? 0.05f : 0.01f);
            if (isStunned)
            {
                var speed = stunnedTicksRemaining / StunDurationTicks;
                neckX = MathF.Sin(speed * 10f) * 3f;
            }
            else if (roarAnimation > 0f)
            {
                var mouthAnim = MathF.Sin(roarAnimation * MathF.PI * 0.25f);
                mouthRotX = (MathF.PI / 2f) * mouthAnim;
            }
        }

        pose.Add("neck", new PoseOffset(new Vector3(neckX, 0f, neckZ), new Vector3(neckRotX, 0f, 0f), Vector3.Zero));
        pose.Add("mouth", new PoseOffset(Vector3.Zero, new Vector3(mouthRotX, 0f, 0f), Vector3.Zero));

        var headPitch = frame.Pitch * (MathF.PI / 180f);
        var headYaw = frame.Yaw * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, headYaw, 0f), Vector3.Zero));

        var phase = frame.WalkAnimationPos * 0.6662f;
        var legRot = 0.4f * frame.WalkAnimationSpeed;
        var rightHind = MathF.Cos(phase) * legRot;
        var leftHind = MathF.Cos(phase + MathF.PI) * legRot;
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHind, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHind, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHind, 0f, 0f), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHind, 0f, 0f), Vector3.Zero));
    }

    /// <summary>Reference <c>Mth.triangleWave</c>: a phase fold mapped onto [-1, 1].</summary>
    private static float TriangleWave(float index, float period) =>
        (MathF.Abs((index % period) - (period * 0.5f)) - (period * 0.25f)) / (period * 0.25f);

    private sealed class State : IEntityAnimationState, IEntityEventAnimationState, IEntityTickAnimationState
    {
        /// <summary>Ticks left in the 10-tick attack (event 4).</summary>
        public int AttackTick { get; private set; }

        /// <summary>Ticks left in the 40-tick stun (event 39).</summary>
        public int StunnedTick { get; private set; }

        /// <summary>Ticks left in the 20-tick roar, entered when a stun finishes.</summary>
        public int RoarTick { get; private set; }

        public void OnEntityEvent(int eventId, int gameTime)
        {
            switch (eventId)
            {
                case 4: // attack
                    AttackTick = AttackDurationTicks;
                    break;
                case 39: // stunned
                    StunnedTick = StunDurationTicks;
                    break;
                case 69: // roar particle effects are client-side only; the roar clock is driven by the stun transition.
                    break;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            // Order mirrors Ravager.aiStep: roar, then attack, then stun (whose final tick feeds the roar clock).
            if (RoarTick > 0)
            {
                RoarTick--;
            }

            if (AttackTick > 0)
            {
                AttackTick--;
            }

            if (StunnedTick > 0)
            {
                StunnedTick--;
                if (StunnedTick == 0)
                {
                    RoarTick = RoarDurationTicks;
                }
            }
        }
    }
}
