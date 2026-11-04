// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.World;

/// <summary>One tracked entity as last reported by the server.</summary>
public sealed class TrackedEntity
{
    internal TrackedEntity(int id, int typeId, double x, double y, double z, float yaw, float pitch)
    {
        Id = id;
        TypeId = typeId;
        Presentation = new EntityPresentationState(typeId);
        X = x;
        Y = y;
        Z = z;
        PreviousX = x;
        PreviousY = y;
        PreviousZ = z;
        Yaw = yaw;
        Pitch = pitch;
        PreviousYaw = yaw;
        PreviousPitch = pitch;
        HeadYaw = yaw;
        TargetX = x;
        TargetY = y;
        TargetZ = z;
        TargetYaw = yaw;
        TargetPitch = pitch;
    }

    /// <summary>Runtime entity id.</summary>
    public int Id { get; }

    /// <summary>Entity type registry id.</summary>
    public int TypeId { get; }

    /// <summary>Persistent identity, when the server sent one.</summary>
    public Guid Uuid { get; internal set; }

    /// <summary>
    /// Per-entity animation state, created once when the entity is first tracked and dropped with it. Null when the
    /// type has no animation provider or no state factory was injected.
    /// </summary>
    public IEntityAnimationState? Animation { get; internal set; }

    /// <summary>Client-side presentation state (walk/flap/tentacle/ground), advanced once per tick; never authoritative.</summary>
    public EntityPresentationState Presentation { get; }

    /// <summary>Feet position, current tick.</summary>
    public double X { get; internal set; }

    /// <summary>Feet y, current tick.</summary>
    public double Y { get; internal set; }

    /// <summary>Feet z, current tick.</summary>
    public double Z { get; internal set; }

    /// <summary>Feet position at the previous tick, used for interpolation.</summary>
    public double PreviousX { get; internal set; }

    /// <summary>Previous y.</summary>
    public double PreviousY { get; internal set; }

    /// <summary>Previous z.</summary>
    public double PreviousZ { get; internal set; }

    /// <summary>Yaw in degrees.</summary>
    public float Yaw { get; internal set; }

    /// <summary>Pitch in degrees.</summary>
    public float Pitch { get; internal set; }

    /// <summary>Head yaw in degrees, driven by the separate rotate-head packet.</summary>
    public float HeadYaw { get; internal set; }

    /// <summary>Eye height above the feet from the vanilla entity-type dimensions table (pose/age aware); 0 when unknown.</summary>
    public float EyeHeight => (EntityDimensionsRegistry.Get(TypeId, Presentation.Pose, IsBaby)?.EyeHeight ?? 0f) * Presentation.Scale;

    /// <summary>World-space eye Y (feet y + eye height).</summary>
    public float EyeY => (float)Y + EyeHeight;

    /// <summary>Yaw at the previous tick.</summary>
    public float PreviousYaw { get; internal set; }

    /// <summary>Pitch at the previous tick.</summary>
    public float PreviousPitch { get; internal set; }

    /// <summary>Velocity in blocks per tick.</summary>
    public double VelocityX { get; internal set; }

    /// <summary>Velocity y.</summary>
    public double VelocityY { get; internal set; }

    /// <summary>Velocity z.</summary>
    public double VelocityZ { get; internal set; }

    /// <summary>Whether the entity reports standing on ground.</summary>
    public bool OnGround { get; internal set; }

    /// <summary>
    /// Raw appearance variant from synced entity data (registry index for registry-backed types, enum id for fixed
    /// ones). Zero means "the entity's default variant"; see docs/analysis/render/entity-variants.md.
    /// </summary>
    public int VariantId { get; internal set; }

    /// <summary>Raw byte flag word from synced entity data (tame/nectar/sleeping bits).</summary>
    public int Flags { get; internal set; }

    /// <summary>Anger end time in game ticks, or long.MinValue when unknown.</summary>
    public long AngerEndTime { get; internal set; } = long.MinValue;

    /// <summary>Game tick at which the variant/state value last changed (for states that depend on elapsed time).</summary>
    public long StateStartTick { get; internal set; }

    /// <summary>True when the entity reports the AgeableMob baby flag (synced data index 16).</summary>
    public bool IsBaby { get; internal set; }

    /// <summary>Raw dye id of a tamed cat/wolf collar, or -1 when unknown.</summary>
    public int CollarColor { get; internal set; } = -1;

    /// <summary>Raw horse markings id (0 none, 1 white, 2 white field, 3 white dots, 4 black dots).</summary>
    public int Markings { get; internal set; }

    private readonly int[] _equipmentItemIds = [-1, -1, -1, -1, -1, -1, -1, -1];
    private readonly int[] _equipmentCounts = new int[8];
    private readonly ItemComponentInfo[] _equipmentComponents = new ItemComponentInfo[8];

    /// <summary>Item registry id in <paramref name="slot"/> (0..7), or -1 when empty.</summary>
    public int EquipmentItemAt(int slot) =>
        (uint)slot < (uint)_equipmentItemIds.Length ? _equipmentItemIds[slot] : -1;

    /// <summary>Decoded component subset for the item in <paramref name="slot"/>; default when empty.</summary>
    public ItemComponentInfo EquipmentComponentsAt(int slot) =>
        (uint)slot < (uint)_equipmentComponents.Length ? _equipmentComponents[slot] : default;

    /// <summary>Stack count in <paramref name="slot"/>; 0 when empty.</summary>
    public int EquipmentCountAt(int slot) =>
        (uint)slot < (uint)_equipmentCounts.Length ? _equipmentCounts[slot] : 0;

    public void ApplyEquipment(ReadOnlySpan<EquipmentItem> items)
    {
        foreach (var item in items)
        {
            if ((uint)item.Slot >= (uint)_equipmentItemIds.Length)
            {
                continue;
            }

            _equipmentItemIds[item.Slot] = item.IsEmpty ? -1 : item.ItemId;
            _equipmentCounts[item.Slot] = item.IsEmpty ? 0 : item.Count;
            _equipmentComponents[item.Slot] = item.IsEmpty ? default : item.Components;
            EquipmentUpdates++;
        }
    }

