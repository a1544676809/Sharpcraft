// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SnifferModel.setupAnim</c>: the head looks along the render-state pitch/yaw, the walk cycle (or the
/// sniff-search cycle while the sniffer is searching) plays from the walk position/speed, and five state clips
/// (feeling_happy, scenting, sniffing, digging, rising) play from their <see cref="AnimationState"/> clocks.
/// </summary>
/// <remarks>
/// <para>
/// Walk mapping: the reference calls <c>applyWalk(walkPos, walkSpeed, 9.0F, 100.0F)</c>, i.e. elapsed millis
/// <c>(long)(walkPos * 50 * 9)</c> and target scale <c>min(walkSpeed * 100, 1)</c>. While
/// <see cref="EntityAnimationFrame"/> has no <c>isSearching</c> flag, this provider derives it from the current
/// <c>Sniffer.State</c> ordinal fed by <see cref="IEntitySyncedDataAnimationState"/> (SEARCHING switches to the
/// sniff-search cycle).
/// </para>
/// <para>
/// State routing mirrors <c>Sniffer.onSyncedDataUpdated</c>: a change to the synced state stops all five clocks and
/// then starts the one clip the new state maps to (FEELING_HAPPY, SCENTING, SNIFFING, DIGGING, RISING; IDLING and
/// SEARCHING map to no clip). Because <see cref="IEntitySyncedDataAnimationState.OnSyncedData"/> carries no tick, the
/// reset/start transition is committed on the next tick or frame with the current game time, whichever comes first;
/// that keeps the clock aligned with <see cref="EntityAnimationFrame.AgeInTicks"/> on the hot path and also makes the
/// transition observable from a plain OnSyncedData → Apply sequence.
/// </para>
/// </remarks>
public sealed class SnifferAnimationProvider : IEntityAnimationProvider
{
    private const int SnifferStateSerializerId = 35;

    // Sniffer.State ordinals from the reference enum (IDLING 0 … RISING 6).
    private const int SnifferStateIdling = 0;
    private const int SnifferStateFeelingHappy = 1;
    private const int SnifferStateScenting = 2;
    private const int SnifferStateSniffing = 3;
    private const int SnifferStateSearching = 4;
    private const int SnifferStateDigging = 5;
    private const int SnifferStateRising = 6;

    private static readonly string[] NominalClips =
        ["feeling_happy", "scenting", "sniffing", "digging", "rising"];

    public SnifferAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var clips = state as State;

        // Commit a pending state transition at the current game time so the clock matches this frame's age.
        clips?.CommitTransition((int)frame.AgeInTicks);

        // Head look: the reference assigns head xRot/yRot from the render-state pitch/yaw in degrees.
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        var headYaw = frame.Yaw * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, headYaw, 0f), Vector3.Zero));

        // Walk / sniff-search: applyWalk(walkPos, walkSpeed, 9.0F, 100.0F).
        var walkMillis = (long)(frame.WalkAnimationPos * 50f * 9f);
        var walkSeconds = walkMillis / 1000f;
        var walkScale = MathF.Min(frame.WalkAnimationSpeed * 100f, 1f);
        if (clips is not null && clips.IsSearching)
        {
            SnifferAnimations.SniffSearchBaked.Apply(walkSeconds, walkScale, pose);
        }
        else
        {
            SnifferAnimations.WalkBaked.Apply(walkSeconds, walkScale, pose);
        }

        if (clips is not null)
        {
            AnimationClipPlayer.Apply(clips.FeelingHappy, SnifferAnimations.FeelingHappyBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.Scenting, SnifferAnimations.ScentingBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.Sniffing, SnifferAnimations.SniffingBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.Digging, SnifferAnimations.DiggingBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.Rising, SnifferAnimations.RisingBaked, frame.AgeInTicks, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var animation = clip switch
        {
            "feeling_happy" => SnifferAnimations.FeelingHappyBaked,
            "scenting" => SnifferAnimations.ScentingBaked,
            "sniffing" => SnifferAnimations.SniffingBaked,
            "digging" => SnifferAnimations.DiggingBaked,
            "rising" => SnifferAnimations.RisingBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    /// <summary>
    /// Per-entity Sniffer animation state: the current synced <c>Sniffer.State</c> ordinal plus the five clip clocks.
    /// </summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState
    {
        public readonly AnimationState FeelingHappy = new();
        public readonly AnimationState Scenting = new();
        public readonly AnimationState Sniffing = new();
        public readonly AnimationState Digging = new();
        public readonly AnimationState Rising = new();

        private int _state = SnifferStateIdling;
        private bool _transitionPending;

        /// <summary>Reference <c>Sniffer.isSearching()</c>: true only while the synced state is SEARCHING.</summary>
        public bool IsSearching => _state == SnifferStateSearching;

        public void OnSyncedData(int index, int serializerId, int value)
        {
            // DATA_STATE uses the SNIFFER_STATE serializer (id 35); only a real change triggers the reference reset.
            if (serializerId != SnifferStateSerializerId || value == _state)
            {
                return;
            }

            _state = value;
            _transitionPending = true;
        }

        public void OnTick(in EntityAnimationTickContext context) => CommitTransition(context.GameTime);

        /// <summary>
        /// Reference <c>resetAnimations()</c> followed by the state's <c>startIfStopped(tickCount)</c>. Runs once per
        /// state change; a no-op when no transition is pending.
        /// </summary>
        public void CommitTransition(int tick)
        {
            if (!_transitionPending)
            {
                return;
            }

            _transitionPending = false;

            Digging.Stop();
            Sniffing.Stop();
            Rising.Stop();
            FeelingHappy.Stop();
            Scenting.Stop();

            switch (_state)
            {
                case SnifferStateFeelingHappy:
                    FeelingHappy.StartIfStopped(tick);
                    break;
                case SnifferStateScenting:
                    Scenting.StartIfStopped(tick);
                    break;
                case SnifferStateSniffing:
                    Sniffing.StartIfStopped(tick);
                    break;
                case SnifferStateDigging:
                    Digging.StartIfStopped(tick);
                    break;
                case SnifferStateRising:
                    Rising.StartIfStopped(tick);
                    break;
                // IDLING and SEARCHING start no clip.
            }
        }
    }
}
