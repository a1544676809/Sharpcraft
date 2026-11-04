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

        if (frame.WalkAnimationSpeed > 0f)
        {
            CreakingAnimations.WalkBaked.Apply(
                frame.WalkAnimationPos * 0.05f,
                MathF.Min(frame.WalkAnimationSpeed, 1f),
                pose);
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

    private sealed class State : IEntityAnimationState
    {
    }
}
