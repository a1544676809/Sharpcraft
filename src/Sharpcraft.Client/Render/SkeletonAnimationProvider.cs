// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SkeletonModel.setupAnim</c>, shared by the Skeleton, Stray, Bogged and Wither Skeleton. The base pose
/// is <c>HumanoidModel.setupAnim</c> (see <see cref="HumanoidAnimationProvider"/>); the only model-specific addition is
/// an aggressive melee stance applied when the mob is aggressive and not holding a bow.
/// </summary>
/// <remarks>
/// The melee branch overwrites the arms (both <c>xRot</c>/<c>yRot</c>/<c>zRot</c>) with the raised-arm pose and then
/// adds <c>AnimationUtils.bobArms</c>. It is gated on <c>isAggressive &amp;&amp; !isHoldingBow</c>, exactly like the
/// reference. Holding a bow instead selects <c>ArmPose.BOW_AND_ARROW</c> in the reference, which is a separate arm-pose
/// channel we do not model yet (documented gap, not approximated here).
/// </remarks>
public sealed class SkeletonAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    /// <summary>Item id of <c>minecraft:bow</c>, resolved once from the item registry (-1 when unavailable).</summary>
    public static readonly int BowItemId = ResolveBowItemId();

    public SkeletonAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplySkeleton(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var sandbox = frame with { IsAggressive = false };
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
        var aggressiveMelee = frame.IsAggressive && !IsHoldingBow(frame.MainHandItemId);
        HumanoidAnimationProvider.ApplyWalk(frame, pose, applyArms: !aggressiveMelee);
        if (aggressiveMelee)
        {
            ApplyAggressiveArms(frame, pose);
        }
    }

    private static void ApplyAggressiveArms(in EntityAnimationFrame frame, EntityPose pose)
    {
        // Reference: swingAnimation is the attack swing progress (0..1).
        var swingAnimation = frame.SwingAnimation;
        var swing2 = MathF.Sin(swingAnimation * MathF.PI);
        var swing = MathF.Sin((1f - (1f - swingAnimation) * (1f - swingAnimation)) * MathF.PI);
        var baseX = -(MathF.PI / 2f) - (swing2 * 1.2f) + (swing * 0.4f);
        var baseY = 0.1f - (swing2 * 0.6f);

        // AnimationUtils.bobArms: bobModelPart(right, age, +1) and bobModelPart(left, age, -1).
        var age = frame.AgeInTicks;
        var bobX = MathF.Sin(age * 0.067f) * 0.05f;
        var bobZ = (MathF.Cos(age * 0.09f) * 0.05f) + 0.05f;

        pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(baseX + bobX, -baseY, bobZ), Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(baseX - bobX, baseY, -bobZ), Vector3.Zero));
    }

    private static bool IsHoldingBow(int mainHandItemId) => BowItemId > 0 && mainHandItemId == BowItemId;

    private static int ResolveBowItemId()
    {
        var registry = EntityModelResolver.Items;
        return registry is null ? -1 : registry.GetId(Identifier.Parse("minecraft:bow"));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
