// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Builds and parses the packets that keep the client's position in step with the server
/// (docs/specs/network/play-movement.md). Field order follows the protocol sources exactly; a swapped float
/// and double here would silently teleport the player.
/// </summary>
public static class PlayMovementPackets
{
    /// <summary>Builds a position-only update.</summary>
    /// <param name="x">X in blocks.</param>
    /// <param name="y">Y in blocks.</param>
    /// <param name="z">Z in blocks.</param>
    /// <param name="onGround">Whether the player stands on something.</param>
    /// <param name="horizontalCollision">Whether the last move hit a wall.</param>
    public static byte[] BuildMovePlayerPos(double x, double y, double z, bool onGround, bool horizontalCollision)
    {
        Span<byte> buffer = stackalloc byte[48];
        var writer = new PayloadWriter(buffer);
        WriteHeader(ref writer, PacketIds.PlayServerbound.MovePlayerPos);
        writer.WriteDouble(x);
        writer.WriteDouble(y);
        writer.WriteDouble(z);
        writer.WriteByte(PackFlags(onGround, horizontalCollision));
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>Builds a position and rotation update, which is what a walking player sends.</summary>
    /// <param name="x">X in blocks.</param>
    /// <param name="y">Y in blocks.</param>
    /// <param name="z">Z in blocks.</param>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    /// <param name="onGround">Whether the player stands on something.</param>
    /// <param name="horizontalCollision">Whether the last move hit a wall.</param>
    public static byte[] BuildMovePlayerPosRot(
        double x,
        double y,
        double z,
        float yawDegrees,
        float pitchDegrees,
        bool onGround,
        bool horizontalCollision)
    {
        Span<byte> buffer = stackalloc byte[64];
        var writer = new PayloadWriter(buffer);
        WriteHeader(ref writer, PacketIds.PlayServerbound.MovePlayerPosRot);
        writer.WriteDouble(x);
        writer.WriteDouble(y);
        writer.WriteDouble(z);
        writer.WriteFloat(yawDegrees);
        writer.WriteFloat(pitchDegrees);
        writer.WriteByte(PackFlags(onGround, horizontalCollision));
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>Builds a rotation-only update.</summary>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    /// <param name="onGround">Whether the player stands on something.</param>
    /// <param name="horizontalCollision">Whether the last move hit a wall.</param>
    public static byte[] BuildMovePlayerRot(float yawDegrees, float pitchDegrees, bool onGround, bool horizontalCollision)
    {
        Span<byte> buffer = stackalloc byte[24];
        var writer = new PayloadWriter(buffer);
        WriteHeader(ref writer, PacketIds.PlayServerbound.MovePlayerRot);
        writer.WriteFloat(yawDegrees);
        writer.WriteFloat(pitchDegrees);
        writer.WriteByte(PackFlags(onGround, horizontalCollision));
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>Builds a status-only update, sent when only the flags changed.</summary>
    /// <param name="onGround">Whether the player stands on something.</param>
    /// <param name="horizontalCollision">Whether the last move hit a wall.</param>
    public static byte[] BuildMovePlayerStatusOnly(bool onGround, bool horizontalCollision)
    {
        Span<byte> buffer = stackalloc byte[8];
        var writer = new PayloadWriter(buffer);
        WriteHeader(ref writer, PacketIds.PlayServerbound.MovePlayerStatusOnly);
        writer.WriteByte(PackFlags(onGround, horizontalCollision));
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>Builds the reply that accepts a server teleport.</summary>
    /// <param name="teleportId">Id from the teleport packet.</param>
    /// <param name="x">Accepted x in blocks.</param>
    /// <param name="y">Accepted y in blocks.</param>
    /// <param name="z">Accepted z in blocks.</param>
    /// <param name="yawDegrees">Accepted yaw in degrees.</param>
    /// <param name="pitchDegrees">Accepted pitch in degrees.</param>
    public static byte[] BuildAcceptTeleportation(
        int teleportId,
        double x,
        double y,
        double z,
        float yawDegrees,
        float pitchDegrees)
    {
        Span<byte> buffer = stackalloc byte[48];
        var writer = new PayloadWriter(buffer);
        WriteHeader(ref writer, PacketIds.PlayServerbound.AcceptTeleportation);
        writer.WriteVarInt(teleportId);
        writer.WriteDouble(x);
        writer.WriteDouble(y);
        writer.WriteDouble(z);
        writer.WriteFloat(yawDegrees);
        writer.WriteFloat(pitchDegrees);
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>Parses a server position packet by its body.</summary>
    /// <param name="body">Packet body without the id, i.e. everything after the VarInt id.</param>
    /// <param name="info">Parsed correction when successful.</param>
    public static DecodeStatus TryReadPlayerPosition(ReadOnlySpan<byte> body, out PlayerPositionInfo? info)
    {
        info = null;
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var teleportId) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (!TryReadVec3(ref reader, out var position) ||
            !TryReadVec3(ref reader, out var delta) ||
            reader.TryReadFloat(out var yaw) != DecodeStatus.Success ||
            reader.TryReadFloat(out var pitch) != DecodeStatus.Success ||
            reader.TryReadInt(out var relatives) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        info = new PlayerPositionInfo(
            teleportId,
            position.X,
            position.Y,
            position.Z,
            delta.X,
            delta.Y,
            delta.Z,
            yaw,
            pitch,
            relatives);

        return DecodeStatus.Success;
    }

    private static bool TryReadVec3(ref PayloadReader reader, out (double X, double Y, double Z) value)
    {
        value = default;
        if (reader.TryReadDouble(out var x) != DecodeStatus.Success ||
            reader.TryReadDouble(out var y) != DecodeStatus.Success ||
            reader.TryReadDouble(out var z) != DecodeStatus.Success)
        {
            return false;
        }

        value = (x, y, z);
        return true;
    }

    private static void WriteHeader(ref PayloadWriter writer, int packetId) => writer.WriteVarInt(packetId);

    /// <summary>
    /// Packs the two movement flags into the single byte the protocol uses. Writing them as two booleans
    /// adds a byte the server does not expect, and the connection dies on the first move.
    /// </summary>
    /// <param name="onGround">Whether the player stands on something.</param>
    /// <param name="horizontalCollision">Whether the last move hit a wall.</param>
    public static byte PackFlags(bool onGround, bool horizontalCollision)
    {
        byte flags = 0;
        if (onGround)
        {
            flags |= 1;
        }

        if (horizontalCollision)
        {
            flags |= 2;
        }

        return flags;
    }
}
