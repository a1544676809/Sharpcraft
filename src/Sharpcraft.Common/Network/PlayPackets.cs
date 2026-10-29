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
    /// <summary>Longest chat or command line the reference accepts.</summary>
    public const int MaxChatLength = 256;

    /// <summary>
    /// Builds an unsigned chat message.
    /// </summary>
    /// <param name="message">Line to send, without a leading slash.</param>
    /// <param name="timestamp">Milliseconds since the epoch, as the reference sends it.</param>
    /// <remarks>
    /// The reference always sends a timestamp, a salt, an optional signature and a last-seen update. When no chat
    /// session exists the signature is absent, and the last-seen update is an offset of zero, twenty acknowledged
    /// bits that are all clear, and a checksum of zero, which the receiver treats as "ignore". Sending those fields
    /// as zeros rather than omitting them is what keeps the packet well formed.
    /// </remarks>
    public static byte[] BuildChatMessage(string message, long timestamp)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Length > MaxChatLength)
        {
            throw new ArgumentException($"a chat line is at most {MaxChatLength} characters", nameof(message));
        }

        var body = new byte[(message.Length * 4) + 64];
        var writer = new PayloadWriter(body);
        writer.WriteVarInt(PacketIds.PlayServerbound.Chat);
        writer.WriteString(message);
        writer.WriteLong(timestamp);
        writer.WriteLong(0L);        // salt
        writer.WriteBool(false);     // no signature
        writer.WriteVarInt(0);       // last seen offset
        writer.WriteByte(0);         // twenty acknowledged bits, three bytes, all clear
        writer.WriteByte(0);
        writer.WriteByte(0);
        writer.WriteByte(0);         // checksum: zero means ignore
        return body.AsSpan(0, writer.Position).ToArray();
    }

    /// <summary>Builds a command line, which keeps its leading slash.</summary>
    /// <param name="command">Command text including the slash.</param>
    public static byte[] BuildChatCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Length == 0 || command.Length > MaxChatLength)
        {
            throw new ArgumentException($"a command is between one and {MaxChatLength} characters", nameof(command));
        }

        var body = new byte[(command.Length * 4) + 8];
        var writer = new PayloadWriter(body);
        writer.WriteVarInt(PacketIds.PlayServerbound.ChatCommand);
        writer.WriteString(command);
        return body.AsSpan(0, writer.Position).ToArray();
    }

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

    /// <summary>
    /// Builds the client information packet. Vanilla sends it once the world is joined, and a server may
    /// treat a client that never sends it as not fully initialised.
    /// </summary>
    /// <param name="locale">Language tag, for example "en_us".</param>
    /// <param name="viewDistance">Render distance in chunks.</param>
    /// <param name="chatVisibility">0 = full, 1 = commands only, 2 = hidden.</param>
    /// <param name="chatColors">Whether chat colours are enabled.</param>
    /// <param name="skinParts">Skin layer bitmask.</param>
    /// <param name="mainHand">0 = left, 1 = right.</param>
    /// <param name="textFiltering">Whether server-side text filtering is enabled.</param>
    /// <param name="allowListing">Whether the player allows server listing.</param>
    /// <param name="particleStatus">Particle setting: 0 all, 1 decreased, 2 minimal.</param>
    public static byte[] BuildClientInformation(
        string locale,
        byte viewDistance,
        int chatVisibility,
        bool chatColors,
        byte skinParts,
        int mainHand,
        bool textFiltering,
        bool allowListing,
        int particleStatus = 0)
    {
        var buffer = new byte[256];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayServerbound.ClientInformation);
        writer.WriteString(locale);
        writer.WriteByte(viewDistance);
        writer.WriteVarInt(chatVisibility);
        writer.WriteBool(chatColors);
        writer.WriteByte(skinParts);
        writer.WriteVarInt(mainHand);
        writer.WriteBool(textFiltering);
        writer.WriteBool(allowListing);

        // 26.3 added a particle-status field at the end of this packet; omitting it makes the server fail to
        // decode the whole packet and drop the connection.
        writer.WriteVarInt(particleStatus);
        return buffer[..writer.Position].ToArray();
    }
    /// <summary>
    /// Builds the end-of-tick marker. It has an empty body; its only job is to tell the server that this
    /// client tick is finished, which resets the per-tick movement guards (docs/specs/network/play-movement.md).
    /// </summary>
    public static byte[] BuildClientTickEnd()
    {
        var buffer = new byte[2];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayServerbound.ClientTickEnd);
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>
    /// Builds the acknowledgement that asks the server for the next chunk batch. The server waits for it,
    /// so a client that never sends it stops receiving chunks.
    /// </summary>
    /// <param name="desiredChunksPerTick">How many chunks the client can take per tick.</param>
    public static byte[] BuildChunkBatchReceived(float desiredChunksPerTick)
    {
        var buffer = new byte[8];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayServerbound.ChunkBatchReceived);
        writer.WriteFloat(desiredChunksPerTick);
        return buffer[..writer.Position].ToArray();
    }

    /// <summary>
    /// Builds a block interaction packet. The action ordinal, the packed position, the face and the sequence
    /// counter are written in that order (docs/specs/network/play-movement.md 7.18).
    /// </summary>
    /// <param name="action">Action ordinal; 0 starts destroying a block.</param>
    /// <param name="x">Block x.</param>
    /// <param name="y">Block y.</param>
    /// <param name="z">Block z.</param>
    /// <param name="face">Face the click hit: 0 down, 1 up, 2 north, 3 south, 4 west, 5 east.</param>
    /// <param name="sequence">Monotonic counter the server uses to order predictions.</param>
    public static byte[] BuildPlayerAction(int action, int x, int y, int z, int face, int sequence)
    {
        var buffer = new byte[32];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayServerbound.PlayerAction);
        writer.WriteVarInt(action);
        writer.WriteLong(BlockPositionCodec.PackBlockPosition(x, y, z));
        writer.WriteByte((byte)face);
        writer.WriteVarInt(sequence);
        return buffer[..writer.Position].ToArray();
    }
}
