// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Decodes the synced entity data packet (docs/analysis/render/entity-variants.md 2). The interesting part is that the
/// variant is not the first item: base-class values arrive first and have to be skipped by serializer width.
/// </summary>
public sealed class EntityDataPacketTests
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

    private static void Float(List<byte> bytes, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void Item(List<byte> bytes, int index, int serializer)
    {
        bytes.Add((byte)index);
        bytes.AddRange(VarInt(serializer));
    }

    private static byte[] Body(params Action<List<byte>>[] items)
    {
        var bytes = new List<byte>();
        bytes.AddRange(VarInt(PacketIds.PlayClientbound.SetEntityData));
        bytes.AddRange(VarInt(42));
        foreach (var item in items)
        {
            item(bytes);
        }

        bytes.Add(EntityDataPackets.EndMarker);
        return bytes.ToArray();
    }

    private static EntityDataItem[] Read(byte[] body, out int entityId, out bool complete, out DecodeStatus status)
    {
        Span<EntityDataItem> items = stackalloc EntityDataItem[EntityDataPackets.MaxItems];
        status = EntityDataPackets.TryRead(body, out entityId, items, out var count, out complete);
        return items[..count].ToArray();
    }

    [Fact]
    public void ItemStackSerializerDecodesAndTheScanContinues()
    {
        // item entity: index 8 is ITEM_STACK(7) = Optional<ItemStack>; a following BOOLEAN must still decode, which is
        // what "continue past a previously unsupported serializer" buys us.
        var body = Body(
            b =>
            {
                Item(b, 8, 7);
                b.AddRange(VarInt(1));    // count 1, not empty
                b.AddRange(VarInt(7));    // item id
                b.AddRange(VarInt(0));    // zero added components
                b.AddRange(VarInt(0));    // zero removed components
            },
            b => { Item(b, 9, 8); b.Add(0x01); });

        var items = Read(body, out _, out var complete, out var status);
        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, items.Length);
        Assert.Equal(8, items[0].Index);
        Assert.Equal(1, items[0].ItemCount);
        Assert.Equal(7, items[0].ItemId);
        Assert.Equal(9, items[1].Index);
        Assert.Equal(1, items[1].Value);
    }

    [Fact]
    public void EmptyItemStackConsumesOnlyTheCount()
    {
        var body = Body(
            b => { Item(b, 8, 7); b.AddRange(VarInt(0)); },
            b => { Item(b, 9, 8); b.Add(0x00); });

        var items = Read(body, out _, out var complete, out var status);
        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, items.Length);
        Assert.Equal(0, items[0].ItemCount);
        Assert.Equal(-1, items[0].ItemId);
    }

    [Fact]
    public void TheVariantIsDecodedAfterTheBaseFieldsAreSkipped()
    {
        var body = Body(
            b => { Item(b, 8, 0); b.Add(0x02); },               // living flags (byte)
            b => { Item(b, 9, 3); Float(b, 20f); },             // health (float)
            b => { Item(b, 10, 17); b.AddRange(VarInt(0)); },   // effect particles (empty list)
            b => { Item(b, 14, 11); b.Add(0x00); },             // sleeping pos (absent)
            b => { Item(b, 16, 8); b.Add(0x00); },              // baby (false)
            b => { Item(b, 19, 28); b.AddRange(VarInt(2)); });  // pig variant (registry id 2)

        var result = Read(body, out var entityId, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(42, entityId);
        Assert.Equal(6, result.Length);
        Assert.Equal(19, result[5].Index);
        Assert.Equal(2, result[5].Value);
    }

    [Fact]
    public void ThePoseSerializerValueIsCapturedForAnimationRouting()
    {
        // Entity.DATA_POSE is index 6 with the POSE serializer (20); EMERGING is ordinal 13.
        var body = Body(
            b => { Item(b, 6, 20); b.AddRange(VarInt(13)); });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        var pose = Assert.Single(result);
        Assert.Equal(6, pose.Index);
        Assert.Equal(20, pose.SerializerId);
        Assert.Equal(13, pose.Value);
    }

    [Fact]
    public void AnUnsupportedSerializerStopsTheScanInsteadOfGuessingALength()
    {
        // RESOLVABLE_PROFILE (41) is still unmodelled: its codec nests optional profile fields, so guessing a width
        // would desynchronise everything after it. ITEM_STACK (7) used to sit here and is now decoded.
        var body = Body(
            b => { Item(b, 1, 1); b.AddRange(VarInt(300)); },   // air supply (int)
            b => { Item(b, 7, 41); b.AddRange(VarInt(0)); },    // resolvable profile: not modelled
            b => { Item(b, 19, 28); b.AddRange(VarInt(1)); });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.False(complete);
        Assert.Single(result);
    }

    [Fact]
    public void ATruncatedBodyReportsNeedMoreData()
    {
        var body = new List<byte>();
        body.AddRange(VarInt(PacketIds.PlayClientbound.SetEntityData));
        body.AddRange(VarInt(42));
        Item(body, 19, 28); // variant item with no value and no terminator

        Read(body.ToArray(), out _, out _, out var status);

        Assert.Equal(DecodeStatus.NeedMoreData, status);
    }

    [Fact]
    public void AnotherPacketIdIsRejected()
    {
        var body = new List<byte>();
        body.AddRange(VarInt(PacketIds.PlayClientbound.AddEntity));
        body.AddRange(VarInt(1));
        body.Add(EntityDataPackets.EndMarker);

        Read(body.ToArray(), out _, out _, out var status);

        Assert.Equal(DecodeStatus.Invalid, status);
    }
}
