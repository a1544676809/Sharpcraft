// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>Humanoid arm-pose ordinal, matching the reference <c>HumanoidModel.ArmPose</c>.</summary>
internal enum HumanoidArmPose
{
    Empty = 0,
    Item = 1,
    Block = 2,
    BowAndArrow = 3,
    ThrowTrident = 4,
    CrossbowCharge = 5,
    CrossbowHold = 6,
    Spyglass = 7,
    TootHorn = 8,
    Brush = 9,
    Spear = 10
}

/// <summary>Which concrete humanoid subclass drives the pose. Both share <c>HumanoidModel.setupAnim</c>.</summary>
internal enum HumanoidVariant
{
    /// <summary>Plain <c>HumanoidModel.setupAnim</c> with no subclass arm override (e.g. Enderman base).</summary>
    Plain,

    /// <summary>Zombie/Drowned/Husk/Giant: <c>setupAttackAnimation</c> also runs <c>animateZombieArms</c>.</summary>
    Zombie,

    /// <summary>Skeleton/Stray/Bogged/Wither Skeleton: an aggressive melee-arm branch runs after <c>setupAnim</c>.</summary>
    Skeleton
}

/// <summary>
/// Full port of the reference <c>HumanoidModel.setupAnim</c> for the humanoid mobs (docs/modules/entity-animation.md).
/// It reproduces head yaw/pitch, walk phases, the passenger stance, arm poses, attack animation, crouching offsets,
/// arm bobbing and the swim pose, plus the two model-specific overrides (<c>animateZombieArms</c> for the zombie family
/// and the aggressive melee stance for the skeleton family).
/// </summary>
/// <remarks>
/// Model transforms are accumulated in local part state and emitted as absolute pose offsets, because the reference
/// assigns (not offsets) model-part fields. Rotations are transcribed in radians; positions are converted from the
/// reference y-down, mirrored model space into the y-up storage used by the mesher. The SPEAR arm pose and the STAB
/// swing animation depend on <c>SpearAnimations</c> (item-model geometry) and are deliberately left as no-ops and
/// recorded as blocked; the CROSSBOW_* arm poses are unreachable for these renderers (only player/illager/piglin).
/// </remarks>
internal static class HumanoidModelPose
{
    private const float Pi = MathF.PI;
    private const float DegToRad = Pi / 180f;

