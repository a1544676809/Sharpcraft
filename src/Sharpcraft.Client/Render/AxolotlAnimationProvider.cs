// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Baby <c>AxolotlModel.setupAnim</c>: walking plays the floor walk loop at <c>walkPos * 0.75</c> with target scale
/// <c>min(walkSpeed * 30, 1)</c>; standing plays the baby floor idle loop. Adult axolotl needs water/ground/moving
/// factors that the render state does not yet carry, so adult animation is intentionally not applied here.
/// </summary>
public sealed class AxolotlAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips =
    [
        "baby_idle_floor", "idle_floor_underwater", "idle_underwater", "baby_swim",
        "walk_floor", "walk_floor_underwater", "play_dead"
    ];

    public AxolotlAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        if (!frame.IsBaby)
        {
            return;
        }

        if (frame.WalkAnimationSpeed > 0f)
        {
            AxolotlAnimations.WalkFloorBaked.Apply(frame.WalkAnimationPos * 0.75f, MathF.Min(frame.WalkAnimationSpeed * 30f, 1f), pose);
        }
        else
        {
            AxolotlAnimations.BabyIdleFloorBaked.Apply(frame.AgeInTicks / 20f, 1f, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (!frame.IsBaby)
        {
            return;
        }

        var animation = clip switch
        {
            "baby_idle_floor" => AxolotlAnimations.BabyIdleFloorBaked,
            "idle_floor_underwater" => AxolotlAnimations.IdleFloorUnderwaterBaked,
            "idle_underwater" => AxolotlAnimations.IdleUnderwaterBaked,
            "baby_swim" => AxolotlAnimations.BabySwimBaked,
            "walk_floor" => AxolotlAnimations.WalkFloorBaked,
            "walk_floor_underwater" => AxolotlAnimations.WalkFloorUnderwaterBaked,
            "play_dead" => AxolotlAnimations.PlayDeadBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