    /// <summary>Number of equipment slot updates applied, for diagnostics.</summary>
    public int EquipmentUpdates { get; private set; }

    /// <summary>
    /// Authoritative target position. Vanilla does not snap a remote entity to the position the server reports:
    /// it approaches it over a few ticks, which is why the rendered entity trails slightly behind the
    /// authoritative one (docs/specs/network/entity-sync.md 9.4).
    /// </summary>
    public double TargetX { get; internal set; }

    /// <summary>Target y.</summary>
    public double TargetY { get; internal set; }

    /// <summary>Target z.</summary>
    public double TargetZ { get; internal set; }

    /// <summary>Target yaw in degrees.</summary>
    public float TargetYaw { get; internal set; }

    /// <summary>Target pitch in degrees.</summary>
    public float TargetPitch { get; internal set; }

    /// <summary>Ticks of interpolation left before the entity sits on its target.</summary>
    public int LerpSteps { get; internal set; }

    /// <summary>Copies the current state into the previous one, so a later update interpolates from here.</summary>
    internal void PushPrevious()
    {
        PreviousX = X;
        PreviousY = Y;
        PreviousZ = Z;
        PreviousYaw = Yaw;
        PreviousPitch = Pitch;
    }
}

/// <summary>What the renderer needs for one entity this frame.</summary>
/// <param name="Id">Runtime entity id.</param>
/// <param name="TypeId">Entity type registry id.</param>
/// <param name="X">Interpolated feet x.</param>
/// <param name="Y">Interpolated feet y.</param>
/// <param name="Z">Interpolated feet z.</param>
/// <param name="Yaw">Interpolated yaw in degrees.</param>
/// <param name="Pitch">Interpolated pitch in degrees.</param>
/// <param name="ShowHat">Whether the player hat overlay is visible.</param>
/// <param name="ShowJacket">Whether the player jacket overlay is visible.</param>
/// <param name="ShowLeftSleeve">Whether the left sleeve overlay is visible.</param>
/// <param name="ShowRightSleeve">Whether the right sleeve overlay is visible.</param>
/// <param name="ShowLeftPants">Whether the left pants overlay is visible.</param>
/// <param name="ShowRightPants">Whether the right pants overlay is visible.</param>
public readonly record struct EntityRenderState(
    int Id,
    int TypeId,
    float X,
    float Y,
    float Z,
    float Yaw,
    float Pitch,
    bool ShowHat = true,
    bool ShowJacket = true,
    bool ShowLeftSleeve = true,
    bool ShowRightSleeve = true,
    bool ShowLeftPants = true,
    bool ShowRightPants = true,
    bool HasChest = false,
    bool HasEgg = false,
    int VariantId = 0,
    bool IsTame = false,
    bool IsAngry = false,
    bool IsSleeping = false,
    bool HasNectar = false,
    bool IsCold = false,
    bool IsCharging = false,
    bool IsHidingInShell = false,
    System.Numerics.Vector3 Tint = default,
    System.Numerics.Vector3 PatternTint = default,
    bool IsBaby = false,
    System.Numerics.Vector3 CollarTint = default,
    bool HasCollar = false,
    int Markings = 0,
    EntityEquipment Equipment = default,
    float AgeInTicks = 0f,
    float WalkAnimationPos = 0f,
    float WalkAnimationSpeed = 0f,
    float Flap = 0f,
    float FlapSpeed = 0f,
    float TentacleAngle = 0f,
    bool IsResting = false,
    IEntityAnimationState? Animation = null,
    float AnimationPartialTick = 0f,
    bool HasMainHandItem = false,
    bool HasOffHandItem = false,
    bool IsOnGround = false,
    bool IsInWater = false,
    bool IsPassenger = false,
    bool IsVehicle = false,
    bool IsSitting = false,
    bool IsMoving = false,
    float HeadYaw = 0f,
    float SwingAnimation = 0f,
    bool IsAggressive = false,
    int MainHandItemId = -1,
    int ArmPose = 0,
    bool IsUsingItem = false,
    int TicksUsingItem = 0,
    int OffHandItemId = -1,
    bool MainHandChargedCrossbow = false,
    int MainHandQuickChargeLevel = 0,
    bool IsLeftHanded = false,
    float Squish = 0f,
    int CubeSize = 1,
    int AttachFace = -1,
    bool IsCrouching = false,
    bool IsPouncing = false,
    bool IsFaceplanted = false,
    float Health = 0f,
    int UnhappyCounter = 0,
    int SneezeCounter = 0,
    bool IsStanding = false,
    float JumpCompletion = 0f,
    bool IsJumping = false,
    float StandScale = 0f,
    bool IsEating = false,
    bool IsRolling = false,
    bool IsOnBack = false,
    float SitAmount = 0f,
    float LieOnBackAmount = 0f,
    float RollAmount = 0f,
    bool IsPartyParrot = false,
    bool IsWorried = false,
    bool IsThundering = false,
    float WitherHeadYaw1 = 0f,
    float WitherHeadPitch1 = 0f,
    float WitherHeadYaw2 = 0f,
    float WitherHeadPitch2 = 0f,
    bool IsIdleHeadTiltStarted = false,
    float IdleHeadTiltAgeTicks = 0f,
    float Scale = 1f);

/// <summary>
/// Keeps the entities the server told us about (docs/specs/network/entity-sync.md 5).
/// </summary>
/// <remarks>
/// The server is authoritative: movement arrives as either a delta or an absolute teleport, and removals are
/// explicit. Updates for ids we never saw are normal around a reconnect and are counted instead of thrown.
/// <para>
/// The interpolation buffer is reused, so a steady frame allocates nothing; that is asserted by a test because
/// the interface path is a hot path under principle 3.6.
/// </para>
/// </remarks>
public sealed class EntityTracker
{
    /// <summary>Upper bound on tracked entities; a runaway server must not grow this dictionary without limit.</summary>
    public const int MaxTrackedEntities = 4096;