    public static void Apply(
        in EntityAnimationFrame frame,
        EntityPose pose,
        HumanoidVariant variant,
        HumanoidArmPose rightArmPose,
        HumanoidArmPose leftArmPose,
        bool isHoldingBow)
    {
        var head = new PartState();
        var body = new PartState();
        var rightArm = new PartState();
        var leftArm = new PartState();
        var rightLeg = new PartState();
        var leftLeg = new PartState();

        var mainLeft = frame.IsLeftHanded;
        var mainArm = mainLeft ? Arm.Left : Arm.Right;
        var offHandArm = Opponent(mainArm);
        var ageScale = frame.IsBaby ? 0.5f : 1f;
        var speedValue = MathF.Max(1f, frame.SpeedValue);

        // Head look. Reference state.yRot is the head yaw relative to the body, so subtract the body yaw we already
        // apply through the model transform.
        head.RotX = frame.Pitch * DegToRad;
        head.RotY = WrapDegrees(frame.HeadYaw - frame.Yaw) * DegToRad;
        if (frame.IsFallFlying)
        {
            head.RotX = -Pi / 4f;
        }

        // Walk phases (speedValue normalises fast mobs).
        var animationPos = frame.WalkAnimationPos;
        var animationSpeed = frame.WalkAnimationSpeed;
        rightArm.RotX = MathF.Cos(animationPos * 0.6662f + Pi) * animationSpeed / speedValue;
        leftArm.RotX = MathF.Cos(animationPos * 0.6662f) * animationSpeed / speedValue;
        rightLeg.RotX = MathF.Cos(animationPos * 0.6662f) * 1.4f * animationSpeed / speedValue;
        leftLeg.RotX = MathF.Cos(animationPos * 0.6662f + Pi) * 1.4f * animationSpeed / speedValue;
        rightLeg.RotY = 0.005f;
        leftLeg.RotY = -0.005f;
        rightLeg.RotZ = 0.005f;
        leftLeg.RotZ = -0.005f;

        if (frame.IsPassenger)
        {
            rightArm.RotX += -Pi / 5f;
            leftArm.RotX += -Pi / 5f;
            rightLeg.RotX = -1.4137167f;
            rightLeg.RotY = Pi / 10f;
            rightLeg.RotZ = 0.07853982f;
            leftLeg.RotX = -1.4137167f;
            leftLeg.RotY = -Pi / 10f;
            leftLeg.RotZ = -0.07853982f;
        }

        // Which hand's pose is applied first (reference priority rule).
        Arm prioritizeArm;
        if (frame.IsUsingItem)
        {
            prioritizeArm = AsArm(frame.UseItemHand, mainLeft);
        }
        else
        {
            prioritizeArm = IsTwoHanded(ArmPoseOf(offHandArm, rightArmPose, leftArmPose)) ? mainArm : offHandArm;
        }

        if (prioritizeArm == Arm.Right)
        {
            PoseRightArm(ref rightArm, ref leftArm, head, rightArmPose, frame);
            if (!AffectsOffhandPose(rightArmPose))
            {
                PoseLeftArm(ref leftArm, ref rightArm, head, leftArmPose, frame);
            }
        }
        else
        {
            PoseLeftArm(ref leftArm, ref rightArm, head, leftArmPose, frame);
            if (!AffectsOffhandPose(leftArmPose))
            {
                PoseRightArm(ref rightArm, ref leftArm, head, rightArmPose, frame);
            }
        }

        SetupAttack(frame, ref head, ref body, ref rightArm, ref leftArm, mainLeft, ageScale, rightArmPose, leftArmPose, variant);

        if (frame.IsCrouching)
        {
            body.RotX = 0.5f;
            rightArm.RotX += 0.4f;
            leftArm.RotX += 0.4f;
            rightLeg.PosZ += 4f;
            leftLeg.PosZ += 4f;
            head.PosY += 4.2f;
            body.PosY += 3.2f;
            leftArm.PosY += 3.2f;
            rightArm.PosY += 3.2f;
        }

        if (rightArmPose != HumanoidArmPose.Spyglass)
        {
            Bob(ref rightArm, frame.AgeInTicks, 1f);
        }

        if (leftArmPose != HumanoidArmPose.Spyglass)
        {
            Bob(ref leftArm, frame.AgeInTicks, -1f);
        }

        if (frame.SwimAmount > 0f)
        {
            SetupSwim(ref head, ref rightArm, ref leftArm, ref rightLeg, ref leftLeg, animationPos, frame, rightArmPose, leftArmPose, mainLeft);
        }

        // Skeleton: SkeletonModel.setupAnim runs after super and replaces the arms in an aggressive stance.
        if (variant == HumanoidVariant.Skeleton && frame.IsAggressive && !isHoldingBow)
        {
            ApplySkeletonAggressiveArms(frame, ref rightArm, ref leftArm);
        }

        pose.Set("head", head.ToOffset());
        pose.Set("body", body.ToOffset());
        pose.Set("right_arm", rightArm.ToOffset());
        pose.Set("left_arm", leftArm.ToOffset());
        pose.Set("right_leg", rightLeg.ToOffset());
        pose.Set("left_leg", leftLeg.ToOffset());
    }

