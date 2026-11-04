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
    }

    /// <summary>Walk position/speed smoothing, ported from the reference <c>WalkAnimationState</c>.</summary>
    public WalkAnimationState Walk { get; } = new();

    /// <summary>Wing-flap clock for chickens/parrots, ported from the reference <c>flap</c> fields.</summary>
    public FlapAnimationState Flap { get; } = new();

    /// <summary>Arm-swing clock, ported from the reference <c>LivingEntity.SwingState</c>.</summary>
    public SwingAnimationState Swing { get; } = new();

    /// <summary>Squid tentacle clock, or null for other types.</summary>
    public SquidTentacleAnimationState? Tentacle { get; }

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

    /// <summary>Ticks elapsed since the current item use started; reset when use stops.</summary>
    public int UseItemTicks { get; internal set; }

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
}