    /// <summary>
    /// Ticks a remote entity takes to reach the position the server reported. Vanilla passes three to
    /// <c>lerpPositionAndRotationStep</c> for remote movement
    /// (docs/specs/network/entity-sync.md 9.4).
    /// </summary>
    public const int InterpolationSteps = 3;

    private readonly Dictionary<int, TrackedEntity> _entities = [];
    private readonly List<EntityRenderState> _renderStates = [];

    /// <summary>
    /// Injected factory that creates a per-entity animation state for a type id, or null when the type has no
    /// animation. The host wires this to <c>EntityAnimationRegistry.CreateState</c> so the world layer stays free of
    /// any rendering dependency; tests that do not care about animation leave it null.
    /// </summary>
    public Func<int, IEntityAnimationState?>? AnimationStateFactory { get; set; }

    /// <summary>
    /// Optional read-only world used by the presentation layer for local queries (currently <c>isInWater</c>). Set by
    /// the host; null means "unknown", and the derived flag stays false.
    /// </summary>
    public ClientWorld? World { get; set; }

    // Passenger graph (from set_passengers). Low-frequency updates; arrays are fine.
    private readonly Dictionary<int, int[]> _passengers = [];
    private readonly Dictionary<int, int> _vehicleOf = [];

    /// <summary>Entities currently tracked.</summary>
    public int Count => _entities.Count;

    /// <summary>Looks up the persistent identity for a runtime entity id, when the server supplied one.</summary>
    public bool TryGetUuid(int id, out Guid uuid)
    {
        if (_entities.TryGetValue(id, out var entity) && entity.Uuid != Guid.Empty)
        {
            uuid = entity.Uuid;
            return true;
        }

        uuid = Guid.Empty;
        return false;
    }

    /// <summary>Updates whose entity id was unknown; expected around reconnects.</summary>
    public int UnknownUpdates { get; private set; }

    /// <summary>Additions rejected because the table was full.</summary>
    public int OverflowDrops { get; private set; }

    /// <summary>Movement updates applied.</summary>
    public int MovesApplied { get; private set; }

    /// <summary>Teleports applied.</summary>
    public int TeleportsApplied { get; private set; }

    /// <summary>Entities removed by the server.</summary>
    public int Removals { get; private set; }

    /// <summary>Variant values applied from synced entity data.</summary>
    public int VariantUpdates { get; private set; }

    /// <summary>Introduces or replaces an entity.</summary>
    /// <param name="added">Parsed add-entity packet.</param>
    /// <returns>True when the entity is now tracked.</returns>
    public bool Add(AddedEntity added)
    {
        ArgumentNullException.ThrowIfNull(added);

        if (!_entities.TryGetValue(added.Id, out var entity))
        {
            if (_entities.Count >= MaxTrackedEntities)
            {
                OverflowDrops++;
                return false;
            }

            entity = new TrackedEntity(
                added.Id,
                added.TypeId,
                added.X,
                added.Y,
                added.Z,
                added.Yaw,
                added.Pitch);
            entity.Animation = AnimationStateFactory?.Invoke(added.TypeId);
            _entities[added.Id] = entity;
        }

        entity.Uuid = added.Uuid;
        entity.PushPrevious();
        entity.X = added.X;
        entity.Y = added.Y;
        entity.Z = added.Z;
        entity.Yaw = added.Yaw;
        entity.Pitch = added.Pitch;
        entity.HeadYaw = added.HeadYaw;
        entity.TargetX = added.X;
        entity.TargetY = added.Y;
        entity.TargetZ = added.Z;
        entity.TargetYaw = added.Yaw;
        entity.TargetPitch = added.Pitch;
        entity.LerpSteps = 0;
        entity.VelocityX = added.VelocityX;
        entity.VelocityY = added.VelocityY;
        entity.VelocityZ = added.VelocityZ;
        return true;
    }

