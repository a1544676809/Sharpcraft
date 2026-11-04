// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>CreakingModel.setupAnim</c>: the head looks along the render-state pitch/yaw, and while the creature
/// can move the 1.125 s walk loop plays from the walk position/speed. The attack, invulnerable and death clips are
/// one-shot <see cref="AnimationState"/> clips in the reference and are exposed through <see cref="ApplyClip"/>.
/// </summary>
/// <remarks>
/// The reference gates walking on <c>state.canMove</c> plus <c>applyWalk(walkPos, walkSpeed, 1, 1)</c>, i.e. elapsed
/// seconds = walkPos * 0.05 and target scale = min(walkSpeed, 1). The generic frame carries no <c>canMove</c> flag, so
/// this provider uses <see cref="EntityAnimationFrame.WalkAnimationSpeed"/> &gt; 0 as the stand-in; the real flag and
/// the per-clip attack/invulnerability/death clocks are not part of <see cref="EntityAnimationFrame"/> yet.
/// </remarks>
public sealed class CreakingAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["walk", "attack", "invulnerable", "death"];

    public int TypeId => CreakingModelDefinition.CreakingEntityTypeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        var headYaw = frame.Yaw * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, headYaw, 0f), Vector3.Zero));

        var clips = state as State;
        if ((clips?.CanMove ?? true) && frame.WalkAnimationSpeed > 0f)
        {
            CreakingAnimations.WalkBaked.Apply(
                frame.WalkAnimationPos * 0.05f,
                MathF.Min(frame.WalkAnimationSpeed, 1f),
                pose);
        }

        if (clips is not null)
        {
            AnimationClipPlayer.Apply(clips.Attack, CreakingAnimations.AttackBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.Invulnerable, CreakingAnimations.InvulnerableBaked, frame.AgeInTicks, pose);
            AnimationClipPlayer.Apply(clips.Death, CreakingAnimations.DeathBaked, frame.AgeInTicks, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var animation = clip switch
        {
            "walk" => CreakingAnimations.WalkBaked,
            "attack" => CreakingAnimations.AttackBaked,
            "invulnerable" => CreakingAnimations.InvulnerableBaked,
            "death" => CreakingAnimations.DeathBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    private sealed class State : IEntityAnimationState, IEntityEventAnimationState, IEntityTickAnimationState, IEntitySyncedDataAnimationState
    {
        public readonly AnimationState Attack = new();
        public readonly AnimationState Invulnerable = new();
        public readonly AnimationState Death = new();
        private int _attackRemaining;
        private int _invulnerabilityRemaining;
        private bool _tearingDown;

        /// <summary>Reference <c>CAN_MOVE</c> synced flag (entity data index 16); gates the walk loop.</summary>
        public bool CanMove { get; private set; } = true;

        public void OnEntityEvent(int eventId, int gameTime)
        {
            switch (eventId)
            {
                case 4: // START_ATTACKING
                    _attackRemaining = 15;
                    break;
                case 66: // SHAKE (hurt / invulnerable)
                    _invulnerabilityRemaining = 8;
                    break;
            }
        }

        public void OnSyncedData(int index, int serializerId, int value)
        {
            // Creaking data after Mob: CAN_MOVE=16, IS_ACTIVE=17, IS_TEARING_DOWN=18 (BOOLEAN serializer 8).
            if (serializerId != 8)
            {
                return;
            }

            if (index == 16)
            {
                CanMove = value != 0;
            }
            else if (index == 18)
            {
                _tearingDown = value != 0;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            if (_attackRemaining > 0)
            {
                _attackRemaining--;
            }

            if (_invulnerabilityRemaining > 0)
            {
                _invulnerabilityRemaining--;
            }

            Attack.AnimateWhen(_attackRemaining > 0, context.GameTime);
            Invulnerable.AnimateWhen(_invulnerabilityRemaining > 0, context.GameTime);
            Death.AnimateWhen(_tearingDown, context.GameTime);
        }
    }
}
