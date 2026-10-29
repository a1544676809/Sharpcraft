// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Network;

/// <summary>
/// Player list packets and the roster they feed (docs/modules/ui.md 13, M5 exit criteria). The interesting parts are
/// partial updates, which must merge rather than overwrite, and removal, which must keep the rows contiguous.
/// </summary>
public sealed class PlayerListTests
{
    private static readonly Guid Alice = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void AnAddPacketCarriesNameAndLatency()
    {
        var body = Body(
            actions: PlayerListActions.AddPlayer | PlayerListActions.UpdateLatency,
            count: 1,
            writer =>
            {
                WriteUuid(writer, Alice);
                WriteString(writer, "Alice");
                WriteVarInt(writer, 0);          // no profile properties
                WriteVarInt(writer, 42);         // latency
            });

        Assert.Equal(DecodeStatus.Success, PlayerListPackets.TryReadPlayerInfoUpdate(body, out var actions, out var entries));
        Assert.Equal(PlayerListActions.AddPlayer | PlayerListActions.UpdateLatency, actions);
        Assert.Single(entries);
        Assert.Equal(Alice, entries[0].Id);
        Assert.Equal("Alice", entries[0].Name);
        Assert.Equal(42, entries[0].Latency);
    }

    [Fact]
    public void PayloadsAreReadInActionOrderNotCallOrder()
    {
        // Listed, game mode and latency all present: the payloads arrive in ordinal order, which is game mode,
        // listed, latency. Reading them in another order would desynchronise the rest of the packet.
        var body = Body(
            actions: PlayerListActions.UpdateLatency | PlayerListActions.UpdateGameMode | PlayerListActions.UpdateListed,
            count: 1,
            writer =>
            {
                WriteUuid(writer, Bob);
                WriteVarInt(writer, 1);          // game mode
                writer.WriteByte(0);              // listed = false
                WriteVarInt(writer, 7);          // latency
            });

        Assert.Equal(DecodeStatus.Success, PlayerListPackets.TryReadPlayerInfoUpdate(body, out _, out var entries));
        Assert.Equal(1, entries[0].GameMode);
        Assert.False(entries[0].Listed);
        Assert.Equal(7, entries[0].Latency);
    }

    [Fact]
    public void ATruncatedPacketReportsNeedMoreData()
    {
        var body = Body(
            actions: PlayerListActions.AddPlayer,
            count: 1,
            writer =>
            {
                WriteUuid(writer, Alice);
                WriteString(writer, "Alice");
            });

        // Cut the profile property count off the end.
        Assert.Equal(
            DecodeStatus.NeedMoreData,
            PlayerListPackets.TryReadPlayerInfoUpdate(body[..^2], out _, out _));
    }

    [Fact]
    public void ARemovePacketCarriesIds()
    {
        // A remove packet is a count and then the ids: it has no action bitset.
        using var stream = new MemoryStream();
        WriteVarInt(stream, 2);
        WriteUuid(stream, Alice);
        WriteUuid(stream, Bob);
        var body = stream.ToArray();

        Assert.Equal(DecodeStatus.Success, PlayerListPackets.TryReadPlayerInfoRemove(body, out var ids));
        Assert.Equal([Alice, Bob], ids);
    }

    [Fact]
    public void ALatencyOnlyUpdateKeepsTheName()
    {
        var roster = new PlayerListRoster();
        roster.Apply([new PlayerInfoUpdate(Alice, "Alice", null, null, 30, null, null, false)]);
        roster.Apply([new PlayerInfoUpdate(Alice, null, null, null, 55, null, null, false)]);

        Assert.Equal(1, roster.Count);
        Assert.Equal("Alice", roster.Entries[0].Name);
        Assert.Equal(55, roster.Entries[0].Latency);
    }

    [Fact]
    public void RemovingAPlayerKeepsTheRestContiguous()
    {
        var roster = new PlayerListRoster();
        roster.Apply(
        [
            new PlayerInfoUpdate(Alice, "Alice", null, null, 10, null, null, false),
            new PlayerInfoUpdate(Bob, "Bob", null, null, 20, null, null, false)
        ]);

        roster.Remove([Alice]);

        Assert.Equal(1, roster.Count);
        Assert.Equal(Bob, roster.Entries[0].Id);
        Assert.Equal("Bob", roster.Entries[0].Name);
    }

    [Fact]
    public void RemovingAnUnknownPlayerIsHarmless()
    {
        var roster = new PlayerListRoster();
        roster.Apply([new PlayerInfoUpdate(Alice, "Alice", null, null, 10, null, null, false)]);

        roster.Remove([Bob]);

        Assert.Equal(1, roster.Count);
    }

    [Fact]
    public void ApplyingWhileGrowingTheBufferKeepsEveryRow()
    {
        var roster = new PlayerListRoster();
        var updates = new PlayerInfoUpdate[200];

        for (var index = 0; index < updates.Length; index++)
        {
            updates[index] = new PlayerInfoUpdate(Guid.NewGuid(), $"player{index}", null, null, index, null, null, false);
        }

        roster.Apply(updates);

        Assert.Equal(200, roster.Count);
        Assert.Equal("player199", roster.Entries[199].Name);
    }

    private static byte[] Body(PlayerListActions actions, int count, Action<MemoryStream> writeEntries)
    {
        using var stream = new MemoryStream();
        WriteVarInt(stream, (int)actions);
        WriteVarInt(stream, count);
        writeEntries(stream);
        return stream.ToArray();
    }

    private static void WriteUuid(Stream stream, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        Assert.True(value.TryWriteBytes(bytes));
        stream.Write(bytes);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var remaining = (uint)value;

        while (remaining >= 0x80)
        {
            stream.WriteByte((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        stream.WriteByte((byte)remaining);
    }
}