    private static void PoseRightArm(ref PartState arm, ref PartState other, in PartState head, HumanoidArmPose pose, in EntityAnimationFrame frame)
    {
        switch (pose)
        {
            case HumanoidArmPose.Empty:
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.Item:
                arm.RotX = (arm.RotX * 0.5f) - (Pi / 10f);
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.Block:
                BlockingArm(ref arm, head, true);
                break;
            case HumanoidArmPose.BowAndArrow:
                arm.RotY = -0.1f + head.RotY;
                other.RotY = 0.1f + head.RotY + 0.4f;
                arm.RotX = (-Pi / 2f) + head.RotX;
                other.RotX = (-Pi / 2f) + head.RotX;
                break;
            case HumanoidArmPose.ThrowTrident:
                arm.RotX = (arm.RotX * 0.5f) - Pi;
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.Spyglass:
                arm.RotX = Math.Clamp(head.RotX - 1.9198622f - (frame.IsCrouching ? Pi / 12f : 0f), -2.4f, 3.3f);
                arm.RotY = head.RotY - (Pi / 12f);
                break;
            case HumanoidArmPose.TootHorn:
                arm.RotX = Math.Clamp(head.RotX, -1.2f, 1.2f) - 1.4835298f;
                arm.RotY = head.RotY - (Pi / 6f);
                break;
            case HumanoidArmPose.Brush:
                arm.RotX = (arm.RotX * 0.5f) - (Pi / 5f);
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.CrossbowCharge:
            case HumanoidArmPose.CrossbowHold:
            case HumanoidArmPose.Spear:
                // Crossbow poses are unreachable for the humanoid mobs; SPEAR/STAB need SpearAnimations (blocked).
                break;
        }
    }

    private static void PoseLeftArm(ref PartState arm, ref PartState other, in PartState head, HumanoidArmPose pose, in EntityAnimationFrame frame)
    {
        switch (pose)
        {
            case HumanoidArmPose.Empty:
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.Item:
                arm.RotX = (arm.RotX * 0.5f) - (Pi / 10f);
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.Block:
                BlockingArm(ref arm, head, false);
                break;
            case HumanoidArmPose.BowAndArrow:
                other.RotY = -0.1f + head.RotY - 0.4f;
                arm.RotY = 0.1f + head.RotY;
                other.RotX = (-Pi / 2f) + head.RotX;
                arm.RotX = (-Pi / 2f) + head.RotX;
                break;
            case HumanoidArmPose.ThrowTrident:
                arm.RotX = (arm.RotX * 0.5f) - Pi;
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.Spyglass:
                arm.RotX = Math.Clamp(head.RotX - 1.9198622f - (frame.IsCrouching ? Pi / 12f : 0f), -2.4f, 3.3f);
                arm.RotY = head.RotY + (Pi / 12f);
                break;
            case HumanoidArmPose.TootHorn:
                arm.RotX = Math.Clamp(head.RotX, -1.2f, 1.2f) - 1.4835298f;
                arm.RotY = head.RotY + (Pi / 6f);
                break;
            case HumanoidArmPose.Brush:
                arm.RotX = (arm.RotX * 0.5f) - (Pi / 5f);
                arm.RotY = 0f;
                break;
            case HumanoidArmPose.CrossbowCharge:
            case HumanoidArmPose.CrossbowHold:
            case HumanoidArmPose.Spear:
                break;
        }
    }

    private static void BlockingArm(ref PartState arm, in PartState head, bool right)
    {
        arm.RotX = (arm.RotX * 0.5f) - 0.9424779f + Math.Clamp(head.RotX, -Pi * 4f / 9f, 0.43633232f);
        arm.RotY = (right ? -30f : 30f) * DegToRad + Math.Clamp(head.RotY, -Pi / 6f, Pi / 6f);
    }

