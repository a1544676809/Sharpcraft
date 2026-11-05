// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>ZombieModel.setupAnim</c> (shared by Zombie, Drowned, Husk and Giant): the full
/// <c>HumanoidModel.setupAnim</c> chain plus the <c>animateZombieArms</c> override applied from
/// <c>setupAttackAnimation</c>. See <see cref="HumanoidModelPose"/> for the shared pose maths.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.HumanoidModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class HumanoidAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public HumanoidAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyZombie(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var sandbox = frame with { IsAggressive = false, SwingAnimation = 0f, SwingType = 0 };
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplyZombie(sandbox with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplyZombie(sandbox with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyZombie(in EntityAnimationFrame frame, EntityPose pose)
    {
        HumanoidArmPoseResolver.Compute(frame, skeleton: false, out var right, out var left);
        HumanoidModelPose.Apply(frame, pose, HumanoidVariant.Zombie, right, left, isHoldingBow: false);
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
