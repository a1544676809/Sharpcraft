// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
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
    public void UnknownComponentIsReportedOpaqueAndStopsParsing()
    {
        var body = Body(
            b =>
            {
                b.Add(0x80);                 // mainhand with continuation
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(123));
                b.AddRange(VarInt(1));       // one added component
                b.AddRange(VarInt(98));      // component id 98 is not modelled
                b.AddRange(VarInt(0));       // no removals
            },
            b =>
            {
                b.Add(7);                    // never reached because the patch payload is unknown
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.False(complete);
        Assert.Single(result);
        Assert.True(result[0].Components.Opaque);
    }

    [Fact]
    public void ChargedProjectilesAndDamageAreDecoded()
    {
        var charged = DataComponentTypes.GetId("charged_projectiles");
        var damage = DataComponentTypes.GetId("damage");
        Assert.True(charged >= 0);
        Assert.True(damage >= 0);

        var body = Body(b =>
        {
            b.Add(0);                        // mainhand, last slot
            b.AddRange(VarInt(1));
            b.AddRange(VarInt(1));           // crossbow
            b.AddRange(VarInt(1));           // one added component (charged_projectiles)
            b.AddRange(VarInt(0));           // no removed components
            b.AddRange(VarInt(charged));
            b.AddRange(VarInt(1));           // one projectile
            b.AddRange(VarInt(262));         // arrow item id
            b.AddRange(VarInt(1));           // count
            b.AddRange(VarInt(0));           // nested patch: 0 added
            b.AddRange(VarInt(0));           // nested patch: 0 removed
        });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.True(result[0].Components.HasChargedProjectiles);
    }

    [Fact]
    public void EnchantmentsComponentYieldsQuickChargeLevel()
    {
        var enchantments = DataComponentTypes.GetId("enchantments");
        var quickCharge = EnchantmentTypes.GetId("quick_charge");
        Assert.True(enchantments >= 0 && quickCharge >= 0);

        var body = Body(b =>
        {
            b.Add(0);
            b.AddRange(VarInt(1));
            b.AddRange(VarInt(1));           // crossbow
            b.AddRange(VarInt(1));           // one added component
            b.AddRange(VarInt(0));           // no removals
            b.AddRange(VarInt(enchantments));
            b.AddRange(VarInt(1));           // one enchantment
            b.AddRange(VarInt(quickCharge));
            b.AddRange(VarInt(2));           // quick_charge II
        });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, result[0].Components.QuickChargeLevel);
    }

    [Fact]
    public void TextComponentValueIsSkippedAndParsingContinues()
    {
        var customName = DataComponentTypes.GetId("custom_name");
        Assert.True(customName >= 0);

        var body = Body(
            b =>
            {
                b.Add(0x80);                 // mainhand with continuation
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));       // one added component
                b.AddRange(VarInt(0));       // no removals
                b.AddRange(VarInt(customName));
                // Text component as one anonymous NBT String tag: type 8, ushort length 3, "abc".
                b.Add(8);
                b.Add(0);
                b.Add(3);
                b.Add((byte)'a');
                b.Add((byte)'b');
                b.Add((byte)'c');
            },
            b =>
            {
                b.Add(7);                    // offhand, last slot
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, result.Length);
        Assert.False(result[0].Components.Opaque);
    }

    [Fact]
    public void AttributeModifiersAreSkippedAndParsingContinues()
    {
        var attributeModifiers = DataComponentTypes.GetId("attribute_modifiers");
        Assert.True(attributeModifiers >= 0);

        var body = Body(
            b =>
            {
                b.Add(0x80);
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));       // one added component
                b.AddRange(VarInt(0));       // no removals
                b.AddRange(VarInt(attributeModifiers));
                b.AddRange(VarInt(1));       // one entry
                b.AddRange(VarInt(0));       // attribute id
                b.AddRange(VarInt(1));       // modifier id length
                b.Add((byte)'x');
                b.AddRange(new byte[8]);     // amount 0.0 (big-endian double)
                b.AddRange(VarInt(0));       // operation
                b.AddRange(VarInt(0));       // slot group
                b.AddRange(VarInt(0));       // display: default (no payload)
            },
            b =>
            {
                b.Add(7);
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, result.Length);
        Assert.False(result[0].Components.Opaque);
    }

    [Fact]
    public void TooltipDisplayIsSkippedAndParsingContinues()
    {
        var tooltip = DataComponentTypes.GetId("tooltip_display");
        Assert.True(tooltip >= 0);

        var body = Body(
            b =>
            {
                b.Add(0x80);
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));       // one added component
                b.AddRange(VarInt(0));       // no removals
                b.AddRange(VarInt(tooltip));
                b.Add(1);                    // hide tooltip
                b.AddRange(VarInt(1));       // one hidden component
                b.AddRange(VarInt(3));       // damage component id
            },
            b =>
            {
                b.Add(7);
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void DyedColorAndTrimAreSkippedAndParsingContinues()
    {
        var dyed = DataComponentTypes.GetId("dyed_color");
        var trim = DataComponentTypes.GetId("trim");
        Assert.True(dyed >= 0 && trim >= 0);

        var body = Body(
            b =>
            {
                b.Add(0x80);
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(1));
                b.AddRange(VarInt(2));       // two added components
                b.AddRange(VarInt(0));       // no removals
                b.AddRange(VarInt(dyed));
                b.AddRange(new byte[4]);     // rgb int
                b.AddRange(VarInt(trim));
                b.AddRange(VarInt(0));       // trim material
                b.AddRange(VarInt(0));       // trim pattern
            },
            b =>
            {
                b.Add(7);
                b.AddRange(VarInt(0));
            });

        var result = Read(body, out _, out var complete, out var status);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.True(complete);
        Assert.Equal(2, result.Length);
        Assert.False(result[0].Components.Opaque);
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