    private static void SetupAttack(
        in EntityAnimationFrame frame,
        ref PartState head,
        ref PartState body,
        ref PartState rightArm,
        ref PartState leftArm,
        bool mainLeft,
        float ageScale,
        HumanoidArmPose rightArmPose,
        HumanoidArmPose leftArmPose,
        HumanoidVariant variant)
    {
        var swingAnimation = frame.SwingAnimation;
        if (swingAnimation > 0f && frame.SwingType != 0)
        {
            var swingArm = AsArm(frame.SwingHand, mainLeft);
            body.RotY = MathF.Sin(MathF.Sqrt(swingAnimation) * (Pi * 2f)) * 0.2f;
            if (swingArm == Arm.Left)
            {
                body.RotY *= -1f;
            }

            // Reference setupAttackAnimation *assigns* ModelPart.x/z to an absolute part position; the rest arm pivot is
            // x = -5 (right) / +5 (left), z = 0. A PoseOffset is a delta from that bind pivot, so convert with
            // delta = assigned - bindPivot. At rest this is ~0, so the arms stay attached (only the body twist moves them).
            const float armBindPivotX = 5f;
            rightArm.PosZ = MathF.Sin(body.RotY) * 5f * ageScale;
            rightArm.PosX = (-MathF.Cos(body.RotY) * 5f * ageScale) + armBindPivotX;
            leftArm.PosZ = -MathF.Sin(body.RotY) * 5f * ageScale;
            leftArm.PosX = (MathF.Cos(body.RotY) * 5f * ageScale) - armBindPivotX;
            rightArm.RotY += body.RotY;
            leftArm.RotY += body.RotY;
            leftArm.RotX += body.RotY;

            if (frame.SwingType == 1)
            {
                // WHACK
                var aa = MathF.Sin(EaseOutQuart(swingAnimation) * Pi);
                var bb = MathF.Sin(swingAnimation * Pi) * -(head.RotX - 0.7f) * 0.75f;
                if (swingArm == Arm.Left)
                {
                    leftArm.RotX -= (aa * 1.2f) + bb;
                    leftArm.RotY += body.RotY * 2f;
                    leftArm.RotZ += MathF.Sin(swingAnimation * Pi) * -0.4f;
                }
                else
                {
                    rightArm.RotX -= (aa * 1.2f) + bb;
                    rightArm.RotY += body.RotY * 2f;
                    rightArm.RotZ += MathF.Sin(swingAnimation * Pi) * -0.4f;
                }
            }

            // STAB (type 2) needs SpearAnimations.thirdPersonAttackHand -> blocked, no-op.
        }

        if (variant == HumanoidVariant.Zombie)
        {
            AnimateZombieArms(frame, ref rightArm, ref leftArm, rightArmPose, leftArmPose);
        }
    }

    private static void AnimateZombieArms(in EntityAnimationFrame frame, ref PartState rightArm, ref PartState leftArm, HumanoidArmPose rightArmPose, HumanoidArmPose leftArmPose)
    {
        if (rightArmPose == HumanoidArmPose.Spear || leftArmPose == HumanoidArmPose.Spear)
        {
            return;
        }

        var raiseArms = !frame.IsBaby || frame.MainHandItemId <= 0;
        var armDrop = raiseArms ? -Pi / (frame.IsAggressive ? 1.5f : 2.25f) : 0f;
        var swing = frame.SwingAnimation;
        var attackYRotModifier = (raiseArms ? 1f : -1f) * MathF.Sin(swing * Pi);
        var attackXRotModifier = MathF.Sin((1f - ((1f - swing) * (1f - swing))) * Pi);
        var xRot = armDrop + (attackYRotModifier * 1.2f) - (attackXRotModifier * 0.4f);
        var yRot = 0.1f - (attackYRotModifier * 0.6f);
        if (rightArmPose != HumanoidArmPose.ThrowTrident)
        {
            rightArm.RotX = xRot;
            rightArm.RotY = raiseArms ? -yRot : yRot;
            rightArm.RotZ = 0f;
        }

        if (leftArmPose != HumanoidArmPose.ThrowTrident)
        {
            leftArm.RotX = xRot;
            leftArm.RotY = raiseArms ? yRot : -yRot;
            leftArm.RotZ = 0f;
        }
    }

