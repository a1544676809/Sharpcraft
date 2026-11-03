// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.World;

/// <summary>One tracked entity as last reported by the server.</summary>
public sealed class TrackedEntity
{
    internal TrackedEntity(int id, int typeId, double x, double y, double z, float yaw, float pitch)
    {
        Id = id;
        TypeId = typeId;
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

    /// <summary>Item registry id in <paramref name="slot"/> (0..7), or -1 when empty.</summary>
    public int EquipmentItemAt(int slot) =>
        (uint)slot < (uint)_equipmentItemIds.Length ? _equipmentItemIds[slot] : -1;

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
    int Markings = 0);

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

        if (!EntityVariantSpecs.TryGet(entity.TypeId, out var spec))
        {
            return false;
        }

        var applied = false;
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

            if (entity.LerpSteps <= 0)
            {
                continue;
            }

            var alpha = 1f / entity.LerpSteps;
            entity.X += (entity.TargetX - entity.X) * alpha;
            entity.Y += (entity.TargetY - entity.Y) * alpha;
            entity.Z += (entity.TargetZ - entity.Z) * alpha;
            entity.Yaw = Wrap360(LerpAngle(entity.Yaw, entity.TargetYaw, alpha));
            entity.Pitch += (entity.TargetPitch - entity.Pitch) * alpha;
            entity.LerpSteps--;
        }
    }

    /// <summary>Forgets every tracked entity.</summary>
    public void Clear() => _entities.Clear();

    /// <summary>Looks up an entity.</summary>
    /// <param name="id">Entity id.</param>
    /// <param name="entity">Tracked entity when found.</param>
    public bool TryGet(int id, out TrackedEntity? entity) => _entities.TryGetValue(id, out entity);

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
                IsSleeping: spec.SleepingMask != 0 && (entity.Flags & spec.SleepingMask) != 0,
                HasNectar: spec.NectarMask != 0 && (entity.Flags & spec.NectarMask) != 0,
                IsCold: spec.ColdMask != 0 && (entity.Flags & spec.ColdMask) != 0,
                IsCharging: spec.ChargingMask != 0 && (entity.Flags & spec.ChargingMask) != 0,
                IsHidingInShell: hidingInShell,
                Tint: tint,
                PatternTint: patternTint,
                IsBaby: entity.IsBaby,
                CollarTint: DyeTint(entity.CollarColor >= 0 ? entity.CollarColor : 14),
                HasCollar: spec.CollarAccessorIndex >= 0 && spec.TameMask != 0 && (entity.Flags & spec.TameMask) != 0,
                Markings: entity.Markings));
        }

        return CollectionsMarshal.AsSpan(_renderStates);
    }

    private static bool Relative(int relatives, int bit) => (relatives & (1 << bit)) != 0;

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
