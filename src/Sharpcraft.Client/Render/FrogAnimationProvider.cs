// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>FrogModel.setupAnim</c>: walking plays the walk loop at <c>walkPos * 0.05</c> with target scale
/// <c>min(walkSpeed * 2.5, 1)</c>; standing plays the water idle loop. The one-shot clips (croak, jump, tongue) are
/// exposed for the sandbox and for the future interaction state wiring.
/// </summary>
public sealed class FrogAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["croak", "walk", "jump", "tongue", "swim", "idle_water"];

    public FrogAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        if (frame.WalkAnimationSpeed > 0f)
        {
            FrogAnimations.WalkBaked.Apply(frame.WalkAnimationPos * 0.05f, MathF.Min(frame.WalkAnimationSpeed * 2.5f, 1f), pose);
        }
        else
        {
            FrogAnimations.IdleWaterBaked.Apply(frame.AgeInTicks / 20f, 1f, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var animation = clip switch
        {
            "croak" => FrogAnimations.CroakBaked,
            "walk" => FrogAnimations.WalkBaked,
            "jump" => FrogAnimations.JumpBaked,
            "tongue" => FrogAnimations.TongueBaked,
            "swim" => FrogAnimations.SwimBaked,
            "idle_water" => FrogAnimations.IdleWaterBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
