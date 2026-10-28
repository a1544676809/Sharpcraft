// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>Essentials of the play-phase JoinGame packet (clientbound <c>login</c>, id 50).</summary>
/// <param name="PlayerId">Entity id assigned to this player.</param>
/// <param name="Hardcore">Whether the world is hardcore.</param>
/// <param name="Levels">Dimension identifiers offered by the server.</param>
/// <param name="MaxPlayers">Advertised player cap.</param>
/// <param name="ChunkRadius">Server view distance in chunks.</param>
/// <param name="SimulationDistance">Server simulation distance.</param>
/// <param name="ReducedDebugInfo">Whether debug info is restricted.</param>
/// <param name="ShowDeathScreen">Whether the death screen is shown.</param>
/// <param name="LimitedCrafting">Whether recipes are restricted.</param>
/// <param name="DimensionTypeId">Positional id into the synchronised dimension_type registry.</param>
/// <param name="Seed">World seed as sent to the client.</param>
/// <param name="GameMode">Game mode id.</param>
public sealed record JoinGameInfo(
    int PlayerId,
    bool Hardcore,
    IReadOnlyList<string> Levels,
    int MaxPlayers,
    int ChunkRadius,
    int SimulationDistance,
    bool ReducedDebugInfo,
    bool ShowDeathScreen,
    bool LimitedCrafting,
    int DimensionTypeId = 0,
    string DimensionId = "",
    long Seed = 0,
    byte GameMode = 0);

/// <summary>
/// Play-phase packet helpers: the JoinGame header plus the keep-alive and ping replies needed to
/// hold a connection open (docs/specs/network/protocol-phases.md 5.2).
/// </summary>
/// <remarks>
/// Every enum inside the spawn info is a <b>VarInt</b>: game type uses an id mapper and the
/// previous game type uses an optional VarInt where 0 means absent. Treating them as bytes
/// desynchronises the rest of the packet, which is how this was found.
/// </remarks>
public static class PlayPackets
{
    /// <summary>Decodes the JoinGame header including the spawn info prefix.</summary>
    public static DecodeStatus TryReadJoinGame(ReadOnlySpan<byte> body, out JoinGameInfo info)
    {
        info = new JoinGameInfo(0, false, [], 0, 0, 0, false, false, false);

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.Login)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadInt(out var playerId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadBool(out var hardcore);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out var levelCount);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (levelCount < 0 || levelCount > 64)
        {
            return DecodeStatus.Invalid;
        }

        var levels = new List<string>(levelCount);
        for (var i = 0; i < levelCount; i++)
        {
            status = reader.TryReadString(out var level, 32767);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            levels.Add(level);
        }

        if (reader.TryReadVarInt(out var maxPlayers) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var chunkRadius) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var simulationDistance) != DecodeStatus.Success ||
            reader.TryReadBool(out var reducedDebugInfo) != DecodeStatus.Success ||
            reader.TryReadBool(out var showDeathScreen) != DecodeStatus.Success ||
            reader.TryReadBool(out var limitedCrafting) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        // Spawn info: dimension type (registry reference), dimension key (identifier), seed,
        // game mode, previous game mode (0 = none), debug flag, flat flag, optional death
        // location, portal cooldown, sea level. Missing the dimension key here shifts every
        // following field, which is exactly how this was found.
        if (reader.TryReadVarInt(out var dimensionTypeId) != DecodeStatus.Success ||
            reader.TryReadString(out var dimensionId, 32767) != DecodeStatus.Success ||
            reader.TryReadLong(out var seed) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var gameMode) != DecodeStatus.Success ||
            reader.TryReadVarInt(out _) != DecodeStatus.Success ||
            reader.TryReadBool(out _) != DecodeStatus.Success ||
            reader.TryReadBool(out _) != DecodeStatus.Success ||
            reader.TryReadBool(out var hasDeathLocation) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (hasDeathLocation &&
            (reader.TryReadString(out _, 32767) != DecodeStatus.Success ||
             reader.TryReadLong(out _) != DecodeStatus.Success))
        {
            return DecodeStatus.NeedMoreData;
        }

        if (reader.TryReadVarInt(out _) != DecodeStatus.Success ||
            reader.TryReadVarInt(out _) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        info = new JoinGameInfo(
            playerId,
            hardcore,
            levels,
            maxPlayers,
            chunkRadius,
            simulationDistance,
            reducedDebugInfo,
            showDeathScreen,
            limitedCrafting,
            dimensionTypeId,
            dimensionId,
            seed,
            unchecked((byte)gameMode));
        return DecodeStatus.Success;
    }

    /// <summary>Builds a keep-alive reply echoing the server's id.</summary>
    public static byte[] BuildKeepAliveReply(long keepAliveId)
    {
        var body = new byte[16];
        var writer = new PayloadWriter(body);
        writer.WriteVarInt(PacketIds.PlayServerbound.KeepAlive);
        writer.WriteLong(keepAliveId);
        return body.AsSpan(0, writer.Position).ToArray();
    }

    /// <summary>Builds a pong reply echoing the server's int id.</summary>
    public static byte[] BuildPongReply(int pingId)
    {
        var body = new byte[16];
        var writer = new PayloadWriter(body);
        writer.WriteVarInt(PacketIds.PlayServerbound.Pong);
        writer.WriteInt(pingId);
        return body.AsSpan(0, writer.Position).ToArray();
    }
}
