// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SkeletonModel.setupAnim</c> (shared by Skeleton, Stray, Bogged and Wither Skeleton): the full
/// <c>HumanoidModel.setupAnim</c> chain plus an aggressive melee-arm stance applied when the mob is aggressive and
/// not holding a bow. See <see cref="HumanoidModelPose"/> for the shared pose maths.
/// </summary>
public sealed class SkeletonAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    /// <summary>Item id of <c>minecraft:bow</c>, resolved once from the item registry (-1 when unavailable).</summary>
    public static int BowItemId => HumanoidArmPoseResolver.BowItemId;

    public SkeletonAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplySkeleton(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var sandbox = frame with { IsAggressive = false, SwingAnimation = 0f, SwingType = 0 };
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplySkeleton(sandbox with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplySkeleton(sandbox with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplySkeleton(in EntityAnimationFrame frame, EntityPose pose)
    {
        HumanoidArmPoseResolver.Compute(frame, skeleton: true, out var right, out var left);
        var holdingBow = HumanoidArmPoseResolver.IsHoldingBow(frame.MainHandItemId);
        HumanoidModelPose.Apply(frame, pose, HumanoidVariant.Skeleton, right, left, holdingBow);
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
