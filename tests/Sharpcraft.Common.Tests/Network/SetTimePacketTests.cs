// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>Parses a channel-agnostic <c>set_time</c> body (docs/modules/time.md 3).</summary>
public sealed class SetTimePacketTests
{
    private static byte[] VarInt(int value)
    {
        var bytes = new List<byte>();
        var v = (uint)value;
        while ((v & ~0x7Fu) != 0)
        {
            bytes.Add((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }

        bytes.Add((byte)v);
        return bytes.ToArray();
    }

    private static void AddVarLong(List<byte> bytes, long value)
    {
        var v = (ulong)value;
        while ((v & ~0x7FUL) != 0)
        {
            bytes.Add((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }

        bytes.Add((byte)v);
    }

    private static void AddLongBe(List<byte> bytes, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddFloatBe(List<byte> bytes, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    [Fact]
    public void ParsesGameTimeAndClocks()
    {
        var bytes = new List<byte>();
        bytes.AddRange(VarInt(PacketIds.PlayClientbound.SetTime));
        AddLongBe(bytes, 123456);
        bytes.AddRange(VarInt(2));
        bytes.AddRange(VarInt(0));      // overworld clock
        AddVarLong(bytes, 6000);
        AddFloatBe(bytes, 0.5f);
        AddFloatBe(bytes, 1.0f);
        bytes.AddRange(VarInt(1));      // end clock
        AddVarLong(bytes, 42);
        AddFloatBe(bytes, 0f);
        AddFloatBe(bytes, 0f);

        var reader = new PayloadReader(bytes.ToArray());
        Assert.Equal(DecodeStatus.Success, SetTimePacketCodec.TryRead(ref reader, out var packet));
        Assert.Equal(123456, packet.GameTime);
        Assert.Equal(2, packet.Clocks.Count);
        Assert.Equal(6000, packet.Clocks[0].TotalTicks);
        Assert.Equal(0.5f, packet.Clocks[0].PartialTick);
        Assert.Equal(1.0f, packet.Clocks[0].Rate);
        Assert.Equal(42, packet.Clocks[1].TotalTicks);
    }

    [Fact]
    public void RejectsTheWrongPacketId()
    {
        var bytes = VarInt(PacketIds.PlayClientbound.KeepAlive);
        var reader = new PayloadReader(bytes);
        Assert.NotEqual(DecodeStatus.Success, SetTimePacketCodec.TryRead(ref reader, out _));
    }
}
