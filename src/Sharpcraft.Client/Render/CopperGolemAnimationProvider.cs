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
/// The reference picks the held-item walk clip (<c>walk_item</c>) and clamps the arms via
/// <c>poseHeldItemArmsIfStill</c> when either hand holds an item. <see cref="EntityAnimationFrame"/> does not carry the
/// hand-item flags yet, so <see cref="Apply"/> always plays the plain walk clip; <c>walk_item</c> is reachable through
/// <see cref="ApplyClip"/> only. The head-spin sound triggered inside the idle branch has no audio equivalent in the
/// animation layer, so only the clock reset is reproduced.
/// </para>
/// </remarks>
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
        // The walk clip also poses the body, so this is the "body" part of the generic pose.
        CopperGolemAnimations.WalkBaked.Apply(
            frame.WalkAnimationPos * 0.1f,
            MathF.Min(frame.WalkAnimationSpeed * 2.5f, 1f),
            pose);

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
