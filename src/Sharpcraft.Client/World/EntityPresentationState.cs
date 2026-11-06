// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Per-entity client-side presentation state (ADR-0022). This is the non-authoritative subset the reference client
/// maintains purely to render an entity: walk/flap clocks and the ground flag. It never contains authoritative AI,
/// pathfinding or combat state.
/// </summary>
/// <remarks>
/// One instance per <see cref="TrackedEntity"/>; fields are advanced once per client tick in
/// <see cref="EntityTracker.Tick"/> and sampled with the partial tick in <see cref="EntityTracker.Interpolate"/>.
/// Further derivations (isInWater, passengers/isRiding, tentacle angle, pose) are staged P0b/P1 in
/// docs/modules/entity-presentation-state.md.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.Entity", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/Entity.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Client-side presentation state aggregated from synced data")]
public sealed class EntityPresentationState
{
    /// <summary>Creates the state, enabling the squid tentacle clock for squid/glow-squid type ids.</summary>
    public EntityPresentationState(int typeId)
    {
        // Squid (130) and glow squid (62). Kept as literals so this layer does not depend on render model classes.
        if (typeId is 130 or 62)
        {
            Tentacle = new SquidTentacleAnimationState();
        }

        if (typeId == 111)
        {
            RabbitHop = new RabbitHopAnimationState();
            RabbitIdle = new RabbitIdleAnimationState();
        }

        if (typeId == 105)
        {
            PolarStand = new PolarBearStandAnimationState();
        }

        if (typeId == 97)
        {
            PandaPose = new PandaPoseAnimationState();
        }

        if (typeId == 149)
        {
            WitherHeads = new WitherHeadAnimationState();
        }
    }

    /// <summary>Walk position/speed smoothing, ported from the reference <c>WalkAnimationState</c>.</summary>
    public WalkAnimationState Walk { get; } = new();

    /// <summary>Wing-flap clock for chickens/parrots, ported from the reference <c>flap</c> fields.</summary>
    public FlapAnimationState Flap { get; } = new();

    /// <summary>Arm-swing clock, ported from the reference <c>LivingEntity.SwingState</c>.</summary>
    public SwingAnimationState Swing { get; } = new();

    /// <summary>Elytra wing rotation state, ported from the reference <c>ElytraAnimationState</c>.</summary>
    public ElytraAnimationState Elytra { get; } = new();

    /// <summary>Squid tentacle clock, or null for other types.</summary>
    public SquidTentacleAnimationState? Tentacle { get; }

    /// <summary>Rabbit hop clock, or null for other types.</summary>
    public RabbitHopAnimationState? RabbitHop { get; }

    /// <summary>Rabbit idle-head-tilt clock, or null for other types.</summary>
    public RabbitIdleAnimationState? RabbitIdle { get; }

    /// <summary>Polar bear stand ease clock, or null for other types.</summary>
    public PolarBearStandAnimationState? PolarStand { get; }

    /// <summary>Panda sit/lie/roll amount clock, or null for other types.</summary>
    public PandaPoseAnimationState? PandaPose { get; }

    /// <summary>Wither side-head aim clock, or null for other types.</summary>
    public WitherHeadAnimationState? WitherHeads { get; }

    /// <summary>Wither side-head alternative target entity id 1 (data index 17), or 0 when none.</summary>
    public int WitherTarget1 { get; internal set; }

    /// <summary>Wither side-head alternative target entity id 2 (data index 18), or 0 when none.</summary>
    public int WitherTarget2 { get; internal set; }

    /// <summary>Mob NoAI flag (Mob flags bit 0), used by the Rabbit idle gate.</summary>
    public bool IsNoAi { get; internal set; }

    /// <summary>Whether the entity has a leash holder (set by the set_entity_link packet).</summary>
    public bool IsLeashed { get; internal set; }

    /// <summary>Synced entity pose ordinal (Entity.DATA_POSE), used for pose-specific dimensions.</summary>
    public int Pose { get; internal set; }

    /// <summary>Synced SCALE attribute value (default 1); multiplies dimensions and model size.</summary>
    public float Scale { get; internal set; } = 1f;

    /// <summary>The synced ground flag from the last move/teleport packet.</summary>
    public bool IsOnGround { get; internal set; }

    /// <summary>Whether the entity's feet block is water (local block query).</summary>
    public bool IsInWater { get; internal set; }

    /// <summary>The synced resting flag for the types that carry one (bat flags bit 0).</summary>
    public bool IsResting { get; internal set; }

    /// <summary>The synced sitting flag for tamable mobs (TamableAnimal flags bit 0), used by the parrot pose.</summary>
    public bool IsSitting { get; internal set; }

    /// <summary>Whether the entity moved horizontally this tick (reference Dolphin-style "isMoving").</summary>
    public bool IsMoving { get; internal set; }

