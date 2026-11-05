// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>ArmadilloModel.setupAnim</c>: while hiding in its shell it plays the roll-up clip; otherwise it walks.
/// Walk time is <c>walkPos * 0.825</c> and target scale <c>min(walkSpeed * 2.5, 1)</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.armadillo.ArmadilloModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/armadillo/ArmadilloModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class ArmadilloAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["walk", "roll_up", "roll_out", "peek"];

    public ArmadilloAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        if (frame.IsHidingInShell)
        {
            ArmadilloAnimations.RollUpBaked.Apply(frame.AgeInTicks / 20f, 1f, pose);
        }
        else
        {
            ArmadilloAnimations.WalkBaked.Apply(frame.WalkAnimationPos * 0.825f, MathF.Min(frame.WalkAnimationSpeed * 2.5f, 1f), pose);
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "roll_up":
                ArmadilloAnimations.RollUpBaked.Apply(seconds, 1f, pose);
                break;
            case "roll_out":
                ArmadilloAnimations.RollOutBaked.Apply(seconds, 1f, pose);
                break;
            case "peek":
                ArmadilloAnimations.PeekBaked.Apply(seconds, 1f, pose);
                break;
            default:
                Apply(state, frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f, IsHidingInShell = false }, pose);
                break;
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
