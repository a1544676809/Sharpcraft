// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>CopperGolemModel.setupAnim</c> together with <c>CopperGolem.setupAnimationStates</c>.
/// </summary>
/// <remarks>
/// Every frame the head looks along the render-state pitch/yaw and the walk loop plays from the walk position/speed.
/// The idle clip plus the four chest-interaction clips are one-shot <see cref="AnimationState"/> clocks reconciled once
/// per tick from the synced <c>CopperGolemState</c> (entity data serializer id 37). All contributions are additive pose
/// offsets against a zero bind pose, exactly like the reference <c>offset*</c> accumulators after <c>resetPose</c>.
/// <para>
/// When either hand holds an item the reference plays the held-item walk clip (<c>walk_item</c>) and clamps the arms
/// via <c>poseHeldItemArmsIfStill</c>; the generic frame now carries <see cref="EntityAnimationFrame.HasMainHandItem"/>
/// and <see cref="EntityAnimationFrame.HasOffHandItem"/> derived from the equipped slots, so both branches run on the
/// hot path. The head-spin sound triggered inside the idle branch has no audio equivalent in the animation layer, so
/// only the clock reset is reproduced.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.golem.CopperGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class CopperGolemAnimationProvider(int typeId) : IEntityAnimationProvider
{
    /// <summary>Reference <c>EntityDataSerializers.COPPER_GOLEM_STATE</c> id used to route the synced state ordinal.</summary>
    public const int CopperGolemStateSerializerId = 37;

    private static readonly string[] NominalClips =
    [
        "walk", "walk_item", "idle", "item_drop", "item_nodrop", "noitem_get", "noitem_noget"
    ];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        // Generic head look: pitch drives xRot, yaw drives yRot (both render-state angles are in degrees).
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        var headYaw = frame.Yaw * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, headYaw, 0f), Vector3.Zero));

        // Walk loop: reference applyWalk(walkPos, walkSpeed, 2.0F, 2.5F).
        //   elapsed seconds = walkPos * 50 * 2 / 1000 = walkPos * 0.1,
        //   target scale    = min(walkSpeed * 2.5, 1).
        // When either hand holds an item the reference uses the held-item walk clip and clamps the arms
        // (poseHeldItemArmsIfStill); otherwise it uses the plain walk clip.
        var walkSeconds = frame.WalkAnimationPos * 0.1f;
        var walkScale = MathF.Min(frame.WalkAnimationSpeed * 2.5f, 1f);
        if (frame.HasMainHandItem || frame.HasOffHandItem)
        {
            CopperGolemAnimations.WalkItemBaked.Apply(walkSeconds, walkScale, pose);
            ClampHeldItemArms(pose);
        }
        else
        {
            CopperGolemAnimations.WalkBaked.Apply(walkSeconds, walkScale, pose);
        }

        if (state is State clips)
        {
            AnimationClipPlayer.Apply(clips.Idle, CopperGolemAnimations.IdleBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.GetItem, CopperGolemAnimations.NoItemGetBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.GetNoItem, CopperGolemAnimations.NoItemNoGetBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.DropItem, CopperGolemAnimations.ItemDropBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.DropNoItem, CopperGolemAnimations.ItemNoDropBaked, frame.AgeInTicks, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var animation = clip switch
        {
            "walk" => CopperGolemAnimations.WalkBaked,
            "walk_item" => CopperGolemAnimations.WalkItemBaked,
            "idle" => CopperGolemAnimations.IdleBaked,
            "item_drop" => CopperGolemAnimations.ItemDropBaked,
            "item_nodrop" => CopperGolemAnimations.ItemNoDropBaked,
            "noitem_get" => CopperGolemAnimations.NoItemGetBaked,
            "noitem_noget" => CopperGolemAnimations.NoItemNoGetBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    /// <summary>
    /// Reference <c>poseHeldItemArmsIfStill</c>: clamp the absolute arm rotations so the held item clears the body.
    /// Our pose is additive over a zero arm bind rotation, so the clamp is expressed as a delta on the accumulated
    /// offset.
    /// </summary>
    private static void ClampHeldItemArms(EntityPose pose)
    {
        ClampRotation(pose, "right_arm", 0, upper: true, -0.87266463f);
        ClampRotation(pose, "right_arm", 1, upper: true, -0.1134464f);
        ClampRotation(pose, "right_arm", 2, upper: true, -0.064577185f);
        ClampRotation(pose, "left_arm", 0, upper: true, -0.87266463f);
        ClampRotation(pose, "left_arm", 1, upper: false, 0.1134464f);
        ClampRotation(pose, "left_arm", 2, upper: false, 0.064577185f);
    }

    private static void ClampRotation(EntityPose pose, string part, int axis, bool upper, float limit)
    {
        var rotation = pose.Get(part).Rotation;
        var value = axis == 0 ? rotation.X : axis == 1 ? rotation.Y : rotation.Z;
        var clamped = upper ? MathF.Min(value, limit) : MathF.Max(value, limit);
        var delta = clamped - value;
        if (delta == 0f)
        {
            return;
        }

        var offset = axis == 0
            ? new Vector3(delta, 0f, 0f)
            : axis == 1
                ? new Vector3(0f, delta, 0f)
                : new Vector3(0f, 0f, delta);
        pose.Add(part, new PoseOffset(Vector3.Zero, offset, Vector3.Zero));
    }

    /// <summary>Reference <c>CopperGolemState</c> ordinals (the values carried by serializer 37).</summary>
    private enum CopperGolemState
    {
        Idle = 0,
        GettingItem = 1,
        GettingNoItem = 2,
        DroppingItem = 3,
        DroppingNoItem = 4
    }

    /// <summary>
    /// Per-entity Copper Golem animation state. Holds the five one-shot clip clocks and the idle restart schedule; the
    /// synced state ordinal arrives through <see cref="OnSyncedData"/> and the clocks advance in <see cref="OnTick"/>.
    /// </summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState
    {
        public readonly AnimationState Idle = new();
        public readonly AnimationState GetItem = new();
        public readonly AnimationState GetNoItem = new();
        public readonly AnimationState DropItem = new();
        public readonly AnimationState DropNoItem = new();

        private readonly Random _random = new();
        private CopperGolemState _state = CopperGolemState.Idle;
        private int _idleAnimationStartTick;

        public void OnSyncedData(int index, int serializerId, int value)
        {
            if (serializerId != CopperGolemStateSerializerId)
            {
                return;
            }

            // Unknown ordinals fall back to IDLE, matching the reference id map's zero out-of-bounds strategy.
            _state = value switch
            {
                0 => CopperGolemState.Idle,
                1 => CopperGolemState.GettingItem,
                2 => CopperGolemState.GettingNoItem,
                3 => CopperGolemState.DroppingItem,
                4 => CopperGolemState.DroppingNoItem,
                _ => CopperGolemState.Idle
            };
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            var tickCount = context.GameTime;
            switch (_state)
            {
                case CopperGolemState.Idle:
                    GetNoItem.Stop();
                    GetItem.Stop();
                    DropItem.Stop();
                    DropNoItem.Stop();

                    if (_idleAnimationStartTick == tickCount)
                    {
                        Idle.Start(tickCount);
                    }
                    else if (_idleAnimationStartTick == 0)
                    {
                        _idleAnimationStartTick = tickCount + _random.Next(200, 240);
                    }

                    if (tickCount == _idleAnimationStartTick + 10)
                    {
                        // The reference plays the head-spin sound here; only the schedule reset is ported.
                        _idleAnimationStartTick = 0;
                    }

                    break;

                case CopperGolemState.GettingItem:
                    Idle.Stop();
                    _idleAnimationStartTick = 0;
                    GetNoItem.Stop();
                    DropItem.Stop();
                    DropNoItem.Stop();
                    GetItem.StartIfStopped(tickCount);
                    break;

                case CopperGolemState.GettingNoItem:
                    Idle.Stop();
                    _idleAnimationStartTick = 0;
                    GetItem.Stop();
                    DropNoItem.Stop();
                    DropItem.Stop();
                    GetNoItem.StartIfStopped(tickCount);
                    break;

                case CopperGolemState.DroppingItem:
                    Idle.Stop();
                    _idleAnimationStartTick = 0;
                    GetItem.Stop();
                    GetNoItem.Stop();
                    DropNoItem.Stop();
                    DropItem.StartIfStopped(tickCount);
                    break;

                case CopperGolemState.DroppingNoItem:
                    Idle.Stop();
                    _idleAnimationStartTick = 0;
                    GetItem.Stop();
                    GetNoItem.Stop();
                    DropItem.Stop();
                    DropNoItem.StartIfStopped(tickCount);
                    break;
            }
        }
    }
}
