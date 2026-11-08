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

    /// <summary>
    /// Reference <c>AvatarRenderer.getArmPose</c>: the player (and any avatar-style renderer) selects a pose per hand from
    /// the held item and the using-item state, then resolves two-handed poses against the off hand.
    /// </summary>
    public static void ComputeForAvatar(in EntityAnimationFrame frame, out HumanoidArmPose right, out HumanoidArmPose left)
    {
        var mainLeft = frame.IsLeftHanded;
        var mainPose = PoseFor(frame, frame.MainHandItemId, isMainHand: true);
        var offPose = PoseFor(frame, frame.OffHandItemId, isMainHand: false);
        if (IsTwoHanded(mainPose))
        {
            // The reference drops a two-handed main pose down to ITEM (or EMPTY) on the other arm.
            offPose = frame.OffHandItemId <= 0 ? HumanoidArmPose.Empty : HumanoidArmPose.Item;
        }

        right = mainLeft ? offPose : mainPose;
        left = mainLeft ? mainPose : offPose;
    }

    private static HumanoidArmPose PoseFor(in EntityAnimationFrame frame, int itemId, bool isMainHand)
    {
        if (itemId <= 0)
        {
            return HumanoidArmPose.Empty;
        }

        // Reference: a charged crossbow held while not swinging takes CROSSBOW_HOLD. We do not decode the charged
        // projectiles component, so only the swing half of that test is applied here (registered limitation).
        if (frame.SwingAnimation <= 0f && IsItem(itemId, "crossbow"))
        {
            return HumanoidArmPose.CrossbowHold;
        }

        if (frame.IsUsingItem && IsUsingHand(frame, isMainHand) && UseAnimationAt(itemId) is { } use)
        {
            return use;
        }

        return IsSpearItem(itemId) ? HumanoidArmPose.Spear : HumanoidArmPose.Item;
    }

    /// <summary>
    /// Which hand is using the item. The reference reads <c>getUsedItemHand()</c>, which is only locally authoritative;
    /// the client-side flag is ambient, so the main hand wins when both could apply.
    /// </summary>
    private static bool IsUsingHand(in EntityAnimationFrame frame, bool isMainHand) =>
        isMainHand || frame.MainHandItemId <= 0;

    /// <summary>
    /// Pose implied by the item's reference <c>use_animation</c> component. The component itself is not decoded, so the
    /// vanilla item names that declare each animation are mapped here.
    /// </summary>
    private static HumanoidArmPose? UseAnimationAt(int itemId) => ItemName(itemId) switch
    {
        "shield" => HumanoidArmPose.Block,
        "bow" => HumanoidArmPose.BowAndArrow,
        "trident" => HumanoidArmPose.ThrowTrident,
        "crossbow" => HumanoidArmPose.CrossbowCharge,
        "spyglass" => HumanoidArmPose.Spyglass,
        "goat_horn" => HumanoidArmPose.TootHorn,
        "brush" => HumanoidArmPose.Brush,
        _ => IsSpearItem(itemId) ? HumanoidArmPose.Spear : null
    };

    private static bool IsTwoHanded(HumanoidArmPose pose) =>
        pose is HumanoidArmPose.BowAndArrow or HumanoidArmPose.CrossbowCharge or HumanoidArmPose.CrossbowHold;

    private static bool IsItem(int itemId, string path) => ItemName(itemId) == path;

    private static bool IsSpearItem(int itemId) =>
        itemId > 0 &&
        EntityModelResolver.Items?.ById(itemId) is { } item &&
        EntityModelResolver.ItemTags.IsInTag(SpearsTag, item.Id);

    private static string? ItemName(int itemId) =>
        itemId > 0 && EntityModelResolver.Items?.ById(itemId) is { } item ? item.Id.Path : null;

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
