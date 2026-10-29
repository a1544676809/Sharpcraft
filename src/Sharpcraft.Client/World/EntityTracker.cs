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
public readonly record struct EntityRenderState(int Id, int TypeId, float X, float Y, float Z, float Yaw, float Pitch);

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

    private readonly Dictionary<int, TrackedEntity> _entities = [];
    private readonly List<EntityRenderState> _renderStates = [];

    /// <summary>Entities currently tracked.</summary>
    public int Count => _entities.Count;

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
                entity.X += move.DeltaX;
            }

            if (move.DeltaY != 0d)
            {
                entity.Y += move.DeltaY;
            }

            if (move.DeltaZ != 0d)
            {
                entity.Z += move.DeltaZ;
            }
        }

        if (move.HasRotation)
        {
            entity.Yaw = move.Yaw;
            entity.Pitch = move.Pitch;
        }

        entity.OnGround = move.OnGround;
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

        entity.X = Relative(teleport.Relatives, 0) ? entity.X + teleport.X : teleport.X;
        entity.Y = Relative(teleport.Relatives, 1) ? entity.Y + teleport.Y : teleport.Y;
        entity.Z = Relative(teleport.Relatives, 2) ? entity.Z + teleport.Z : teleport.Z;
        entity.Yaw = Relative(teleport.Relatives, 3) ? entity.Yaw + teleport.Yaw : teleport.Yaw;
        entity.Pitch = Relative(teleport.Relatives, 4) ? entity.Pitch + teleport.Pitch : teleport.Pitch;

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
            _renderStates.Add(new EntityRenderState(
                entity.Id,
                entity.TypeId,
                (float)(entity.PreviousX + ((entity.X - entity.PreviousX) * t)),
                (float)(entity.PreviousY + ((entity.Y - entity.PreviousY) * t)),
                (float)(entity.PreviousZ + ((entity.Z - entity.PreviousZ) * t)),
                LerpAngle(entity.PreviousYaw, entity.Yaw, t),
                entity.PreviousPitch + ((entity.Pitch - entity.PreviousPitch) * t)));
        }

        return CollectionsMarshal.AsSpan(_renderStates);
    }

    private static bool Relative(int relatives, int bit) => (relatives & (1 << bit)) != 0;

    /// <summary>Interpolates an angle along the shortest arc, so 350° to 10° passes through 0°.</summary>
    private static float LerpAngle(float from, float to, float t)
    {
        var delta = to - from;
        delta -= 360f * MathF.Floor((delta + 180f) / 360f);
        return from + (delta * t);
    }
}
