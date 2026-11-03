// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Common animation inputs every creature receives each frame.
/// </summary>
/// <remarks>
/// Only fields that already exist on the generic <see cref="EntityRenderState"/> are carried here. Per-creature
/// special factors (for example Axolotl water/ground factors or Frog one-shot clocks) live in the creature's own
/// <see cref="IEntityAnimationState"/>; they must not be pushed into this shared struct
/// (docs/analysis/render/entity-animation-architecture.md 3.1, 6.2).
/// </remarks>
public readonly record struct EntityAnimationFrame(
    int TypeId,
    float AgeInTicks,
    float WalkAnimationPos,
    float WalkAnimationSpeed,
    float Pitch,
    float Flap,
    float FlapSpeed,
    float TentacleAngle,
    bool IsResting,
    bool IsBaby,
    bool IsHidingInShell)
{
    /// <summary>Projects the generic render state onto the frame. The mesher calls this once per entity per frame.</summary>
    public static EntityAnimationFrame From(in EntityRenderState state) => new(
        state.TypeId,
        state.AgeInTicks,
        state.WalkAnimationPos,
        state.WalkAnimationSpeed,
        state.Pitch,
        state.Flap,
        state.FlapSpeed,
        state.TentacleAngle,
        state.IsResting,
        state.IsBaby,
        state.IsHidingInShell);
}

/// <summary>
/// Opaque per-entity animation state. Each provider owns a concrete implementation holding its clocks and derived
/// factors; the world layer only stores and carries the reference.
/// </summary>
/// <remarks>
/// This interface deliberately lives in <c>Client.World</c> and mentions no rendering type, so the world layer does
/// not depend on <c>Client.Render</c> (docs/analysis/render/entity-animation-architecture.md 7). The provider that
/// creates and consumes it lives in <c>Client.Render</c>.
/// </remarks>
public interface IEntityAnimationState
{
}
