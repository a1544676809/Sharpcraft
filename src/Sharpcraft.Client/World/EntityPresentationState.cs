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
    /// <summary>Walk position/speed smoothing, ported from the reference <c>WalkAnimationState</c>.</summary>
    public WalkAnimationState Walk { get; } = new();

    /// <summary>Wing-flap clock for chickens/parrots, ported from the reference <c>flap</c> fields.</summary>
    public FlapAnimationState Flap { get; } = new();

    /// <summary>The synced ground flag from the last move/teleport packet.</summary>
    public bool IsOnGround { get; internal set; }
}
