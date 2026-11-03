// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>One equipment slot as sent by <c>set_equipment</c>; an empty stack has <see cref="Count"/> 0 and no item id.</summary>
public readonly record struct EquipmentItem(int Slot, int ItemId, int Count, bool HasDataComponents = false)
{
    /// <summary>True when the slot was explicitly emptied.</summary>
    public bool IsEmpty => Count <= 0;
}

/// <summary>
/// Decodes the play <c>set_equipment</c> packet (clientbound 104).
/// </summary>
/// <remarks>
/// Layout: VarInt entity id, then repeated (byte slot | 0x80 continuation, optional ItemStack). This first stage reads
/// only stacks with an empty <c>DataComponentPatch</c>; if a non-empty patch is met the item is still reported with
/// <see cref="EquipmentItem.HasDataComponents"/> = true and parsing stops, because component payloads have no generic
/// skip length.
/// </remarks>
public static class SetEquipmentPackets
{
    public const int MaxSlots = 8;

    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        out int entityId,
        Span<EquipmentItem> items,
        out int count,
        out bool complete)
    {
        entityId = 0;
        count = 0;
        complete = false;

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.SetEquipment)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadVarInt(out entityId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        while (true)
        {
            status = reader.TryReadByte(out var slotByte);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            var continueReading = (slotByte & 0x80) != 0;
            var slot = slotByte & 0x7F;
            status = reader.TryReadVarInt(out var stackCount);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            if (stackCount > 0)
            {
                status = reader.TryReadVarInt(out var itemId);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }

                status = reader.TryReadVarInt(out var added);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }

                status = reader.TryReadVarInt(out var removed);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }

                var hasData = added != 0 || removed != 0;
                if (count < items.Length)
                {
                    items[count++] = new EquipmentItem(slot, itemId, stackCount, hasData);
                }

                if (hasData)
                {
                    // No generic skip length for component payloads: stop rather than desynchronise the stream.
                    return DecodeStatus.Success;
                }
            }
            else if (count < items.Length)
            {
                items[count++] = new EquipmentItem(slot, 0, 0);
            }

            if (!continueReading)
            {
                complete = true;
                return DecodeStatus.Success;
            }
        }
    }
}
