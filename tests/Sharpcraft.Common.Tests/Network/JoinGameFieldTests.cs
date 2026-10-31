// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Field level equivalence for the login packet (docs/specs/quality/vanilla-equivalence-matrix.md row 1).
/// </summary>
/// <remarks>
/// The field order comes from the reference: written in order are the player id, hardcore flag, the dimension key set,
/// the three distances, three booleans, the spawn info (dimension type, dimension key, seed, game type, previous game
/// type, debug, flat, optional death location, portal cooldown, sea level), then online mode and secure chat. The client
/// reads through the game type and deliberately ignores the rest, so the test proves the values it does read and that the
/// unused tail cannot shift them.
/// </remarks>
public sealed class JoinGameFieldTests
{
    [Fact]
    public void EveryFieldTheClientReadsMatchesTheReferenceOrder()
    {
        var body = Build(
            playerId: 4242,
            hardcore: true,
            levels: ["minecraft:overworld", "minecraft:the_nether", "minecraft:the_end"],
            maxPlayers: 20,
            view: 12,
            sim: 8,
            reducedDebug: true,
            showDeathScreen: false,
            limitedCrafting: true,
            dimensionTypeId: 3,
            dimension: "minecraft:the_nether",
            seed: 0x0102_0304_0506_0708L,
            gameMode: 1);

        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadJoinGame(body, out var info));

        Assert.Equal(4242, info.PlayerId);
        Assert.True(info.Hardcore);
        Assert.Equal(3, info.Levels.Count);
        Assert.Equal("minecraft:the_end", info.Levels[^1]);
        Assert.Equal(20, info.MaxPlayers);
        Assert.Equal(12, info.ChunkRadius);
        Assert.Equal(8, info.SimulationDistance);
        Assert.True(info.ReducedDebugInfo);
        Assert.False(info.ShowDeathScreen);
        Assert.True(info.LimitedCrafting);
        Assert.Equal(3, info.DimensionTypeId);
        Assert.Equal("minecraft:the_nether", info.DimensionId);
        Assert.Equal(0x0102_0304_0506_0708L, info.Seed);
        Assert.Equal(1, info.GameMode);
    }

    [Fact]
    public void TheTailTheClientIgnoresCannotShiftTheFieldsItReads()
    {
        // Same values but with online mode and secure chat set, which sit after everything the client reads.
        var body = Build(7, false, ["minecraft:overworld"], 20, 10, 6, false, true, false, 0, "minecraft:overworld", 5, 0, onlineMode: true, secureChat: true);

        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadJoinGame(body, out var info));
        Assert.Equal(7, info.PlayerId);
        Assert.Equal(10, info.ChunkRadius);
        Assert.Equal(6, info.SimulationDistance);
        Assert.Equal("minecraft:overworld", info.DimensionId);
        Assert.Equal(5, info.Seed);
    }

    [Fact]
    public void APresentDeathLocationIsAccepted()
    {
        var body = Build(9, false, ["minecraft:overworld"], 20, 10, 6, false, true, false, 0, "minecraft:overworld", 1, 0, hasDeathLocation: true);

        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadJoinGame(body, out var info));
        Assert.Equal(9, info.PlayerId);
        Assert.Equal(0, info.GameMode);
    }

    private static byte[] Build(
        int playerId,
        bool hardcore,
        IReadOnlyList<string> levels,
        int maxPlayers,
        int view,
        int sim,
        bool reducedDebug,
        bool showDeathScreen,
        bool limitedCrafting,
        int dimensionTypeId,
        string dimension,
        long seed,
        int gameMode,
        bool hasDeathLocation = false,
        bool onlineMode = false,
        bool secureChat = false)
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
        writer.WriteBool(reducedDebug);
        writer.WriteBool(showDeathScreen);
        writer.WriteBool(limitedCrafting);

        // Spawn info.
        writer.WriteVarInt(dimensionTypeId);
        writer.WriteString(dimension, 32767);
        writer.WriteLong(seed);
        writer.WriteVarInt(gameMode);
        writer.WriteVarInt(0);              // previous game type: absent
        writer.WriteBool(false);            // debug
        writer.WriteBool(false);            // flat
        writer.WriteBool(hasDeathLocation);

        if (hasDeathLocation)
        {
            writer.WriteString("minecraft:overworld", 32767);
            writer.WriteLong(0);            // packed block position
        }

        writer.WriteVarInt(0);              // portal cooldown
        writer.WriteVarInt(63);             // sea level
        writer.WriteBool(onlineMode);
        writer.WriteBool(secureChat);

        return buffer.AsSpan(0, writer.Position).ToArray();
    }
}
