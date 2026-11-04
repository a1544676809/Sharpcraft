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
    float Yaw,
    float Flap,
    float FlapSpeed,
    float TentacleAngle,
    bool IsResting,
    bool IsBaby,
    bool IsHidingInShell,
    float PartialTick = 0f,
    bool HasMainHandItem = false,
    bool HasOffHandItem = false,
    int EntityId = 0,
    bool IsOnGround = false,
    bool IsInWater = false,
    bool IsPassenger = false,
    bool IsVehicle = false,
    bool IsTame = false,
    bool IsSitting = false,
    bool IsMoving = false,
    float HeadYaw = 0f,
    bool IsAngry = false,
    float SwingAnimation = 0f,
    bool IsAggressive = false,
    int MainHandItemId = -1,
    bool IsUsingItem = false,
    int TicksUsingItem = 0,
    int OffHandItemId = -1,
    bool MainHandChargedCrossbow = false)
{
    /// <summary>Projects the generic render state onto the frame. The mesher calls this once per entity per frame.</summary>
    public static EntityAnimationFrame From(in EntityRenderState state) => new(
        state.TypeId,
        state.AgeInTicks,
        state.WalkAnimationPos,
        state.WalkAnimationSpeed,
        state.Pitch,
        state.Yaw,
        state.Flap,
        state.FlapSpeed,
        state.TentacleAngle,
        state.IsResting,
        state.IsBaby,
        state.IsHidingInShell,
        state.AnimationPartialTick,
        state.HasMainHandItem,
        state.HasOffHandItem,
        state.Id,
        state.IsOnGround,
        state.IsInWater,
        state.IsPassenger,
        state.IsVehicle,
        state.IsTame,
        state.IsSitting,
        state.IsMoving,
        state.HeadYaw,
        state.IsAngry,
        state.SwingAnimation,
        state.IsAggressive,
        state.MainHandItemId,
        state.IsUsingItem,
        state.TicksUsingItem,
        state.OffHandItemId,
        state.MainHandChargedCrossbow);
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

/// <summary>Per-tick context delivered to animation states that need to advance their own clocks.</summary>
/// <param name="GameTime">Client game time in ticks, the same clock the frame's age uses.</param>
/// <param name="HorizontalDistance">Horizontal distance moved during the tick (blocks).</param>
/// <param name="IsInWater">Whether the entity's feet block is water this tick.</param>
public readonly record struct EntityAnimationTickContext(int GameTime, float HorizontalDistance, bool IsInWater = false, int MainHandItemId = -1);

/// <summary>
/// Optional capability for states whose clips are triggered by a server <c>entity_event</c> packet. The tracker calls
/// this when a clientbound entity event arrives; unknown ids can be ignored.
/// </summary>
public interface IEntityEventAnimationState
{
    /// <summary>Handles one entity event (see the reference <c>EntityEvent</c> constants).</summary>
    void OnEntityEvent(int eventId, int gameTime);
}

/// <summary>
/// Optional capability for states that must advance counters or animation clocks once per tick (for example the
/// Creaking attack/invulnerability timers or the Warden tendril countdown).
/// </summary>
public interface IEntityTickAnimationState
{
    /// <summary>Advances one tick. Called once per tracked entity per tick, before rendering.</summary>
    void OnTick(in EntityAnimationTickContext context);
}

/// <summary>
/// Optional capability for states whose clips are started by the synced entity <c>Pose</c> (entity data index 6):
/// Warden emerge/dig/roar/sniff, for example.
/// </summary>
public interface IEntityPoseAnimationState
{
    /// <summary>Called when the synced pose changes. Ordinals follow the reference <c>Pose</c> enum (STANDING 0).</summary>
    void OnPoseChanged(int poseId, int gameTime);
}

/// <summary>
/// Optional capability for states that need one of their creature's own synced data fields (for example the Creaking
/// <c>CAN_MOVE</c> / <c>IS_TEARING_DOWN</c> booleans). The state interprets the index; unknown indices are ignored.
/// </summary>
public interface IEntitySyncedDataAnimationState
{
    /// <summary>Called for every decoded integer-valued synced data item.</summary>
    void OnSyncedData(int index, int serializerId, int value);
}

/// <summary>
/// Optional capability for providers that derive an arm-pose enum used by the mesh visibility (crossed arms vs the
/// separate humanoid arms). The tracker projects this into the render state.
/// </summary>
public interface IEntityArmPoseAnimationState
{
    /// <summary>The provider's current arm-pose ordinal (provider-defined; 0 must mean "crossed arms").</summary>
    int ArmPose { get; }
}