    private static void ApplySkeletonAggressiveArms(in EntityAnimationFrame frame, ref PartState rightArm, ref PartState leftArm)
    {
        var swingAnimation = frame.SwingAnimation;
        var swing2 = MathF.Sin(swingAnimation * Pi);
        var swing = MathF.Sin((1f - ((1f - swingAnimation) * (1f - swingAnimation))) * Pi);
        rightArm.RotZ = 0f;
        leftArm.RotZ = 0f;
        rightArm.RotY = -(0.1f - (swing2 * 0.6f));
        leftArm.RotY = 0.1f - (swing2 * 0.6f);
        rightArm.RotX = -Pi / 2f;
        leftArm.RotX = -Pi / 2f;
        rightArm.RotX -= (swing2 * 1.2f) - (swing * 0.4f);
        leftArm.RotX -= (swing2 * 1.2f) - (swing * 0.4f);
        Bob(ref rightArm, frame.AgeInTicks, 1f);
        Bob(ref leftArm, frame.AgeInTicks, -1f);
    }

    private static void SetupSwim(
        ref PartState head,
        ref PartState rightArm,
        ref PartState leftArm,
        ref PartState rightLeg,
        ref PartState leftLeg,
        float animationPos,
        in EntityAnimationFrame frame,
        HumanoidArmPose rightArmPose,
        HumanoidArmPose leftArmPose,
        bool mainLeft)
    {
        var swimAmount = frame.SwimAmount;
        if (!frame.IsFallFlying)
        {
            head.RotX = RotLerpRad(swimAmount, head.RotX, -Pi / 4f);
        }

        var swimPos = animationPos % 26f;
        Arm? swingArm = frame.SwingAnimation > 0f && frame.SwingType != 0 ? AsArm(frame.SwingHand, mainLeft) : null;
        var rightSwim = rightArmPose != HumanoidArmPose.Spear && swingArm != Arm.Right ? swimAmount : 0f;
        var leftSwim = leftArmPose != HumanoidArmPose.Spear && swingArm != Arm.Left ? swimAmount : 0f;
        if (!frame.IsUsingItem)
        {
            if (swimPos < 14f)
            {
                var factor = QuadraticArmUpdate(swimPos) / QuadraticArmUpdate(14f);
                leftArm.RotX = RotLerpRad(leftSwim, leftArm.RotX, 0f);
                rightArm.RotX = Lerp(rightSwim, rightArm.RotX, 0f);
                leftArm.RotY = RotLerpRad(leftSwim, leftArm.RotY, Pi);
                rightArm.RotY = Lerp(rightSwim, rightArm.RotY, Pi);
                leftArm.RotZ = RotLerpRad(leftSwim, leftArm.RotZ, Pi + (1.8707964f * factor));
                rightArm.RotZ = Lerp(rightSwim, rightArm.RotZ, Pi - (1.8707964f * factor));
            }
            else if (swimPos < 22f)
            {
                var internalPos = (swimPos - 14f) / 8f;
                leftArm.RotX = RotLerpRad(leftSwim, leftArm.RotX, (Pi / 2f) * internalPos);
                rightArm.RotX = Lerp(rightSwim, rightArm.RotX, (Pi / 2f) * internalPos);
                leftArm.RotY = RotLerpRad(leftSwim, leftArm.RotY, Pi);
                rightArm.RotY = Lerp(rightSwim, rightArm.RotY, Pi);
                leftArm.RotZ = RotLerpRad(leftSwim, leftArm.RotZ, 5.012389f - (1.8707964f * internalPos));
                rightArm.RotZ = Lerp(rightSwim, rightArm.RotZ, 1.2707963f + (1.8707964f * internalPos));
            }
            else if (swimPos < 26f)
            {
                var internalPos = (swimPos - 22f) / 4f;
                leftArm.RotX = RotLerpRad(leftSwim, leftArm.RotX, (Pi / 2f) - ((Pi / 2f) * internalPos));
                rightArm.RotX = Lerp(rightSwim, rightArm.RotX, (Pi / 2f) - ((Pi / 2f) * internalPos));
                leftArm.RotY = RotLerpRad(leftSwim, leftArm.RotY, Pi);
                rightArm.RotY = Lerp(rightSwim, rightArm.RotY, Pi);
                leftArm.RotZ = RotLerpRad(leftSwim, leftArm.RotZ, Pi);
                rightArm.RotZ = Lerp(rightSwim, rightArm.RotZ, Pi);
            }
        }

        leftLeg.RotX = Lerp(swimAmount, leftLeg.RotX, 0.3f * MathF.Cos((animationPos * 0.33333334f) + Pi));
        rightLeg.RotX = Lerp(swimAmount, rightLeg.RotX, 0.3f * MathF.Cos(animationPos * 0.33333334f));
    }

