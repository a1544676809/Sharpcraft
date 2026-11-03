// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>Decodes clientbound set_equipment (docs/analysis/render/entity-equipment.md 2).</summary>
public sealed class SetEquipmentPacketTests
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

    private static byte[] Body(params Action<List<byte>>[] slots)
    {
        var bytes = new List<byte>();
        bytes.AddRange(VarInt(PacketIds.PlayClientbound.SetEquipment));
        bytes.AddRange(VarInt(42));
        foreach (var slot in slots)
        {
            slot(bytes);
        }

        return bytes.ToArray();
    }

    private static EquipmentItem[] Read(byte[] body, out int entityId, out bool complete, out DecodeStatus status)
    {
        Span<EquipmentItem> items = stackalloc EquipmentItem[SetEquipmentPackets.MaxSlots];
        status = SetEquipmentPackets.TryRead(body, out entityId, items, out var count, out complete);
        return items[..count].ToArray();
    }

    [Fact]
    public void ContinuationBitChainsMultipleSlots()
    {
        var body = Body(
            b =>
            {
                b.Add(0x87);                 // saddle (7) with continuation
                b.AddRange(VarInt(1));       // count
                b.AddRange(VarInt(123));     // item id
                b.AddRange(VarInt(0));       // added components
                b.AddRange(VarInt(0));       // removed components
            },
            b =>
            {
                b.Add(2);                    // feet, last
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(456));
                b.AddRange(VarInt(0));
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out var entityId, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(42, entityId);
        Assert.Equal(2, result.Length);
        Assert.Equal(7, result[0].Slot);
        Assert.Equal(123, result[0].ItemId);
        Assert.Equal(2, result[1].Slot);
        Assert.Equal(456, result[1].ItemId);
    }

    [Fact]
    public void EmptyStackClearsTheSlot()
    {
        var body = Body(b =>
        {
            b.Add(7);
            b.AddRange(VarInt(0));
        });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Single(result);
        Assert.True(result[0].IsEmpty);
    }

    [Fact]
    public void NonEmptyComponentPatchIsReportedAndStopsParsing()
    {
        var body = Body(
            b =>
            {
                b.Add(0x80);                 // mainhand with continuation
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(123));
                b.AddRange(VarInt(1));       // one added component; payload not modelled
                b.AddRange(VarInt(0));
            },
            b =>
            {
                b.Add(7);                    // never reached because the patch length is unknown
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.False(complete);
        Assert.Single(result);
        Assert.True(result[0].HasDataComponents);
    }

    [Fact]
    public void TruncatedStackReportsNeedMoreData()
    {
        var body = new List<byte>();
        body.AddRange(VarInt(PacketIds.PlayClientbound.SetEquipment));
        body.AddRange(VarInt(42));
        body.Add(7);
        body.AddRange(VarInt(1));
        body.AddRange(VarInt(123)); // no component counts

        Read(body.ToArray(), out _, out _, out var status);

        Assert.Equal(DecodeStatus.NeedMoreData, status);
    }
}
