// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>Piglin arm poses in reference <c>PiglinArmPose</c> ordinal order.</summary>
public enum PiglinArmPose
{
    AttackingWithMeleeWeapon = 0,
    CrossbowHold = 1,
    CrossbowCharge = 2,
    AdmiringItem = 3,
    Dancing = 4,
    Default = 5
}

/// <summary>
/// Reference <c>PiglinModel.setupAnim</c> (via <c>AbstractPiglinModel</c> / <c>HumanoidModel</c>): head look, ear sway,
/// walking arms/legs, and the derivable arm poses. Shared geometry for Piglin (102), Piglin Brute (103) and Zombified
/// Piglin (158).
/// </summary>
/// <remarks>
/// <para>
/// Derivable arm poses: <b>DANCING</b> (synced, index 19 / serializer 8), <b>CROSSBOW_CHARGE</b> (synced, index 18 /
/// serializer 8, reproduced from <c>frame.TicksUsingItem + frame.PartialTick</c> over the default 25-tick charge),
/// <b>ADMIRING_ITEM</b> (offhand item in the <c>piglin_loved</c> tag), <b>ATTACKING_WITH_MELEE_WEAPON</b> (aggressive and
/// main hand in the <c>piglin_preferred_weapons</c> tag) and <b>CROSSBOW_HOLD</b> (main hand carries the synced
/// <c>charged_projectiles</c> component, projected as <c>frame.MainHandChargedCrossbow</c>). Item tags come from the
/// extracted pack via <see cref="EntityModelResolver.ItemTags"/>.
/// </para>
/// <para>
/// The reference <c>mainArm</c> is not on the frame; DANCING/CROSSBOW_CHARGE/CROSSBOW_HOLD assume the default right hand
/// (all vanilla piglins are right-handed by default). The charge duration uses the main-hand quick-charge level.
/// </para>
/// </remarks>
public sealed class PiglinAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["default", "dancing", "crossbow_charge"];

    private const float DegreesToRadians = MathF.PI / 180f;
    private const float WalkPhase = 0.6662f;
    private const float MaxCrossbowChargeTicks = 25f;
    private const float RidingArmX = -MathF.PI / 5f;

    /// <summary>Stored bind ear z-rotation (adult 30°, matching the model definition).</summary>
    private const float BindEarAngle = MathF.PI / 6f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var live = state as State;
        ApplyPose(SelectArmPose(live, frame), frame, pose);
    }

    private static PiglinArmPose SelectArmPose(State? live, in EntityAnimationFrame frame)
    {
        if (live?.IsDancing == true)
        {
            return PiglinArmPose.Dancing;
        }

        if (IsInItemTag("piglin_loved", frame.OffHandItemId))
        {
            return PiglinArmPose.AdmiringItem;
        }

        if (frame.IsAggressive && IsInItemTag("piglin_preferred_weapons", frame.MainHandItemId))
        {
            return PiglinArmPose.AttackingWithMeleeWeapon;
        }

        if (live?.IsCharging == true)
        {
            return PiglinArmPose.CrossbowCharge;
        }

        if (frame.MainHandChargedCrossbow)
        {
            return PiglinArmPose.CrossbowHold;
        }

        return PiglinArmPose.Default;
    }

    private static bool IsInItemTag(string path, int itemId)
    {
        if (itemId < 0 || EntityModelResolver.Items?.ById(itemId) is not { } item)
        {
            return false;
        }

        return EntityModelResolver.ItemTags.IsInTag(new Identifier("minecraft", path), item.Id);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var armPose = clip switch
        {
            "dancing" => PiglinArmPose.Dancing,
            "crossbow_charge" => PiglinArmPose.CrossbowCharge,
            _ => PiglinArmPose.Default
        };
        ApplyPose(armPose, frame, pose);
    }

    private static void ApplyPose(PiglinArmPose armPose, in EntityAnimationFrame frame, EntityPose pose)
    {
        var age = frame.AgeInTicks;
        var phase = frame.WalkAnimationPos * WalkPhase;
        var speed = frame.WalkAnimationSpeed;
        var riding = frame.IsPassenger;

        // Ears: AbstractPiglinModel sway around the stored bind angle.
        var defaultAngle = (frame.IsBaby ? 5f : 30f) * DegreesToRadians;
        var frequency = (age * 0.1f) + (frame.WalkAnimationPos * 0.5f);
        var amplitude = 0.08f + (speed * 0.4f);
        var leftEarZ = ((-defaultAngle) - (MathF.Cos(frequency * 1.2f) * amplitude)) - (-BindEarAngle);
        var rightEarZ = (defaultAngle + (MathF.Cos(frequency) * amplitude)) - BindEarAngle;

        // Legs: walk cycle, overridden by the seated pose when riding.
        float rLegX, rLegY, rLegZ, lLegX, lLegY, lLegZ;
        if (riding)
        {
            rLegX = lLegX = -1.4137167f;
            rLegY = MathF.PI / 10f;
            lLegY = -MathF.PI / 10f;
            rLegZ = 0.07853982f;
            lLegZ = -0.07853982f;
        }
        else
        {
            rLegX = MathF.Cos(phase) * 1.4f * speed;
            lLegX = MathF.Cos(phase + MathF.PI) * 1.4f * speed;
            rLegY = 0.005f;
            lLegY = -0.005f;
            rLegZ = 0.005f;
            lLegZ = -0.005f;
        }

        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(rLegX, rLegY, rLegZ), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(lLegX, lLegY, lLegZ), Vector3.Zero));

        // Arms baseline (walk; +riding offset), then the arm pose overrides.
        var rX = MathF.Cos(phase + MathF.PI) * 2f * speed * 0.5f + (riding ? RidingArmX : 0f);
        var lX = MathF.Cos(phase) * 2f * speed * 0.5f + (riding ? RidingArmX : 0f);
        float rY = 0f, rZ = 0f, lY = 0f, lZ = 0f;
        var rPos = Vector3.Zero;
        var lPos = Vector3.Zero;
        var headPos = Vector3.Zero;
        var bodyPos = Vector3.Zero;
        var headXRot = frame.Pitch * DegreesToRadians;
        var headYRot = frame.Yaw * DegreesToRadians;

        switch (armPose)
        {
            case PiglinArmPose.Dancing:
            {
                var dancePos = age / 60f;
                var danceSin = MathF.Sin(dancePos * 40f);
                rightEarZ = DegreesToRadians * MathF.Sin(dancePos * 30f) * 10f;
                leftEarZ = -(DegreesToRadians * MathF.Cos(dancePos * 30f) * 10f);
                headPos = new Vector3(MathF.Sin(dancePos * 10f), -(MathF.Sin(dancePos * 40f) + 0.4f), 0f);
                bodyPos = new Vector3(0f, -(danceSin * 0.35f), 0f);
                rZ = DegreesToRadians * (70f + (MathF.Cos(dancePos * 40f) * 10f));
                lZ = -rZ;
                rPos = new Vector3(0f, -((danceSin * 0.5f) - 0.5f), 0f);
                lPos = new Vector3(0f, -((danceSin * 0.5f) + 0.5f), 0f);
                break;
            }

            case PiglinArmPose.CrossbowCharge:
            {
                // AnimationUtils.animateCrossbowCharge(right, left, maxCharge, ticksUsingItem, holdingInRightArm=true).
                var maxCharge = MathF.Max(1f, MaxCrossbowChargeTicks - (5f * frame.MainHandQuickChargeLevel));
                var charge = Math.Clamp((frame.TicksUsingItem + frame.PartialTick) / maxCharge, 0f, 1f);
                rY = -0.8f;
                rX = -0.97079635f;
                lX = -0.97079635f + (((-MathF.PI / 2f) - (-0.97079635f)) * charge);
                lY = 0.4f + ((0.85f - 0.4f) * charge);
                break;
            }

            case PiglinArmPose.AdmiringItem:
            {
                // Head looks at the admired item; the non-main arm holds it up.
                headXRot = 0.5f;
                headYRot = 0f;
                lY = 0.5f;
                lX = -0.9f;
                break;
            }

            case PiglinArmPose.AttackingWithMeleeWeapon:
            {
                var swing = frame.SwingAnimation;
                if (swing <= 0f)
                {
                    // holdWeaponHigh: main arm raised (mainArm is right for all vanilla piglins).
                    rX = -1.8f;
                    break;
                }

                var attack2 = MathF.Sin(swing * MathF.PI);
                var attack = MathF.Sin((1f - ((1f - swing) * (1f - swing))) * MathF.PI);
                rY = MathF.PI / 20f;
                lY = -MathF.PI / 20f;
                rX = -1.8849558f + (MathF.Cos(age * 0.09f) * 0.15f) + (attack2 * 2.2f) - (attack * 0.4f);
                lX = (MathF.Cos(age * 0.19f) * 0.5f) + (attack2 * 1.2f) - (attack * 0.4f);
                // bobArms: right scale +1, left scale -1.
                rZ += MathF.Cos(age * 0.09f) * 0.05f + 0.05f;
                rX += MathF.Sin(age * 0.067f) * 0.05f;
                lZ -= MathF.Cos(age * 0.09f) * 0.05f + 0.05f;
                lX -= MathF.Sin(age * 0.067f) * 0.05f;
                break;
            }

            case PiglinArmPose.CrossbowHold:
            {
                // AnimationUtils.animateCrossbowHold(right, left, head, holdingInRightArm=true).
                rY = -0.3f + headYRot;
                lY = 0.6f + headYRot;
                rX = (-MathF.PI / 2f) + headXRot + 0.1f;
                lX = -1.5f + headXRot;
                break;
            }

            case PiglinArmPose.Default:
            default:
                break;
        }

        pose.Add("head", new PoseOffset(headPos, new Vector3(headXRot, headYRot, 0f), Vector3.Zero));
        pose.Add("left_ear", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, leftEarZ), Vector3.Zero));
        pose.Add("right_ear", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, rightEarZ), Vector3.Zero));
        pose.Add("body", new PoseOffset(bodyPos, Vector3.Zero, Vector3.Zero));
        pose.Add("right_arm", new PoseOffset(rPos, new Vector3(rX, rY, rZ), Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(lPos, new Vector3(lX, lY, lZ), Vector3.Zero));
    }

    /// <summary>Per-entity Piglin state: the synced dancing and crossbow-charging flags.</summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState, IEntityArmPoseAnimationState
    {
        public bool IsDancing { get; private set; }

        public bool IsCharging { get; private set; }

        public int ArmPose => (int)(IsDancing ? PiglinArmPose.Dancing : IsCharging ? PiglinArmPose.CrossbowCharge : PiglinArmPose.Default);

        public void OnSyncedData(int index, int serializerId, int value)
        {
            if (serializerId != 8)
            {
                return;
            }

            if (index == 18)
            {
                IsCharging = value != 0;
            }
            else if (index == 19)
            {
                IsDancing = value != 0;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
        }
    }
}