    /// <summary>Mob aggressive flag (reference <c>Mob.isAggressive</c>, synced DATA_MOB_FLAGS bit 2).</summary>
    public bool IsAggressive { get; internal set; }

    /// <summary>Mob left-handed flag (reference <c>Mob.isLeftHanded</c>, synced DATA_MOB_FLAGS bit 1).</summary>
    public bool IsLeftHanded { get; internal set; }

    /// <summary>Whether the entity is using an item (reference DATA_LIVING_ENTITY_FLAGS bit 0).</summary>
    public bool IsUsingItem { get; internal set; }

    /// <summary>Which hand is using the item: 0 = main, 1 = off (reference DATA_LIVING_ENTITY_FLAGS bit 1).</summary>
    public int UseItemHand { get; internal set; }

    /// <summary>Ticks elapsed since the current item use started; reset when use stops.</summary>
    public int UseItemTicks { get; internal set; }

    /// <summary>Elytra fall-flying flag (reference Entity shared flag bit 7).</summary>
    public bool IsFallFlying { get; internal set; }

    /// <summary>Current swim amount 0..1 (reference LivingEntity swimAmount).</summary>
    public float SwimAmount { get; internal set; }

    /// <summary>Swim amount at the previous tick (reference swimAmountO), for partial-tick interpolation.</summary>
    public float SwimAmountO { get; internal set; }

    /// <summary>
    /// Advances the swim clock one tick (reference <c>LivingEntity.updateSwimAmount</c>): move 0.09 towards 1 while
    /// visually swimming, otherwise 0.09 towards 0.
    /// </summary>
    public void UpdateSwimAmount(bool isVisuallySwimming)
    {
        SwimAmountO = SwimAmount;
        SwimAmount = isVisuallySwimming
            ? MathF.Min(1f, SwimAmount + 0.09f)
            : MathF.Max(0f, SwimAmount - 0.09f);
    }

    /// <summary>Interpolates the swim amount at a partial tick (reference <c>getSwimAmount</c>).</summary>
    public float SwimAmountAt(float partialTick) => SwimAmountO + ((SwimAmount - SwimAmountO) * partialTick);

    /// <summary>Cube-mob size from the synced <c>ID_SIZE</c> (1 for other entities).</summary>
    public int CubeSize { get; internal set; } = 1;

    /// <summary>Current squish value (reference <c>AbstractCubeMob.squish</c>).</summary>
    public float Squish { get; internal set; }

    /// <summary>Squish value at the previous tick (reference <c>oSquish</c>).</summary>
    public float OldSquish { get; internal set; }

    /// <summary>Squish target, -0.5 on landing and 1.0 on jump (reference <c>targetSquish</c>).</summary>
    public float TargetSquish { get; internal set; }

    /// <summary>Whether the cube mob was on the ground on the previous tick.</summary>
    public bool WasOnGround { get; internal set; }

    /// <summary>Shulker attach face direction id (0..5), or -1 when not applicable.</summary>
    public int AttachFace { get; internal set; } = -1;

    /// <summary>Fox/AI sleeping flag (Fox DATA_FLAGS bit 32).</summary>
    public bool IsSleeping { get; internal set; }

    /// <summary>Fox crouching flag (Fox DATA_FLAGS bit 4).</summary>
    public bool IsCrouching { get; internal set; }

    /// <summary>Fox pouncing flag (Fox DATA_FLAGS bit 16).</summary>
    public bool IsPouncing { get; internal set; }

    /// <summary>Fox faceplanted flag (Fox DATA_FLAGS bit 64).</summary>
    public bool IsFaceplanted { get; internal set; }

    /// <summary>Synced health (reference LivingEntity DATA_HEALTH).</summary>
    public float Health { get; internal set; }

    /// <summary>Panda unhappy counter (synced int, drives the unhappy head-shake).</summary>
    public int UnhappyCounter { get; internal set; }

    /// <summary>Panda sneeze counter (synced int).</summary>
    public int SneezeCounter { get; internal set; }

    /// <summary>Panda eating flag (EAT_COUNTER &gt; 0).</summary>
    public bool IsEating { get; internal set; }

    /// <summary>Panda rolling flag (Panda flags bit 4).</summary>
    public bool IsRolling { get; internal set; }

    /// <summary>Panda lying-on-back flag (Panda flags bit 16).</summary>
    public bool IsOnBack { get; internal set; }

    /// <summary>Panda main gene is WORRIED (reference getVariant()==WORRIED).</summary>
    public bool IsWorried { get; internal set; }

    /// <summary>Parrot party flag (jukebox song playing nearby).</summary>
    public bool IsPartyParrot { get; internal set; }

    /// <summary>Jukebox block position that last started the party, or long.MinValue when none.</summary>
    public long PartyJukebox { get; internal set; } = long.MinValue;

