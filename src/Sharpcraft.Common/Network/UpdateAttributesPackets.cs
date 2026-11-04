// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Registries;

namespace Sharpcraft.Common.Network;

/// <summary>One synced attribute value (already combined base + modifiers).</summary>
public readonly record struct AttributeValue(int AttributeId, float Value);

/// <summary>
/// Decodes the play <c>update_attributes</c> packet (clientbound 134): entity id plus attribute snapshots
/// (attribute id, base double, modifier list (id, amount, operation)).
/// </summary>
public static class UpdateAttributesPackets
{
    private static readonly int ScaleAttribute = AttributeTypes.GetId("scale");

    /// <summary>Reads every attribute value into <paramref name="values"/>.</summary>
    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        Span<AttributeValue> values,
        out int count,
        out int entityId)
    {
        count = 0;
        entityId = 0;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (packetId != PacketIds.PlayClientbound.UpdateAttributes)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadVarInt(out entityId) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var attributeCount) != DecodeStatus.Success ||
            attributeCount < 0)
        {
            return DecodeStatus.NeedMoreData;
        }

        for (var i = 0; i < attributeCount; i++)
        {
            if (reader.TryReadVarInt(out var attributeId) != DecodeStatus.Success ||
                reader.TryReadDouble(out var baseValue) != DecodeStatus.Success ||
                reader.TryReadVarInt(out var modifierCount) != DecodeStatus.Success ||
                modifierCount < 0)
            {
                return DecodeStatus.NeedMoreData;
            }

            double add = 0;
            double multipliedBase = 0;
            double multipliedTotal = 0;
            for (var m = 0; m < modifierCount; m++)
            {
                if (reader.TryReadString(out _) != DecodeStatus.Success ||
                    reader.TryReadDouble(out var amount) != DecodeStatus.Success ||
                    reader.TryReadVarInt(out var operation) != DecodeStatus.Success)
                {
                    return DecodeStatus.NeedMoreData;
                }

                switch (operation)
                {
                    case 0:
                        add += amount;
                        break;
                    case 1:
                        multipliedBase += amount;
                        break;
                    case 2:
                        multipliedTotal += amount;
                        break;
                }
            }

            var value = (float)((baseValue + add) * (1 + multipliedBase) * (1 + multipliedTotal));
            if (count < values.Length)
            {
                values[count] = new AttributeValue(attributeId, value);
            }

            count++;
        }

        return DecodeStatus.Success;
    }

    /// <summary>Convenience overload returning only the SCALE value (default 1 when absent).</summary>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int entityId, out float scale)
    {
        Span<AttributeValue> values = stackalloc AttributeValue[128];
        var status = TryRead(body, values, out var count, out entityId);
        scale = 1f;
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        for (var i = 0; i < count && i < values.Length; i++)
        {
            if (values[i].AttributeId == ScaleAttribute)
            {
                scale = values[i].Value;
            }
        }

        return DecodeStatus.Success;
    }
}
