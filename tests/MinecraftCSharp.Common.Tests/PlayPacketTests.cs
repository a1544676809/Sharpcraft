// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Network;
using Xunit;

namespace MinecraftCSharp.Common.Tests;

/// <summary>
/// Play-phase helpers: the JoinGame header and the keep-alive/ping replies
/// (docs/specs/network/protocol-phases.md 5.1).
/// </summary>
public sealed class PlayPacketTests
{
    [Fact]
    public void JoinGameHeaderIsParsed()
    {
        var body = BuildJoinGame(playerId: 24617, hardcore: false, levels: ["minecraft:overworld"], maxPlayers: 20, view: 16, sim: 10);

        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadJoinGame(body, out var info));
        Assert.Equal(24617, info.PlayerId);
        Assert.False(info.Hardcore);
        Assert.Equal(["minecraft:overworld"], info.Levels);
        Assert.Equal(20, info.MaxPlayers);
        Assert.Equal(16, info.ChunkRadius);
        Assert.Equal(10, info.SimulationDistance);
        Assert.False(info.ReducedDebugInfo);
    }

    [Fact]
    public void JoinGameHandlesMultipleLevelsAndHardcore()
    {
        var body = BuildJoinGame(
            playerId: 1,
            hardcore: true,
            levels: ["minecraft:overworld", "minecraft:the_nether", "minecraft:the_end"],
            maxPlayers: 20,
            view: 12,
            sim: 8);

        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadJoinGame(body, out var info));
        Assert.True(info.Hardcore);
        Assert.Equal(3, info.Levels.Count);
        Assert.Equal("minecraft:the_end", info.Levels[^1]);
    }

    [Fact]
    public void JoinGameRejectsOtherPackets()
    {
        var body = BuildJoinGame(1, false, ["minecraft:overworld"], 20, 16, 10);
        body[0] = 32; // pretend it is a disconnect

        Assert.Equal(DecodeStatus.Invalid, PlayPackets.TryReadJoinGame(body, out _));
    }

    [Fact]
    public void JoinGameTruncationIsReportedAsNeedMoreData()
    {
        var body = BuildJoinGame(1, false, ["minecraft:overworld"], 20, 16, 10);

        Assert.Equal(DecodeStatus.NeedMoreData, PlayPackets.TryReadJoinGame(body.AsSpan(0, body.Length - 3), out _));
    }

    [Fact]
    public void KeepAliveReplyEchoesTheIdentifier()
    {
        const long identifier = 0x0102030405060708L;

        var reply = PlayPackets.BuildKeepAliveReply(identifier);

        var reader = new PayloadReader(reply);
        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var packetId));
        Assert.Equal(PacketIds.PlayServerbound.KeepAlive, packetId);
        Assert.Equal(DecodeStatus.Success, reader.TryReadLong(out var echoed));
        Assert.Equal(identifier, echoed);
        Assert.Equal(reply.Length, reader.Position);
    }

    [Fact]
    public void PongReplyEchoesTheIntIdentifier()
    {
        var reply = PlayPackets.BuildPongReply(4242);

        var reader = new PayloadReader(reply);
        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var packetId));
        Assert.Equal(PacketIds.PlayServerbound.Pong, packetId);
        Assert.Equal(DecodeStatus.Success, reader.TryReadInt(out var echoed));
        Assert.Equal(4242, echoed);
    }

    private static byte[] BuildJoinGame(
        int playerId,
        bool hardcore,
        IReadOnlyList<string> levels,
        int maxPlayers,
        int view,
        int sim)
    {
        var buffer = new byte[512];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayClientbound.Login);
        writer.WriteInt(playerId);
        writer.WriteBool(hardcore);
        writer.WriteVarInt(levels.Count);
        foreach (var level in levels)
        {
            writer.WriteString(level, 32767);
        }

        writer.WriteVarInt(maxPlayers);
        writer.WriteVarInt(view);
        writer.WriteVarInt(sim);
        writer.WriteBool(false);   // reduced debug info
        writer.WriteBool(true);    // show death screen
        writer.WriteBool(false);   // limited crafting

        // Spawn info: dimension type id, dimension key, seed, game mode, previous game mode,
        // debug flag, flat flag, optional death location, portal cooldown, sea level.
        writer.WriteVarInt(0);
        writer.WriteString("minecraft:overworld", 32767);
        writer.WriteLong(0x1234_5678_9ABC_DEF0L);
        writer.WriteVarInt(0);
        writer.WriteVarInt(0);
        writer.WriteBool(false);
        writer.WriteBool(false);
        writer.WriteBool(false);
        writer.WriteVarInt(0);
        writer.WriteVarInt(63);

        return buffer.AsSpan(0, writer.Position).ToArray();
    }
}