    /// <summary>Applies a delta movement or a rotation-only update.</summary>
    /// <param name="move">Parsed movement.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyMove(in EntityMove move)
    {
        if (!_entities.TryGetValue(move.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.PushPrevious();

        if (move.HasPosition)
        {
            // Zero is "keep this axis", which is what the wire format means rather than "moved by nothing".
            if (move.DeltaX != 0d)
            {
                entity.TargetX += move.DeltaX;
            }

            if (move.DeltaY != 0d)
            {
                entity.TargetY += move.DeltaY;
            }

            if (move.DeltaZ != 0d)
            {
                entity.TargetZ += move.DeltaZ;
            }
        }

        if (move.HasRotation)
        {
            entity.TargetYaw = move.Yaw;
            entity.TargetPitch = move.Pitch;
        }

        entity.OnGround = move.OnGround;
        entity.LerpSteps = InterpolationSteps;
        MovesApplied++;
        return true;
    }

    /// <summary>Applies an absolute repositioning, honouring the per-component relative flags.</summary>
    /// <param name="teleport">Parsed teleport.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyTeleport(in EntityTeleport teleport)
    {
        if (!_entities.TryGetValue(teleport.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.PushPrevious();

        entity.TargetX = Relative(teleport.Relatives, 0) ? entity.TargetX + teleport.X : teleport.X;
        entity.TargetY = Relative(teleport.Relatives, 1) ? entity.TargetY + teleport.Y : teleport.Y;
        entity.TargetZ = Relative(teleport.Relatives, 2) ? entity.TargetZ + teleport.Z : teleport.Z;
        entity.TargetYaw = Wrap360(Relative(teleport.Relatives, 3) ? entity.TargetYaw + teleport.Yaw : teleport.Yaw);
        entity.TargetPitch = Relative(teleport.Relatives, 4) ? entity.TargetPitch + teleport.Pitch : teleport.Pitch;
        entity.LerpSteps = InterpolationSteps;

        entity.VelocityX = teleport.VelocityX;
        entity.VelocityY = teleport.VelocityY;
        entity.VelocityZ = teleport.VelocityZ;
        entity.OnGround = teleport.OnGround;
        TeleportsApplied++;
        return true;
    }

    /// <summary>Applies a head rotation.</summary>
    /// <param name="id">Entity id.</param>
    /// <param name="headYaw">Head yaw in degrees.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyHeadYaw(int id, float headYaw)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.HeadYaw = headYaw;
        return true;
    }

    /// <summary>Applies an absolute velocity.</summary>
    /// <param name="motion">Parsed velocity.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyMotion(in EntityMotion motion)
    {
        if (!_entities.TryGetValue(motion.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.VelocityX = motion.X;
        entity.VelocityY = motion.Y;
        entity.VelocityZ = motion.Z;
        return true;
    }

    /// <summary>
    /// Applies the variant field from a <c>set_entity_data</c> packet, if this entity's type has one.
    /// </summary>
    /// <summary>Applies one set_equipment update to the tracked entity with <paramref name="id"/>.</summary>
    public bool ApplyEquipment(int id, ReadOnlySpan<EquipmentItem> items)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            return false;
        }

        entity.ApplyEquipment(items);
        return true;
    }

    /// <summary>Routes one clientbound <c>entity_event</c> (packet 34) to the entity's animation state, if it listens.</summary>
    /// <param name="id">Runtime entity id.</param>
    /// <param name="eventId">Event id (reference <c>EntityEvent</c> constant).</param>
    /// <returns>True when the entity was tracked.</returns>
    public bool ApplyEntityEvent(int id, int eventId)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        if (entity.Animation is IEntityEventAnimationState listener)
        {
            listener.OnEntityEvent(eventId, (int)GameTime);
        }

        entity.Presentation.Tentacle?.OnEntityEvent(eventId);
        entity.Presentation.RabbitHop?.OnEntityEvent(eventId);
        return true;
    }

    /// <summary>Applies the SCALE attribute value from an <c>update_attributes</c> packet.</summary>
    public bool ApplyAttributes(int id, float scale)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Presentation.Scale = scale;
        return true;
    }

    /// <summary>Applies a <c>set_entity_link</c> packet (leash attach/detach) to the source entity.</summary>
    public bool ApplyEntityLink(int sourceId, int destinationId)
    {
        if (!_entities.TryGetValue(sourceId, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Presentation.IsLeashed = destinationId != 0;
        return true;
    }

    /// <summary>Applies a jukebox level event (1010 start / 1011 stop) to nearby parrots.</summary>
    public bool ApplyLevelEvent(int type, long packedPosition, int data)
    {
        if (type is not (1010 or 1011))
        {
            return false;
        }

        var (bx, by, bz) = BlockPositionCodec.UnpackBlockPosition(packedPosition);
        var play = type == 1010;
        foreach (var entity in _entities.Values)
        {
            if (entity.TypeId != 99)
            {
                continue;
            }

            // Reference AABB(blockPos).inflate(3.0) around the block, then the per-tick 3.46 center check.
            if (entity.X >= bx - 3 && entity.X <= bx + 4 &&
                entity.Y >= by - 3 && entity.Y <= by + 4 &&
                entity.Z >= bz - 3 && entity.Z <= bz + 4)
            {
                entity.Presentation.IsPartyParrot = play;
                entity.Presentation.PartyJukebox = play ? packedPosition : long.MinValue;
            }
        }

        return true;
    }

    /// <summary>Applies a <c>swing_animation</c> packet (clientbound 123) to the entity's arm-swing clock.</summary>
    public bool ApplySwingAnimation(int id, int hand, int animationType, int durationTicks)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Presentation.Swing.StartIfAble(hand, animationType, durationTicks);
        return true;
    }

    /// <summary>Applies a <c>set_passengers</c> update (the vehicle's new passenger list).</summary>
    /// <param name="vehicleId">Vehicle entity id.</param>
    /// <param name="passengers">Passenger entity ids (empty clears the list).</param>
    public bool ApplyPassengers(int vehicleId, ReadOnlySpan<int> passengers)
    {
        if (_passengers.TryGetValue(vehicleId, out var old))
        {
            foreach (var previous in old)
            {
                _vehicleOf.Remove(previous);
            }
        }

        if (passengers.Length == 0)
        {
            _passengers.Remove(vehicleId);
            return true;
        }

        var copy = passengers.ToArray();
        _passengers[vehicleId] = copy;
        foreach (var passenger in copy)
        {
            _vehicleOf[passenger] = vehicleId;
        }

        return true;
    }

    /// <param name="id">Entity id the packet named.</param>
    /// <param name="items">Decoded data items.</param>
    /// <returns>True when a variant value was found and stored.</returns>
    public bool ApplyEntityData(int id, ReadOnlySpan<EntityDataItem> items)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        var applied = HandleAnimationData(entity, items);

        if (!EntityVariantSpecs.TryGet(entity.TypeId, out var spec))
        {
            return applied;
        }

        foreach (var item in items)
        {
            if (spec.AccessorIndex >= 0 && item.Index == spec.AccessorIndex && item.Value != EntityDataItem.NotAnInteger)
            {
                var variantValue = item.Value & spec.ValueMask;
                if (variantValue != entity.VariantId)
                {
                    entity.StateStartTick = GameTime;
                }

                entity.VariantId = variantValue;
                if (spec.MarkingsShift >= 0)
                {
                    entity.Markings = (item.Value >> spec.MarkingsShift) & 0xFF;
                }

                VariantUpdates++;
                applied = true;
            }
            else if (spec.FlagsAccessorIndex >= 0 && item.Index == spec.FlagsAccessorIndex && item.Value != EntityDataItem.NotAnInteger)
            {
                entity.Flags = item.Value;
                applied = true;
            }
            else if (spec.AngerAccessorIndex >= 0 && item.Index == spec.AngerAccessorIndex && item.LongValue != EntityDataItem.NotALong)
            {
                entity.AngerEndTime = item.LongValue;
                applied = true;
            }
            else if (item.Index == 16 && item.SerializerId == 8 && AgeableTypes.Contains(entity.TypeId))
            {
                entity.IsBaby = item.Value != 0;
                applied = true;
            }
            else if (spec.CollarAccessorIndex >= 0 && item.Index == spec.CollarAccessorIndex && item.Value != EntityDataItem.NotAnInteger)
            {
                entity.CollarColor = item.Value;
                applied = true;
            }
        }

        return applied;
    }

    /// <summary>Game time used to decide whether an anger end time is still in the future.</summary>
    public long GameTime { get; set; }

    /// <summary>Removes entities the server dropped.</summary>
    /// <param name="ids">Entity ids.</param>
    /// <returns>How many were actually tracked.</returns>
    public int Remove(ReadOnlySpan<int> ids)
    {
        var removed = 0;
        foreach (var id in ids)
        {
            if (_entities.Remove(id))
            {
                removed++;
            }
        }

        Removals += removed;
        return removed;
    }

    /// <summary>
    /// Advances every entity one tick towards its target, the way vanilla moves a remote entity: a fraction
    /// 1/steps of the remaining distance per tick, so the rendered entity trails the authoritative one by a few
    /// ticks instead of jumping (docs/specs/network/entity-sync.md 9.4).
    /// </summary>
    public void Tick()
    {
        foreach (var entity in _entities.Values)
        {
            entity.PushPrevious();

            if (entity.LerpSteps > 0)
            {
                var alpha = 1f / entity.LerpSteps;
                entity.X += (entity.TargetX - entity.X) * alpha;
                entity.Y += (entity.TargetY - entity.Y) * alpha;
                entity.Z += (entity.TargetZ - entity.Z) * alpha;
                entity.Yaw = Wrap360(LerpAngle(entity.Yaw, entity.TargetYaw, alpha));
                entity.Pitch += (entity.TargetPitch - entity.Pitch) * alpha;
                entity.LerpSteps--;
            }

            // Horizontal distance actually travelled this tick drives the walk animation, mirroring the reference
            // calculateEntityAnimation (uses the movement delta, not the commanded velocity).
            var dx = (float)(entity.X - entity.PreviousX);
            var dz = (float)(entity.Z - entity.PreviousZ);
            var dy = (float)(entity.Y - entity.PreviousY);
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            var profile = WalkProfile.For(entity.TypeId, entity.IsBaby);
            entity.Presentation.Walk.Update(
                MathF.Min(distance * profile.DistanceToSpeed, profile.MaxSpeed),
                profile.Factor,
                profile.PositionScale);

            // Client-side presentation derivations (ADR-0022). Passenger tracking does not exist yet, so the flap
            // clock uses "not on ground" as the airborne signal (documented deviation for ridden parrots).
            entity.Presentation.IsOnGround = entity.OnGround;
            entity.Presentation.Flap.Update(!entity.OnGround);
            entity.Presentation.IsMoving = distance > 3.0E-4f;
            if (entity.TypeId == 120 || entity.TypeId == 81)
            {
                var presentation = entity.Presentation;
                presentation.OldSquish = presentation.Squish;
                presentation.Squish += (presentation.TargetSquish - presentation.Squish) * 0.5f;
                if (presentation.IsOnGround && !presentation.WasOnGround)
                {
                    presentation.TargetSquish = -0.5f;
                }
                else if (!presentation.IsOnGround && presentation.WasOnGround)
                {
                    presentation.TargetSquish = 1f;
                }

                presentation.WasOnGround = presentation.IsOnGround;
            }
            entity.Presentation.IsInWater = World?.IsWaterAt(
                (int)MathF.Floor((float)entity.X),
                (int)MathF.Floor((float)entity.Y),
                (int)MathF.Floor((float)entity.Z)) ?? false;
            entity.Presentation.Tentacle?.Update(entity.Presentation.IsInWater);
            entity.Presentation.RabbitHop?.Tick();
            entity.Presentation.RabbitIdle?.Tick((int)GameTime, entity.Presentation.IsNoAi, entity.Presentation.IsLeashed, entity.Presentation.RabbitHop?.IsJumping ?? false);
            entity.Presentation.PolarStand?.Tick(entity.Presentation.IsStanding);
            entity.Presentation.PandaPose?.Tick(entity.Presentation.IsSitting, entity.Presentation.IsOnBack, entity.Presentation.IsRolling);
            if (entity.TypeId == 99 && entity.Presentation.IsPartyParrot && entity.Presentation.PartyJukebox != long.MinValue)
            {
                var (bx, by, bz) = BlockPositionCodec.UnpackBlockPosition(entity.Presentation.PartyJukebox);
                var jx = entity.X - (bx + 0.5);
                var jy = entity.Y - (by + 0.5);
                var jz = entity.Z - (bz + 0.5);
                if ((jx * jx) + (jy * jy) + (jz * jz) > 3.46 * 3.46)
                {
                    entity.Presentation.IsPartyParrot = false;
                    entity.Presentation.PartyJukebox = long.MinValue;
                }
            }

            entity.Presentation.Swing.Tick();
            if (entity.Presentation.IsUsingItem)
            {
                entity.Presentation.UseItemTicks++;
            }
            else
            {
                entity.Presentation.UseItemTicks = 0;
            }

            if (entity.Animation is IEntityTickAnimationState tickState)
            {
                tickState.OnTick(new EntityAnimationTickContext((int)GameTime, distance, entity.Presentation.IsInWater, entity.EquipmentItemAt(0), dy, entity.Presentation.IsOnGround));
            }
        }

        UpdateWitherHeads();
    }

    private void UpdateWitherHeads()
    {
        Span<WitherHeadAim> aims = stackalloc WitherHeadAim[2];
        Span<int> targets = stackalloc int[2];
        foreach (var entity in _entities.Values)
        {
            if (entity.TypeId != 149 || entity.Presentation.WitherHeads is not { } heads)
            {
                continue;
            }

            targets[0] = entity.Presentation.WitherTarget1;
            targets[1] = entity.Presentation.WitherTarget2;
            for (var i = 0; i < 2; i++)
            {
                aims[i] = default;
                var targetId = targets[i];
                if (targetId <= 0 || !_entities.TryGetValue(targetId, out var target))
                {
                    continue;
                }

                // Reference getHeadX/Y/Z for head index i+1 (side heads): ±1.3 blocks around the body, height 2.2.
                var angle = (entity.Yaw + (180f * (i + 1))) * (MathF.PI / 180f);
                var hx = entity.X + (MathF.Cos(angle) * 1.3);
                var hy = entity.Y + 2.2;
                var hz = entity.Z + (MathF.Sin(angle) * 1.3);
                var dx = target.X - hx;
                var dy = target.EyeY - hy;
                var dz = target.Z - hz;
                var sd = MathF.Sqrt((float)((dx * dx) + (dz * dz)));
                var targetYaw = (float)(Math.Atan2(dz, dx) * 180.0 / Math.PI) - 90f;
                var targetPitch = (float)(-(Math.Atan2(dy, sd) * 180.0 / Math.PI));
                aims[i] = new WitherHeadAim(true, targetYaw, targetPitch);
            }

            heads.Tick(entity.Yaw, aims);
        }
    }

    /// <summary>Forgets every tracked entity.</summary>
    public void Clear() => _entities.Clear();

    /// <summary>Looks up an entity.</summary>
    /// <param name="id">Entity id.</param>
    /// <param name="entity">Tracked entity when found.</param>
    public bool TryGet(int id, out TrackedEntity? entity) => _entities.TryGetValue(id, out entity);

    /// <summary>Eye Y of another tracked entity, using the vanilla dimensions table (for target aiming).</summary>
    public bool TryGetEyeY(int id, out float eyeY)
    {
        if (_entities.TryGetValue(id, out var entity))
        {
            eyeY = entity.EyeY;
            return true;
        }

        eyeY = 0f;
        return false;
    }

    /// <summary>
    /// Builds the per-frame render states, interpolating between the previous and current tick. The returned span
    /// is valid until the next call and is reused, so a steady frame allocates nothing.
    /// </summary>
    /// <param name="alpha">Fraction into the current tick, 0..1.</param>
    public ReadOnlySpan<EntityRenderState> Interpolate(float alpha)
    {
        var t = Math.Clamp(alpha, 0f, 1f);
        _renderStates.Clear();

        foreach (var entity in _entities.Values)
        {
            EntityVariantSpecs.TryGet(entity.TypeId, out var spec);
            var stateTicks = GameTime - entity.StateStartTick;
            var hidingInShell = spec.ShellHideStates
                && (entity.VariantId == 2
                    || (entity.VariantId == 1 && stateTicks > 5)
                    || (entity.VariantId == 3 && stateTicks < 26));
            var tint = default(System.Numerics.Vector3);
            var patternTint = default(System.Numerics.Vector3);
            if (entity.TypeId == 140)
            {
                tint = DyeTint((entity.VariantId >> 16) & 0xFF);
                patternTint = DyeTint((entity.VariantId >> 24) & 0xFF);
            }

            _renderStates.Add(new EntityRenderState(
                entity.Id,
                entity.TypeId,
                (float)(entity.PreviousX + ((entity.X - entity.PreviousX) * t)),
                (float)(entity.PreviousY + ((entity.Y - entity.PreviousY) * t)),
                (float)(entity.PreviousZ + ((entity.Z - entity.PreviousZ) * t)),
                Wrap360(LerpAngle(entity.PreviousYaw, entity.Yaw, t)),
                entity.PreviousPitch + ((entity.Pitch - entity.PreviousPitch) * t),
                VariantId: entity.VariantId,
                IsTame: spec.TameMask != 0 && (entity.Flags & spec.TameMask) != 0,
                IsAngry: spec.AngerAccessorIndex >= 0 && entity.AngerEndTime > GameTime,
                IsSleeping: (spec.SleepingMask != 0 && (entity.Flags & spec.SleepingMask) != 0) || entity.Presentation.IsSleeping,
                HasNectar: spec.NectarMask != 0 && (entity.Flags & spec.NectarMask) != 0,
                IsCold: spec.ColdMask != 0 && (entity.Flags & spec.ColdMask) != 0,
                IsCharging: spec.ChargingMask != 0 && (entity.Flags & spec.ChargingMask) != 0,
                IsHidingInShell: hidingInShell,
                Tint: tint,
                PatternTint: patternTint,
                IsBaby: entity.IsBaby,
                CollarTint: DyeTint(entity.CollarColor >= 0 ? entity.CollarColor : 14),
                HasCollar: spec.CollarAccessorIndex >= 0 && spec.TameMask != 0 && (entity.Flags & spec.TameMask) != 0,
                Markings: entity.Markings,
                Equipment: new EntityEquipment(
                    entity.EquipmentItemAt(0), entity.EquipmentItemAt(1), entity.EquipmentItemAt(2), entity.EquipmentItemAt(3),
                    entity.EquipmentItemAt(4), entity.EquipmentItemAt(5), entity.EquipmentItemAt(6), entity.EquipmentItemAt(7)),
                AgeInTicks: GameTime,
                WalkAnimationPos: entity.Presentation.Walk.Position(t),
                WalkAnimationSpeed: entity.Presentation.Walk.Speed(t),
                Flap: entity.Presentation.Flap.Flap(t),
                FlapSpeed: entity.Presentation.Flap.Speed(t),
                IsOnGround: entity.Presentation.IsOnGround,
                IsInWater: entity.Presentation.IsInWater,
                IsPassenger: _vehicleOf.ContainsKey(entity.Id),
                IsVehicle: _passengers.ContainsKey(entity.Id),
                IsSitting: entity.Presentation.IsSitting,
                IsMoving: entity.Presentation.IsMoving,
                HeadYaw: entity.HeadYaw,
                SwingAnimation: entity.Presentation.Swing.Animation(t),
                IsAggressive: entity.Presentation.IsAggressive,
                MainHandItemId: entity.EquipmentItemAt(0),
                ArmPose: entity.Animation is IEntityArmPoseAnimationState armPose ? armPose.ArmPose : 0,
                IsUsingItem: entity.Presentation.IsUsingItem,
                TicksUsingItem: entity.Presentation.UseItemTicks,
                OffHandItemId: entity.EquipmentItemAt(1),
                MainHandChargedCrossbow: entity.EquipmentComponentsAt(0).HasChargedProjectiles,
                MainHandQuickChargeLevel: entity.EquipmentComponentsAt(0).QuickChargeLevel,
                IsLeftHanded: entity.Presentation.IsLeftHanded,
                Squish: entity.Presentation.OldSquish + ((entity.Presentation.Squish - entity.Presentation.OldSquish) * t),
                CubeSize: entity.Presentation.CubeSize,
                AttachFace: entity.Presentation.AttachFace,
                IsCrouching: entity.Presentation.IsCrouching,
                IsPouncing: entity.Presentation.IsPouncing,
                IsFaceplanted: entity.Presentation.IsFaceplanted,
                Health: entity.Presentation.Health,
                UnhappyCounter: entity.Presentation.UnhappyCounter,
                SneezeCounter: entity.Presentation.SneezeCounter,
                IsStanding: entity.Presentation.IsStanding,
                JumpCompletion: entity.Presentation.RabbitHop?.Completion(t) ?? 0f,
                IsJumping: entity.Presentation.RabbitHop?.IsJumping ?? false,
                StandScale: entity.Presentation.PolarStand?.Scale(t) ?? 0f,
                IsEating: entity.Presentation.IsEating,
                IsRolling: entity.Presentation.IsRolling,
                IsOnBack: entity.Presentation.IsOnBack,
                SitAmount: entity.Presentation.PandaPose?.SitAmount(t) ?? 0f,
                LieOnBackAmount: entity.Presentation.PandaPose?.LieOnBackAmount(t) ?? 0f,
                RollAmount: entity.Presentation.PandaPose?.RollAmount(t) ?? 0f,
                IsPartyParrot: entity.Presentation.IsPartyParrot,
                IsWorried: entity.Presentation.IsWorried,
                IsThundering: World?.IsThundering ?? false,
                WitherHeadYaw1: entity.Presentation.WitherHeads?.Yaw(0) ?? 0f,
                WitherHeadPitch1: entity.Presentation.WitherHeads?.Pitch(0) ?? 0f,
                WitherHeadYaw2: entity.Presentation.WitherHeads?.Yaw(1) ?? 0f,
                WitherHeadPitch2: entity.Presentation.WitherHeads?.Pitch(1) ?? 0f,
                IsIdleHeadTiltStarted: entity.Presentation.RabbitIdle?.IsStarted ?? false,
                IdleHeadTiltAgeTicks: (entity.Presentation.RabbitIdle?.IsStarted ?? false)
                    ? entity.Presentation.RabbitIdle!.AgeTicks((int)GameTime) + t
                    : 0f,
                Scale: entity.Presentation.Scale,
                TentacleAngle: entity.Presentation.Tentacle?.Angle(t) ?? 0f,
                IsResting: entity.Presentation.IsResting,
                Animation: entity.Animation,
                AnimationPartialTick: t,
                HasMainHandItem: entity.EquipmentItemAt(0) > 0,
                HasOffHandItem: entity.EquipmentItemAt(1) > 0));
        }

        return CollectionsMarshal.AsSpan(_renderStates);
    }

    private static bool Relative(int relatives, int bit) => (relatives & (1 << bit)) != 0;

    // Synced entity data: index 6 is Entity.DATA_POSE (Pose serializer id 20). Values are the reference Pose ordinals.
    private const int PoseAccessorIndex = 6;
    private const int PoseSerializerId = 20;

    /// <summary>Routes synced pose and creature data to the per-entity animation state when it listens.</summary>
    private bool HandleAnimationData(TrackedEntity entity, ReadOnlySpan<EntityDataItem> items)
    {
        var poseListener = entity.Animation as IEntityPoseAnimationState;
        var dataListener = entity.Animation as IEntitySyncedDataAnimationState;

        var applied = false;
        foreach (var item in items)
        {
            if (item.Value == EntityDataItem.NotAnInteger)
            {
                continue;
            }

            if (item.Index == PoseAccessorIndex && item.SerializerId == PoseSerializerId)
            {
                entity.Presentation.Pose = item.Value;
                applied = true;
            }

            if (poseListener is not null && item.Index == PoseAccessorIndex && item.SerializerId == PoseSerializerId)
            {
                poseListener.OnPoseChanged(item.Value, (int)GameTime);
                applied = true;
            }

            if (dataListener is not null)
            {
                dataListener.OnSyncedData(item.Index, item.SerializerId, item.Value);
                applied = true;
            }

            // Bat (type 100) flags byte: index 16, serializer 0, bit 0 = resting.
            if (entity.TypeId == 100 && item.Index == 16 && item.SerializerId == 0)
            {
                entity.Presentation.IsResting = (item.Value & 1) != 0;
                applied = true;
            }

            // TamableAnimal flags byte (index 17, serializer 8): bit 0 = sitting (parrot/cat/ocelot/wolf).
            if (item.Index == 17 && item.SerializerId == 8 && entity.TypeId is 21 or 92 or 99 or 152)
            {
                entity.Presentation.IsSitting = (item.Value & 1) != 0;
                applied = true;
            }

            // Fox flags byte (index 18, byte serializer): sitting/pouncing/sleeping/faceplanted/crouching bits.
            if (item.Index == 18 && item.SerializerId == 0 && entity.TypeId == 55)
            {
                entity.Presentation.IsSitting = (item.Value & 1) != 0;
                entity.Presentation.IsCrouching = (item.Value & 4) != 0;
                entity.Presentation.IsPouncing = (item.Value & 16) != 0;
                entity.Presentation.IsSleeping = (item.Value & 32) != 0;
                entity.Presentation.IsFaceplanted = (item.Value & 64) != 0;
                applied = true;
            }

            // LivingEntity health (index 9, float serializer 3) when the value was decoded.
            if (item.Index == 9 && item.SerializerId == 3 && item.Value != EntityDataItem.NotAnInteger)
            {
                entity.Presentation.Health = BitConverter.Int32BitsToSingle(item.Value);
                applied = true;
            }

            // Panda synced counters (unhappy index 17, sneeze index 18; int serializer 1).
            if (entity.TypeId == 97 && item.SerializerId == 1 && item.Index is 17 or 18)
            {
                if (item.Index == 17)
                {
                    entity.Presentation.UnhappyCounter = item.Value;
                }
                else
                {
                    entity.Presentation.SneezeCounter = item.Value;
                }

                applied = true;
            }

            // Panda EAT_COUNTER (index 19, int serializer 1).
            if (entity.TypeId == 97 && item.Index == 19 && item.SerializerId == 1)
            {
                entity.Presentation.IsEating = item.Value > 0;
                applied = true;
            }

            // Wither side-head alternative targets (indices 17/18, int serializer 1).
            if (entity.TypeId == 149 && item.SerializerId == 1 && item.Index is 17 or 18)
            {
                if (item.Index == 17)
                {
                    entity.Presentation.WitherTarget1 = item.Value;
                }
                else
                {
                    entity.Presentation.WitherTarget2 = item.Value;
                }

                applied = true;
            }

            // Panda main gene byte (index 20): WORRIED id 2 drives isScared under thunder.
            if (entity.TypeId == 97 && item.Index == 20 && item.SerializerId == 0)
            {
                entity.Presentation.IsWorried = item.Value == 2;
                applied = true;
            }

            // Panda flags byte (index 22, byte serializer): bit 4 rolling, bit 8 sitting, bit 16 on-back.
            if (entity.TypeId == 97 && item.Index == 22 && item.SerializerId == 0)
            {
                entity.Presentation.IsRolling = (item.Value & 4) != 0;
                entity.Presentation.IsSitting = (item.Value & 8) != 0;
                entity.Presentation.IsOnBack = (item.Value & 16) != 0;
                applied = true;
            }

            // Polar bear standing flag (index 17, boolean serializer 8).
            if (entity.TypeId == 105 && item.Index == 17 && item.SerializerId == 8)
            {
                entity.Presentation.IsStanding = item.Value != 0;
                applied = true;
            }
            // Mob flags byte (index 15, byte serializer): bit 2 = aggressive, bit 1 = left-handed.
            if (item.Index == 15 && item.SerializerId == 0)
            {
                entity.Presentation.IsNoAi = (item.Value & 1) != 0;
                entity.Presentation.IsAggressive = (item.Value & 4) != 0;
                entity.Presentation.IsLeftHanded = (item.Value & 2) != 0;
                applied = true;
            }

            // Shulker attach face: DATA_ATTACH_FACE_ID (index 16, direction serializer 12).
            if (entity.TypeId == 115 && item.Index == 16 && item.SerializerId == 12)
            {
                entity.Presentation.AttachFace = item.Value;
                applied = true;
            }

            // Cube-mob size: AbstractCubeMob.ID_SIZE is the first int accessor after Mob flags (index 16, serializer 2).
            if (item.Index == 16 && item.SerializerId == 2 && (entity.TypeId == 120 || entity.TypeId == 81))
            {
                entity.Presentation.CubeSize = item.Value;
                applied = true;
            }

            // Living-entity flags byte (index 8, byte serializer): bit 0 = using item.
            if (item.Index == 8 && item.SerializerId == 0)
            {
                entity.Presentation.IsUsingItem = (item.Value & 1) != 0;
                applied = true;
            }
        }

        return applied;
    }

    private readonly record struct WalkAnimationProfile(float DistanceToSpeed, float MaxSpeed, float Factor, float PositionScale);

    /// <summary>
    /// Per-type walk animation mapping. The default mirrors <c>LivingEntity.updateWalkAnimation</c>
    /// (target = min(distance*4, 1), factor 0.4, baby position scale 3); the Creaking overrides it with
    /// min(distance*25, 3). Type ids are literals on purpose so this layer does not depend on the render model classes.
    /// </summary>
    private static class WalkProfile
    {
        private const int CreakingTypeId = 31;

        public static WalkAnimationProfile For(int typeId, bool isBaby)
        {
            if (typeId == CreakingTypeId)
            {
                return new WalkAnimationProfile(25f, 3f, 0.4f, 1f);
            }

            return new WalkAnimationProfile(4f, 1f, 0.4f, isBaby ? 3f : 1f);
        }
    }

    private static readonly int[] DyeRgb =
    [
        16383998, 16351261, 13061821, 3847130, 16701501, 8439583, 15961002, 4673362,
        10329495, 1481884, 8991416, 3949738, 8606770, 6192150, 11546150, 1908001
    ];

    /// <summary>Entity types whose synced data index 16 is the AgeableMob baby flag.</summary>
    private static readonly HashSet<int> AgeableTypes =
    [
        4, 7, 11, 19, 21, 26, 30, 36, 59, 65, 67, 79, 89, 101, 102, 103, 111, 122,
        132, 141, 143, 145, 152, 153, 156, 158
    ];

    private static System.Numerics.Vector3 DyeTint(int dyeId)
    {
        var rgb = DyeRgb[Math.Clamp(dyeId, 0, 15)];
        return new System.Numerics.Vector3(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }

    /// <summary>Normalises an angle into [0, 360).</summary>
    private static float Wrap360(float degrees)
    {
        var wrapped = degrees % 360f;
        return wrapped < 0f ? wrapped + 360f : wrapped;
    }

    /// <summary>Interpolates an angle along the shortest arc, so 350° to 10° passes through 0°.</summary>
    private static float LerpAngle(float from, float to, float t)
    {
        var delta = to - from;
        delta -= 360f * MathF.Floor((delta + 180f) / 360f);
        return from + (delta * t);
    }
}



