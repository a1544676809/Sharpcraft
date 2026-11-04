// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>IllagerModel.setupAnim</c> shared by the Vindicator, Evoker, Pillager and Illusioner. The head looks
/// along the render-state pitch/yaw, the legs/arms walk (or ride), and the arm pose is driven by each subclass's
/// <c>getArmPose</c>. Arm visibility is toggled by the mesh from <see cref="IllagerArmPose"/> (0 = crossed arms).
/// </summary>
/// <remarks>
/// <para>
/// The arm-pose selection needs synced flags (Raider celebrating index 16 / serializer 8, Spellcaster spell-casting
/// index 17 / serializer 0, Pillager charging index 16 / serializer 8) plus frame inputs (aggressive, main-hand item
/// id). <see cref="State"/> holds those and exposes <see cref="State.ArmPose"/> through
/// <see cref="IEntityArmPoseAnimationState"/> so the mesher toggles the crossed group vs the separate arms.
/// </para>
/// <para>
/// <b>Missing inputs (reported, not silently faked):</b>
/// <list type="bullet">
/// <item>Crossbow charge: reproduced from <c>frame.TicksUsingItem + frame.PartialTick</c> over the default 25-tick
/// charge duration. Quick-charge enchantment is not modelled (it needs the item's data components), so a quick-charged
/// crossbow charges visually at the default rate.</item>
/// <item>The reference <c>mainArm</c> is not on the frame; the weapon swing uses the default right hand
/// (all vanilla illagers are right-handed).</item>
/// <item>The tracker reads <see cref="State.ArmPose"/> while building the render state (before the per-frame Apply), so
/// the exposed pose can lag one frame; the pose used for the offsets in Apply itself is current.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class IllagerAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips =
    [
        "crossed", "attacking", "spellcasting", "bow", "crossbow_hold", "crossbow_charge", "celebrating", "neutral"
    ];

    private const float DegreesToRadians = MathF.PI / 180f;
    private const float WalkPhase = 0.6662f;

    private const float RidingArmX = -MathF.PI / 5f;
    private const float RidingLegX = -1.4137167f;
    private const float RidingLegY = MathF.PI / 10f;
    private const float RidingLegZ = 0.07853982f;
    private const float AttackingArmDrop = -MathF.PI / 1.5f;

    /// <summary>Default crossbow charge duration in ticks (CrossbowItem.getChargeDuration = floor(1.25 * 20)).</summary>
    private const float MaxCrossbowChargeTicks = 25f;

    /// <summary>Item id of <c>minecraft:crossbow</c>, resolved once from the item registry.</summary>
    public static readonly int CrossbowItemId = ResolveCrossbowItemId();

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State(TypeId);

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var live = state as State;
        var kind = live?.Kind ?? KindOf(TypeId);
        var armPose = ComputeArmPose(
            kind,
            frame.IsAggressive,
            frame.MainHandItemId,
            live?.IsCelebrating ?? false,
            live?.IsSpellCasting ?? false,
            live?.IsCharging ?? false);
        live?.CacheFrame(frame.IsAggressive, frame.MainHandItemId);
        ApplyPose(armPose, frame, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var armPose = clip switch
        {
            "attacking" => IllagerArmPose.Attacking,
            "spellcasting" => IllagerArmPose.Spellcasting,
            "bow" => IllagerArmPose.BowAndArrow,
            "crossbow_hold" => IllagerArmPose.CrossbowHold,
            "crossbow_charge" => IllagerArmPose.CrossbowCharge,
            "celebrating" => IllagerArmPose.Celebrating,
            "neutral" => IllagerArmPose.Neutral,
            _ => IllagerArmPose.Crossed
        };
        ApplyPose(armPose, frame, pose);
    }

    private static void ApplyPose(IllagerArmPose armPose, in EntityAnimationFrame frame, EntityPose pose)
    {
        var age = frame.AgeInTicks;
        var phase = frame.WalkAnimationPos * WalkPhase;
        var speed = frame.WalkAnimationSpeed;
        var riding = frame.IsPassenger;

        // Head look.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        // Legs: riding pose or a walk cycle.
        float rLegX, rLegY, rLegZ, lLegX, lLegY, lLegZ;
        if (riding)
        {
            rLegX = lLegX = RidingLegX;
            rLegY = RidingLegY;
            lLegY = -RidingLegY;
            rLegZ = RidingLegZ;
            lLegZ = -RidingLegZ;
        }
        else
        {
            rLegX = MathF.Cos(phase) * 1.4f * speed * 0.5f;
            lLegX = MathF.Cos(phase + MathF.PI) * 1.4f * speed * 0.5f;
            rLegY = lLegY = rLegZ = lLegZ = 0f;
        }

        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(rLegX, rLegY, rLegZ), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(lLegX, lLegY, lLegZ), Vector3.Zero));

        // Arms: baseline (riding or walking), then the arm-pose branch overrides the visible arm rotations.
        float rX, rY, rZ, lX, lY, lZ;
        if (riding)
        {
            rX = lX = RidingArmX;
            rY = rZ = lY = lZ = 0f;
        }
        else
        {
            rX = MathF.Cos(phase + MathF.PI) * 2f * speed * 0.5f;
            lX = MathF.Cos(phase) * 2f * speed * 0.5f;
            rY = rZ = lY = lZ = 0f;
        }

        var headPitch = frame.Pitch * DegreesToRadians;
        var headYaw = frame.Yaw * DegreesToRadians;
        var bobZ = MathF.Cos(age * 0.09f) * 0.05f + 0.05f;
        var bobX = MathF.Sin(age * 0.067f) * 0.05f;

        switch (armPose)
        {
            case IllagerArmPose.Attacking:
                if (!frame.HasMainHandItem)
                {
                    // animateZombieArms(aggressive=true) then bobArms.
                    var attackY = MathF.Sin(frame.SwingAnimation * MathF.PI);
                    var attackX = MathF.Sin((1f - ((1f - frame.SwingAnimation) * (1f - frame.SwingAnimation))) * MathF.PI);
                    var xRot = AttackingArmDrop + (attackY * 1.2f) - (attackX * 0.4f);
                    var yRot = 0.1f - (attackY * 0.6f);
                    rX = xRot + bobX;
                    rY = -yRot;
                    rZ = bobZ;
                    lX = xRot - bobX;
                    lY = yRot;
                    lZ = -bobZ;
                }
                else
                {
                    // swingWeaponDown(mainArm = RIGHT) then bobArms.
                    var attack2 = MathF.Sin(frame.SwingAnimation * MathF.PI);
                    var attack = MathF.Sin((1f - ((1f - frame.SwingAnimation) * (1f - frame.SwingAnimation))) * MathF.PI);
                    rY = MathF.PI / 20f;
                    lY = -MathF.PI / 20f;
                    rZ = bobZ;
                    lZ = -bobZ;
                    rX = -1.8849558f + (MathF.Cos(age * 0.09f) * 0.15f) + (attack2 * 2.2f) - (attack * 0.4f) + bobX;
                    lX = (MathF.Cos(age * 0.19f) * 0.5f) + (attack2 * 1.2f) - (attack * 0.4f) - bobX;
                }

                break;

            case IllagerArmPose.Spellcasting:
                rX = MathF.Cos(age * WalkPhase) * 0.25f;
                lX = MathF.Cos(age * WalkPhase) * 0.25f;
                rZ = MathF.PI * 3f / 4f;
                lZ = -MathF.PI * 3f / 4f;
                rY = lY = 0f;
                break;

            case IllagerArmPose.BowAndArrow:
                rY = -0.1f + headYaw;
                rX = (-MathF.PI / 2f) + headPitch;
                lX = -0.9424779f + headPitch;
                lY = headYaw - 0.4f;
                lZ = MathF.PI / 2f;
                break;

            case IllagerArmPose.CrossbowHold:
                rY = -0.3f + headYaw;
                lY = 0.6f + headYaw;
                rX = (-MathF.PI / 2f) + headPitch + 0.1f;
                lX = -1.5f + headPitch;
                break;

            case IllagerArmPose.CrossbowCharge:
                // AnimationUtils.animateCrossbowCharge(rightArm, leftArm, maxCharge, ticksUsingItem, holdingInRightArm: true).
                // progress = min((ticksUsingItem + partialTick) / maxCharge, 1); maxCharge = default 25 ticks.
                // Quick-charge enchantment is not modelled (needs the item's data components).
                var charge = Math.Clamp((frame.TicksUsingItem + frame.PartialTick) / MaxCrossbowChargeTicks, 0f, 1f);
                rY = -0.8f;
                rX = -0.97079635f;
                lX = -0.97079635f + (((-MathF.PI / 2f) - (-0.97079635f)) * charge);
                lY = 0.4f + ((0.85f - 0.4f) * charge);
                break;

            case IllagerArmPose.Celebrating:
                rZ = 2.670354f;
                lZ = -MathF.PI * 3f / 4f;
                rX = MathF.Cos(age * WalkPhase) * 0.05f;
                lX = MathF.Cos(age * WalkPhase) * 0.05f;
                rY = lY = 0f;
                break;

            case IllagerArmPose.Neutral:
            case IllagerArmPose.Crossed:
            default:
                // Neutral keeps the walk/riding baseline; crossed hides the separate arms (the crossed group is static).
                break;
        }

        pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(rX, rY, rZ), Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(lX, lY, lZ), Vector3.Zero));
    }

    private static IllagerArmPose ComputeArmPose(
        IllagerKind kind,
        bool aggressive,
        int mainHandItemId,
        bool celebrating,
        bool spellCasting,
        bool charging)
    {
        switch (kind)
        {
            case IllagerKind.Vindicator:
                return aggressive ? IllagerArmPose.Attacking : celebrating ? IllagerArmPose.Celebrating : IllagerArmPose.Crossed;
            case IllagerKind.Evoker:
                // SpellcasterIllager.getArmPose: spell-casting wins, else celebration, else crossed.
                return spellCasting ? IllagerArmPose.Spellcasting : celebrating ? IllagerArmPose.Celebrating : IllagerArmPose.Crossed;
            case IllagerKind.Pillager:
                if (charging)
                {
                    return IllagerArmPose.CrossbowCharge;
                }

                if (CrossbowItemId > 0 && mainHandItemId == CrossbowItemId)
                {
                    return IllagerArmPose.CrossbowHold;
                }

                return aggressive ? IllagerArmPose.Attacking : IllagerArmPose.Neutral;
            case IllagerKind.Illusioner:
                return spellCasting ? IllagerArmPose.Spellcasting : aggressive ? IllagerArmPose.BowAndArrow : IllagerArmPose.Crossed;
            default:
                return IllagerArmPose.Crossed;
        }
    }

    private static IllagerKind KindOf(int typeId) => typeId switch
    {
        IllagerModelDefinition.VindicatorEntityTypeId => IllagerKind.Vindicator,
        IllagerModelDefinition.EvokerEntityTypeId => IllagerKind.Evoker,
        IllagerModelDefinition.PillagerEntityTypeId => IllagerKind.Pillager,
        IllagerModelDefinition.IllusionerEntityTypeId => IllagerKind.Illusioner,
        _ => IllagerKind.Other
    };

    private static int ResolveCrossbowItemId()
    {
        var registry = EntityModelResolver.Items;
        return registry is null ? -1 : registry.GetId(Identifier.Parse("minecraft:crossbow"));
    }

    private enum IllagerKind
    {
        Other,
        Vindicator,
        Evoker,
        Pillager,
        Illusioner
    }

    /// <summary>Per-entity illager state: synced arm-pose flags plus the last frame's aggressive/item inputs.</summary>
    private sealed class State(int typeId) : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState, IEntityArmPoseAnimationState
    {
        private bool _celebrating;
        private bool _spellCasting;
        private bool _charging;
        private bool _aggressive;
        private int _mainHandItemId = -1;

        public IllagerKind Kind { get; } = KindOf(typeId);

        public bool IsCelebrating => _celebrating;

        public bool IsSpellCasting => _spellCasting;

        public bool IsCharging => _charging;

        public int ArmPose => (int)ComputeArmPose(Kind, _aggressive, _mainHandItemId, _celebrating, _spellCasting, _charging);

        public void CacheFrame(bool aggressive, int mainHandItemId)
        {
            _aggressive = aggressive;
            _mainHandItemId = mainHandItemId;
        }

        public void OnSyncedData(int index, int serializerId, int value)
        {
            if (index == 16 && serializerId == 8)
            {
                // Raider celebrating and Pillager charging share the same synced slot.
                if (Kind == IllagerKind.Pillager)
                {
                    _charging = value != 0;
                }
                else
                {
                    _celebrating = value != 0;
                }
            }
            else if (index == 17 && serializerId == 0)
            {
                // SpellcasterIllager DATA_SPELL_CASTING_ID: non-zero spell id while casting.
                _spellCasting = value > 0;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            if (context.MainHandItemId >= 0)
            {
                _mainHandItemId = context.MainHandItemId;
            }
        }
    }
}
