// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Network;

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
public sealed record JoinGameInfo(
    int PlayerId,
    bool Hardcore,
    IReadOnlyList<string> Levels,
    int MaxPlayers,
    int ChunkRadius,
    int SimulationDistance,
    bool ReducedDebugInfo,
    bool ShowDeathScreen,
    bool LimitedCrafting);

/// <summary>
/// Play-phase packet helpers (docs/specs/network/protocol-phases.md): the subset needed to hold
/// a connection open, plus the JoinGame header.
/// </summary>
/// <remarks>
/// Only the leading JoinGame fields are decoded; the spawn-info block is skipped deliberately
/// until the world layer needs it, but the reader still validates the header it does parse.
/// </remarks>
public static class PlayPackets
{
    /// <summary>Decodes the JoinGame header.</summary>
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

        info = new JoinGameInfo(
            playerId,
            hardcore,
            levels,
            maxPlayers,
            chunkRadius,
            simulationDistance,
            reducedDebugInfo,
            showDeathScreen,
            limitedCrafting);
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
