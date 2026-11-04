// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Resolves the reference <c>HumanoidModel.ArmPose</c> for each arm of a humanoid mob (docs/modules/entity-animation.md).
/// </summary>
/// <remarks>
/// Mirrors <c>HumanoidMobRenderer.getArmPose</c> (SPEAR when the held item is in the <c>spears</c> tag or the current
/// swing is a STAB) and <c>AbstractSkeletonRenderer.getArmPose</c> (the main arm uses BOW_AND_ARROW when aggressive
/// with a bow). The CROSSBOW_* / BLOCK / SPYGLASS poses of <c>HumanoidModel</c> are only produced by the player,
/// illager and piglin renderers, which keep their own providers, so they never appear here.
/// </remarks>
internal static class HumanoidArmPoseResolver
{
    /// <summary>Item id of <c>minecraft:bow</c>, resolved once from the item registry (-1 when unavailable).</summary>
    public static readonly int BowItemId = ResolveBowItemId();

    private static readonly Identifier SpearsTag = new("minecraft", "spears");

    public static void Compute(in EntityAnimationFrame frame, bool skeleton, out HumanoidArmPose right, out HumanoidArmPose left)
    {
        var mainLeft = frame.IsLeftHanded;
        right = UsesSpearPose(frame, HumanoidSide.Right, mainLeft) ? HumanoidArmPose.Spear : HumanoidArmPose.Empty;
        left = UsesSpearPose(frame, HumanoidSide.Left, mainLeft) ? HumanoidArmPose.Spear : HumanoidArmPose.Empty;

        if (skeleton && frame.IsAggressive && IsHoldingBow(frame.MainHandItemId))
        {
            if (mainLeft)
            {
                left = HumanoidArmPose.BowAndArrow;
            }
            else
            {
                right = HumanoidArmPose.BowAndArrow;
            }
        }
    }

    public static bool IsHoldingBow(int mainHandItemId) => BowItemId > 0 && mainHandItemId == BowItemId;

    private static bool UsesSpearPose(in EntityAnimationFrame frame, HumanoidSide side, bool mainLeft)
    {
        var mainSide = mainLeft ? HumanoidSide.Left : HumanoidSide.Right;
        var itemId = side == mainSide ? frame.MainHandItemId : frame.OffHandItemId;
        if (itemId > 0 && EntityModelResolver.Items?.ById(itemId) is { } item
            && EntityModelResolver.ItemTags.IsInTag(SpearsTag, item.Id))
        {
            return true;
        }

        return frame.SwingAnimation > 0f && frame.SwingType == 2 && AsArm(frame.SwingHand, mainLeft) == side;
    }

    private static HumanoidSide AsArm(int hand, bool mainLeft)
    {
        if (hand == 0)
        {
            return mainLeft ? HumanoidSide.Left : HumanoidSide.Right;
        }

        return mainLeft ? HumanoidSide.Right : HumanoidSide.Left;
    }

    private static int ResolveBowItemId()
    {
        var registry = EntityModelResolver.Items;
        return registry is null ? -1 : registry.GetId(Identifier.Parse("minecraft:bow"));
    }

    private enum HumanoidSide
    {
        Right,
        Left
    }
}
