// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>BreezeModel.setupAnim</c>: all six clips are driven purely by their own <c>AnimationState</c>
/// clocks sampled at <c>state.ageInTicks</c>. The clips are exposed through <see cref="ApplyClip"/>; the hot-path
/// <see cref="Apply"/> writes nothing until those clocks reach the frame (see the remarks on <see cref="Apply"/>).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.breeze.BreezeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/breeze/BreezeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class BreezeAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "shoot", "slide", "slide_back", "inhale", "jump"];

    public BreezeAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    /// <remarks>
    /// The reference reads six per-clip <c>AnimationState</c> clocks off <c>BreezeRenderState</c>:
    /// <c>idle</c>, <c>shoot</c>, <c>slide</c>, <c>slideBack</c>, <c>inhale</c> and <c>longJump</c>. Each
    /// <c>KeyframeAnimation.apply(AnimationState, currentTime)</c> samples
    /// <c>(currentTime - state.startTick) * 50 ms</c> and skips the clip entirely while the state is stopped.
    /// <see cref="EntityAnimationFrame"/> carries only <see cref="EntityAnimationFrame.AgeInTicks"/> (the
    /// <c>currentTime</c> argument) and none of the six start ticks, so no clip can be reproduced here without
    /// inventing state. Apply therefore writes nothing; the reference clips are reachable through
    /// <see cref="ApplyClip"/> until those clocks are threaded onto the frame.
    /// </remarks>
    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        // Blocked on the six Breeze AnimationState clocks listed above; intentionally a no-op.
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var animation = clip switch
        {
            "idle" => BreezeAnimations.IdleBaked,
            "shoot" => BreezeAnimations.ShootBaked,
            "slide" => BreezeAnimations.SlideBaked,
            "slide_back" => BreezeAnimations.SlideBackBaked,
            "inhale" => BreezeAnimations.InhaleBaked,
            "jump" => BreezeAnimations.JumpBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