    /// <summary>Polar bear standing flag (synced boolean).</summary>
    public bool IsStanding { get; internal set; }

    /// <summary>Sprinting flag (reference Entity shared flag bit 3).</summary>
    public bool IsSprinting { get; internal set; }

    /// <summary>Cat lying flag (reference Cat IS_LYING, synced boolean).</summary>
    public bool IsLying { get; internal set; }

    /// <summary>Cat relax-state-one flag (reference Cat RELAX_STATE_ONE, synced boolean).</summary>
    public bool IsRelaxStateOne { get; internal set; }

    /// <summary>Cat lie-down amount 0..1 (reference <c>lieDownAmount</c>).</summary>
    public float LieDownAmount { get; internal set; }

    /// <summary>Cat lie-down amount at the previous tick (for partial-tick interpolation).</summary>
    public float LieDownAmountO { get; internal set; }

    /// <summary>Cat lie-down tail amount 0..1 (reference <c>lieDownAmountTail</c>).</summary>
    public float LieDownAmountTail { get; internal set; }

    /// <summary>Cat lie-down tail amount at the previous tick.</summary>
    public float LieDownAmountTailO { get; internal set; }

    /// <summary>Cat relax-state-one amount 0..1 (reference <c>relaxStateOneAmount</c>).</summary>
    public float RelaxStateOneAmount { get; internal set; }

    /// <summary>Cat relax-state-one amount at the previous tick.</summary>
    public float RelaxStateOneAmountO { get; internal set; }

    // Client-derived body/head rotation, ported from the reference BodyRotationControl. The body lags the head and,
    // when the head turns beyond the mob's limit while idle, the body is dragged around to follow it.
    /// <summary>Client-derived body yaw (reference <c>yBodyRot</c>), used for the entity-level yaw.</summary>
    public float BodyYaw { get; internal set; }

    /// <summary>Body yaw at the previous tick (reference <c>yBodyRotO</c>), for partial-tick interpolation.</summary>
    public float BodyYawO { get; internal set; }

    /// <summary>Effective head yaw (reference <c>yHeadRot</c>), clamped to the body while moving.</summary>
    public float HeadYaw { get; internal set; }

    /// <summary>Head yaw at the previous tick (reference <c>yHeadRotO</c>).</summary>
    public float HeadYawO { get; internal set; }

    private float _lastStableHeadYaw;
    private int _headStableTime;

    /// <summary>Seeds the body/head rotation to the spawn yaw so the first tick has no spurious turn.</summary>
    public void InitializeRotation(float yaw)
    {
        BodyYaw = yaw;
        BodyYawO = yaw;
        HeadYaw = yaw;
        HeadYawO = yaw;
        _lastStableHeadYaw = yaw;
        _headStableTime = 0;
    }

    /// <summary>
    /// Advances the client-side body rotation one tick, porting the reference <c>BodyRotationControl.clientTick</c>:
    /// while moving the body snaps to the entity yaw and the head is clamped to it; while idle, a head turn beyond 15°
    /// drags the body to within <paramref name="maxHeadYaw"/> of the head, and a stable head is returned to front.
    /// </summary>
    public void TickBodyRotation(float entityYaw, float headYaw, bool moving, float maxHeadYaw)
    {
        BodyYawO = BodyYaw;
        HeadYawO = HeadYaw;
        HeadYaw = headYaw;

        if (moving)
        {
            BodyYaw = entityYaw;
            HeadYaw = RotateIfNecessary(HeadYaw, BodyYaw, maxHeadYaw);
            _lastStableHeadYaw = HeadYaw;
            _headStableTime = 0;
            return;
        }

        if (MathF.Abs(HeadYaw - _lastStableHeadYaw) > 15f)
        {
            _headStableTime = 0;
            _lastStableHeadYaw = HeadYaw;
            BodyYaw = RotateIfNecessary(BodyYaw, HeadYaw, maxHeadYaw);
        }
        else
        {
            _headStableTime++;
            if (_headStableTime > 10)
            {
                var timeSince = _headStableTime - 10;
                var fraction = Math.Clamp(timeSince / 10f, 0f, 1f);
                var remaining = maxHeadYaw * (1f - fraction);
                BodyYaw = RotateIfNecessary(BodyYaw, HeadYaw, remaining);
            }
        }
    }

    /// <summary>Reference <c>Mth.rotateIfNecessary</c>: move <paramref name="baseAngle"/> toward the target until the
    /// difference is within <paramref name="maxAngleDiff"/>; unchanged when already within.</summary>
    private static float RotateIfNecessary(float baseAngle, float targetAngle, float maxAngleDiff)
    {
        var delta = Wrap180(targetAngle - baseAngle);
        var clamped = Math.Clamp(delta, -maxAngleDiff, maxAngleDiff);
        return targetAngle - clamped;
    }

    private static float Wrap180(float degrees)
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
}
