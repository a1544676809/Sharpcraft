// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>An entity the server introduced, as described by <c>add_entity</c>.</summary>
/// <param name="Id">Runtime entity id.</param>
/// <param name="Uuid">Persistent identity.</param>
/// <param name="TypeId">Entity type registry id.</param>
/// <param name="X">Feet x.</param>
/// <param name="Y">Feet y.</param>
/// <param name="Z">Feet z.</param>
/// <param name="VelocityX">Initial velocity x, blocks per tick.</param>
/// <param name="VelocityY">Initial velocity y.</param>
/// <param name="VelocityZ">Initial velocity z.</param>
/// <param name="Pitch">Pitch in degrees.</param>
/// <param name="Yaw">Yaw in degrees.</param>
/// <param name="HeadYaw">Head yaw in degrees.</param>
/// <param name="Data">Type specific payload, such as a projectile's owner.</param>
public sealed record AddedEntity(
    int Id,
    Guid Uuid,
    int TypeId,
    double X,
    double Y,
    double Z,
    float VelocityX,
    float VelocityY,
    float VelocityZ,
    float Pitch,
    float Yaw,
    float HeadYaw,
    int Data);

/// <summary>One movement update for an existing entity.</summary>
/// <param name="Id">Runtime entity id.</param>
/// <param name="DeltaX">Blocks moved on x; 0 means "unchanged" on this axis, as the wire format defines.</param>
/// <param name="DeltaY">Blocks moved on y.</param>
/// <param name="DeltaZ">Blocks moved on z.</param>
/// <param name="HasPosition">True when the packet carried a position delta.</param>
/// <param name="Yaw">Yaw in degrees, when present.</param>
/// <param name="Pitch">Pitch in degrees, when present.</param>
/// <param name="HasRotation">True when the packet carried a rotation.</param>
/// <param name="OnGround">Whether the entity reports standing on ground.</param>
public readonly record struct EntityMove(
    int Id,
    double DeltaX,
    double DeltaY,
    double DeltaZ,
    bool HasPosition,
    float Yaw,
    float Pitch,
    bool HasRotation,
    bool OnGround);

/// <summary>An absolute repositioning of an entity.</summary>
/// <param name="Id">Runtime entity id.</param>
/// <param name="X">Absolute x.</param>
/// <param name="Y">Absolute y.</param>
/// <param name="Z">Absolute z.</param>
/// <param name="VelocityX">Velocity x after the teleport.</param>
/// <param name="VelocityY">Velocity y.</param>
/// <param name="VelocityZ">Velocity z.</param>
/// <param name="Yaw">Yaw in degrees.</param>
/// <param name="Pitch">Pitch in degrees.</param>
/// <param name="Relatives">Raw relative flags, applied by the client.</param>
/// <param name="OnGround">Whether the entity reports standing on ground.</param>
public readonly record struct EntityTeleport(
    int Id,
    double X,
    double Y,
    double Z,
    double VelocityX,
    double VelocityY,
    double VelocityZ,
    float Yaw,
    float Pitch,
    int Relatives,
    bool OnGround);

/// <summary>An absolute velocity assignment.</summary>
/// <param name="Id">Runtime entity id.</param>
/// <param name="X">Velocity x, blocks per tick.</param>
/// <param name="Y">Velocity y.</param>
/// <param name="Z">Velocity z.</param>
public readonly record struct EntityMotion(int Id, float X, float Y, float Z);

