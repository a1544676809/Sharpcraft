// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>IronGolemModel.setupAnim</c>: a fully procedural pose driven by the generic walk inputs, the head
/// yaw/pitch, and two event-driven counters (<c>attackTicksRemaining</c> and <c>offerFlowerTick</c>).
/// <list type="bullet">
/// <item>While attacking, both arms swing from <c>-2 + 1.5·triangleWave(attackTick, 10)</c>.</item>
/// <item>While offering a flower, the right arm sits at <c>-0.8 + 0.025·triangleWave(offerTick, 70)</c> and the left arm is neutral.</item>
/// <item>Otherwise both arms follow the walk cycle: right <c>(-0.2 + 1.5·tri(pos,13))·speed</c>, left <c>(-0.2 - 1.5·tri(pos,13))·speed</c>.</item>
/// <item>The head looks along pitch/yaw; both legs follow <c>±1.5·tri(pos,13)·speed</c> in opposite phase.</item>
/// </list>
/// </summary>
/// <remarks>
/// All animated parts are root part cubes whose bind rotation is zero, so the additive pose offsets equal the reference
/// absolute assignments. The attack counter mirrors the reference: <c>attackTicksRemaining</c> is the integer
/// <c>attackAnimationTick</c> minus the frame's partial tick while positive (the renderer extracts
/// <c>attackAnimationTick - partialTicks</c>), and <c>offerFlowerTick</c> is used as-is. Counters are armed by entity
/// events (4 → attack 10, 11 → offer 400, 34 → offer 0) and decrement once per tick, exactly like
/// <c>IronGolem.handleEntityEvent</c> / <c>IronGolem.aiStep</c>.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.golem.IronGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/IronGolemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class IronGolemAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    private static readonly string[] NominalClips = ["idle", "walk", "attack", "offer_flower"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var clips = state as State;
        var attackTick = clips is { AttackAnimationTick: > 0 } ? clips.AttackAnimationTick - frame.PartialTick : 0f;
        var offerFlowerTick = clips?.OfferFlowerTick ?? 0;
        ApplyPose(frame, attackTick, offerFlowerTick, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "idle":
                ApplyPose(frame with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, 0f, 0, pose);
                break;
            case "walk":
                // Ten ticks per second: seconds*10 is a stable walk phase across the preview.
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, 0f, 0, pose);
                break;
            case "attack":
                // Ten ticks span 0.5 s; the counter runs 10 → 0.
                ApplyPose(frame, Math.Clamp(10f - (seconds * 20f), 0f, 10f), 0, pose);
                break;
            case "offer_flower":
                // The 400-tick offer counts down in the preview.
                ApplyPose(frame, 0f, (int)Math.Clamp(400f - (seconds * 20f), 0f, 400f), pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, float attackTick, int offerFlowerTick, EntityPose pose)
    {
        var pos = frame.WalkAnimationPos;
        var speed = frame.WalkAnimationSpeed;

        if (attackTick > 0f)
        {
            var arm = -2f + (1.5f * TriangleWave(attackTick, 10f));
            pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(arm, 0f, 0f), Vector3.Zero));
            pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(arm, 0f, 0f), Vector3.Zero));
        }
        else if (offerFlowerTick > 0)
        {
            var rightArm = -0.8f + (0.025f * TriangleWave(offerFlowerTick, 70f));
            pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(rightArm, 0f, 0f), Vector3.Zero));
            pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, 0f), Vector3.Zero));
        }
        else
        {
            var wave = 1.5f * TriangleWave(pos, 13f);
            pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3((-0.2f + wave) * speed, 0f, 0f), Vector3.Zero));
            pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3((-0.2f - wave) * speed, 0f, 0f), Vector3.Zero));
        }

        // Head look: reference head.yRot = yaw, head.xRot = pitch.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        // Legs: always the walk-cycle gait, in opposite phase; yRot is explicitly zeroed in the reference and stays zero here.
        var legWave = 1.5f * TriangleWave(pos, 13f) * speed;
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(-legWave, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(legWave, 0f, 0f), Vector3.Zero));
    }

    /// <summary>Reference <c>Mth.triangleWave(index, period)</c>.</summary>
    private static float TriangleWave(float index, float period) =>
        (MathF.Abs((index % period) - (period * 0.5f)) - (period * 0.25f)) / (period * 0.25f);

    private sealed class State : IEntityAnimationState, IEntityEventAnimationState, IEntityTickAnimationState
    {
        /// <summary>Reference <c>attackAnimationTick</c>: armed to 10 by event 4, decremented every tick.</summary>
        public int AttackAnimationTick;

        /// <summary>Reference <c>offerFlowerTick</c>: armed to 400 by event 11, cleared by event 34, decremented every tick.</summary>
        public int OfferFlowerTick;

        public void OnEntityEvent(int eventId, int gameTime)
        {
            switch (eventId)
            {
                case 4: // START_ATTACKING
                    AttackAnimationTick = 10;
                    break;
                case 11: // START_OFFERING_FLOWER
                    OfferFlowerTick = 400;
                    break;
                case 34: // STOP_OFFERING_FLOWER
                    OfferFlowerTick = 0;
                    break;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            if (AttackAnimationTick > 0)
            {
                AttackAnimationTick--;
            }

            if (OfferFlowerTick > 0)
            {
                OfferFlowerTick--;
            }
        }
    }
}
