// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>BatModel.setupAnim</c>: rest and flight are two ported looping keyframe clips.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.ambient.BatModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/ambient/BatModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class BatAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["resting", "flying"];

    public BatAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        if (frame.IsResting)
        {
            BatAnimations.RestingBaked.Apply(frame.AgeInTicks / 20f, 1f, pose);
        }
        else
        {
            BatAnimations.FlyingBaked.Apply(frame.AgeInTicks / 20f, 1f, pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose) =>
        (string.Equals(clip, "resting", StringComparison.Ordinal) ? BatAnimations.RestingBaked : BatAnimations.FlyingBaked)
            .Apply(seconds, 1f, pose);

    private sealed class State : IEntityAnimationState
    {
    }
}