/// <summary>
/// Parsers for the entity packets (docs/specs/network/entity-sync.md 3). 26.3 differs from older releases in
/// ways that silently corrupt every following field if they are guessed: three angle bytes on add, a compressed
/// velocity vector, and a packed properties word plus a stepped delta on movement.
/// </summary>
public static class EntityPackets
{
    /// <summary>Reads an <c>add_entity</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="entity">Parsed entity when successful.</param>
    public static DecodeStatus TryReadAddEntity(ReadOnlySpan<byte> body, out AddedEntity? entity)
    {
        entity = null;
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success ||
            reader.TryReadUuid(out var uuid) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var typeId) != DecodeStatus.Success ||
            reader.TryReadDouble(out var x) != DecodeStatus.Success ||
            reader.TryReadDouble(out var y) != DecodeStatus.Success ||
            reader.TryReadDouble(out var z) != DecodeStatus.Success ||
            !TryReadLpVec3(ref reader, out var vx, out var vy, out var vz) ||
            reader.TryReadByte(out var pitch) != DecodeStatus.Success ||
            reader.TryReadByte(out var yaw) != DecodeStatus.Success ||
            reader.TryReadByte(out var headYaw) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var data) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        entity = new AddedEntity(
            id,
            uuid,
            typeId,
            x,
            y,
            z,
            vx,
            vy,
            vz,
            AngleFromByte(pitch),
            AngleFromByte(yaw),
            AngleFromByte(headYaw),
            data);
        return DecodeStatus.Success;
    }

    /// <summary>Reads a <c>remove_entities</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="ids">Removed entity ids.</param>
    public static DecodeStatus TryReadRemoveEntities(ReadOnlySpan<byte> body, out int[] ids)
    {
        ids = [];
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (count < 0 || count > 4096)
        {
            return DecodeStatus.Invalid;
        }

        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            if (reader.TryReadVarInt(out result[index]) != DecodeStatus.Success)
            {
                return DecodeStatus.NeedMoreData;
            }
        }

        ids = result;
        return DecodeStatus.Success;
    }

    /// <summary>
    /// Reads one of the three movement bodies. They share the id and the position delta but differ in the tail:
    /// the position variant stops after the delta, the combined variant adds two angle bytes, and the rotation
    /// only variant replaces the packed properties with a plain boolean.
    /// </summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="hasPosition">True for the position and position-rotation variants.</param>
    /// <param name="hasRotation">True for the rotation and position-rotation variants.</param>
    /// <param name="move">Parsed update when successful.</param>
    public static DecodeStatus TryReadMoveEntity(
        ReadOnlySpan<byte> body,
        bool hasPosition,
        bool hasRotation,
        out EntityMove move)
    {
        move = default;
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        var onGround = false;
        var deltaX = 0d;
        var deltaY = 0d;
        var deltaZ = 0d;

        if (hasPosition)
        {
            if (reader.TryReadVarInt(out var properties) != DecodeStatus.Success)
            {
                return DecodeStatus.NeedMoreData;
            }

            onGround = (properties & 1) != 0;
            var stepCount = properties >>> 1;

            if (!TryReadVecDelta(ref reader, stepCount, out deltaX, out deltaY, out deltaZ))
            {
                return DecodeStatus.NeedMoreData;
            }
        }
        else if (reader.TryReadBool(out onGround) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        var yaw = 0f;
        var pitch = 0f;

        if (hasRotation)
        {
            if (reader.TryReadByte(out var yawByte) != DecodeStatus.Success ||
                reader.TryReadByte(out var pitchByte) != DecodeStatus.Success)
            {
                return DecodeStatus.NeedMoreData;
            }

            yaw = AngleFromByte(yawByte);
            pitch = AngleFromByte(pitchByte);
        }

        move = new EntityMove(id, deltaX, deltaY, deltaZ, hasPosition, yaw, pitch, hasRotation, onGround);
        return DecodeStatus.Success;
    }

    /// <summary>Reads a <c>rotate_head</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="id">Entity id.</param>
    /// <param name="headYaw">Head yaw in degrees.</param>
    public static DecodeStatus TryReadRotateHead(ReadOnlySpan<byte> body, out int id, out float headYaw)
    {
        id = 0;
        headYaw = 0f;
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out id) != DecodeStatus.Success ||
            reader.TryReadByte(out var yaw) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        headYaw = AngleFromByte(yaw);
        return DecodeStatus.Success;
    }

    /// <summary>Reads a <c>set_entity_motion</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="motion">Parsed velocity when successful.</param>
    public static DecodeStatus TryReadSetEntityMotion(ReadOnlySpan<byte> body, out EntityMotion motion)
    {
        motion = default;
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success ||
            !TryReadLpVec3(ref reader, out var x, out var y, out var z))
        {
            return DecodeStatus.NeedMoreData;
        }

        motion = new EntityMotion(id, x, y, z);
        return DecodeStatus.Success;
    }

    /// <summary>Reads a <c>teleport_entity</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="teleport">Parsed state when successful.</param>
    public static DecodeStatus TryReadTeleportEntity(ReadOnlySpan<byte> body, out EntityTeleport teleport)
    {
        teleport = default;
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success ||
            reader.TryReadDouble(out var x) != DecodeStatus.Success ||
            reader.TryReadDouble(out var y) != DecodeStatus.Success ||
            reader.TryReadDouble(out var z) != DecodeStatus.Success ||
            reader.TryReadDouble(out var dx) != DecodeStatus.Success ||
            reader.TryReadDouble(out var dy) != DecodeStatus.Success ||
            reader.TryReadDouble(out var dz) != DecodeStatus.Success ||
            reader.TryReadFloat(out var yaw) != DecodeStatus.Success ||
            reader.TryReadFloat(out var pitch) != DecodeStatus.Success ||
            reader.TryReadInt(out var relatives) != DecodeStatus.Success ||
            reader.TryReadBool(out var onGround) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        teleport = new EntityTeleport(id, x, y, z, dx, dy, dz, yaw, pitch, relatives, onGround);
        return DecodeStatus.Success;
    }

    /// <summary>Converts a wire angle byte to degrees; the encoding is a full turn over 256 steps.</summary>
    /// <param name="value">Raw byte.</param>
    public static float AngleFromByte(byte value) => value * (360f / 256f);

    /// <summary>
    /// Reads the compressed vector used for velocities: fifteen bit components scaled by a power of two, with a
    /// continuation flag in bit two of the first byte rather than its high bit
    /// (docs/specs/network/entity-sync.md 4.3).
    /// </summary>
    private static bool TryReadLpVec3(ref PayloadReader reader, out float x, out float y, out float z)
    {
        x = 0f;
        y = 0f;
        z = 0f;

        if (reader.TryReadByte(out var lowest) != DecodeStatus.Success)
        {
            return false;
        }

        if (lowest == 0)
        {
            return true;
        }

        if (reader.TryReadByte(out var middle) != DecodeStatus.Success ||
            reader.TryReadInt(out var highest) != DecodeStatus.Success)
        {
            return false;
        }

        var buffer = ((long)(uint)highest << 16) | ((long)middle << 8) | lowest;
        var scale = (long)(lowest & 3);

        if ((lowest & 4) != 0)
        {
            if (reader.TryReadVarInt(out var extra) != DecodeStatus.Success)
            {
                return false;
            }

            scale |= (long)(uint)extra << 2;
        }

        x = (float)(UnpackLp(buffer >> 3) * scale);
        y = (float)(UnpackLp(buffer >> 18) * scale);
        z = (float)(UnpackLp(buffer >> 33) * scale);
        return true;
    }

    private static double UnpackLp(long value) =>
        (Math.Min(value & 32767L, 32766.0) * 2.0 / 32766.0) - 1.0;

    /// <summary>
    /// Reads a movement delta. Zero on an axis means "keep the current value" rather than "no movement", which is
    /// why the caller must treat it as such (docs/specs/network/entity-sync.md 4.1).
    /// </summary>
    private static bool TryReadVecDelta(
        ref PayloadReader reader,
        int stepCount,
        out double deltaX,
        out double deltaY,
        out double deltaZ)
    {
        const double UnitsPerBlock = 4096.0;
        deltaX = 0d;
        deltaY = 0d;
        deltaZ = 0d;

        if (stepCount <= 0)
        {
            if (!TryReadDeltaTriple(ref reader, out var lx, out var ly, out var lz))
            {
                return false;
            }

            deltaX = lx / UnitsPerBlock;
            deltaY = ly / UnitsPerBlock;
            deltaZ = lz / UnitsPerBlock;
            return true;
        }

        if (stepCount > 1024)
        {
            return false;
        }

        // Steps play out over the following ticks; until the animation path exists the total is applied at once,
        // which is the same end position (docs/specs/network/entity-sync.md 4.1).
        for (var step = 0; step < stepCount; step++)
        {
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success ||
                !TryReadDeltaTriple(ref reader, out var lx, out var ly, out var lz))
            {
                return false;
            }

            deltaX += lx / UnitsPerBlock;
            deltaY += ly / UnitsPerBlock;
            deltaZ += lz / UnitsPerBlock;
        }

        return true;
    }

    private static bool TryReadDeltaTriple(ref PayloadReader reader, out short x, out short y, out short z)
    {
        x = 0;
        y = 0;
        z = 0;

        if (reader.TryReadUnsignedShort(out var ux) != DecodeStatus.Success ||
            reader.TryReadUnsignedShort(out var uy) != DecodeStatus.Success ||
            reader.TryReadUnsignedShort(out var uz) != DecodeStatus.Success)
        {
            return false;
        }

        x = unchecked((short)ux);
        y = unchecked((short)uy);
        z = unchecked((short)uz);
        return true;
    }
}