    private static void Bob(ref PartState part, float ageInTicks, float scale)
    {
        part.RotZ += scale * ((MathF.Cos(ageInTicks * 0.09f) * 0.05f) + 0.05f);
        part.RotX += scale * (MathF.Sin(ageInTicks * 0.067f) * 0.05f);
    }

    private static float QuadraticArmUpdate(float x) => (-65f * x) + (x * x);

    private static float Lerp(float alpha, float from, float to) => from + (alpha * (to - from));

    private static float RotLerpRad(float alpha, float from, float to)
    {
        var diff = to - from;
        while (diff < -Pi)
        {
            diff += Pi * 2f;
        }

        while (diff >= Pi)
        {
            diff -= Pi * 2f;
        }

        return from + (alpha * diff);
    }

    private static float WrapDegrees(float degrees)
    {
        var value = degrees % 360f;
        if (value >= 180f)
        {
            value -= 360f;
        }

        if (value < -180f)
        {
            value += 360f;
        }

        return value;
    }

    private static float EaseOutQuart(float x)
    {
        var f = 1f - x;
        return 1f - (f * f * f * f);
    }

    private static HumanoidArmPose ArmPoseOf(Arm arm, HumanoidArmPose right, HumanoidArmPose left) => arm == Arm.Right ? right : left;

    private static bool IsTwoHanded(HumanoidArmPose pose) => pose is HumanoidArmPose.BowAndArrow or HumanoidArmPose.CrossbowCharge or HumanoidArmPose.CrossbowHold;

    private static bool AffectsOffhandPose(HumanoidArmPose pose) =>
        pose is HumanoidArmPose.BowAndArrow or HumanoidArmPose.ThrowTrident or HumanoidArmPose.CrossbowCharge or HumanoidArmPose.CrossbowHold;

    private static Arm AsArm(int hand, bool mainLeft) =>
        hand == 0 ? (mainLeft ? Arm.Left : Arm.Right) : (mainLeft ? Arm.Right : Arm.Left);

    private static Arm Opponent(Arm arm) => arm == Arm.Right ? Arm.Left : Arm.Right;

    private enum Arm
    {
        Right,
        Left
    }

    /// <summary>Mutable model-part transform in our y-up storage (absolute, relative to the bind pose = zero).</summary>
    private struct PartState
    {
        public float PosX;
        public float PosY;
        public float PosZ;
        public float RotX;
        public float RotY;
        public float RotZ;

        public readonly PoseOffset ToOffset()
        {
            // Reference model space is y-down; the mesher stores the scale(-1,-1,1)-flipped model, so position offsets
            // are flipped in X and Y (Z unchanged), matching the vertex conversion in PlayerModelDefinition.FromVanilla.
            return new PoseOffset(new Vector3(-PosX, -PosY, PosZ), new Vector3(RotX, RotY, RotZ), Vector3.Zero);
        }
    }
}
