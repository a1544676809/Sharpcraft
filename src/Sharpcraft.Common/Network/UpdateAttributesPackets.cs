// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Registries;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Decodes the play <c>update_attributes</c> packet (clientbound 134) and extracts the SCALE attribute value.
/// Layout: entity id, list of (attribute id, base double, modifier list (id, amount, operation)).
/// </summary>
public static class UpdateAttributesPackets
{
    private static readonly int ScaleAttribute = AttributeTypes.GetId("scale");

    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int entityId, out float scale)
    {
        entityId = 0;
        scale = 1f;

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
                    case 0: // add_value
                        add += amount;
                        break;
                    case 1: // add_multiplied_base
                        multipliedBase += amount;
                        break;
                    case 2: // add_multiplied_total
                        multipliedTotal += amount;
                        break;
                }
            }

            if (attributeId == ScaleAttribute)
            {
                scale = (float)((baseValue + add) * (1 + multipliedBase) * (1 + multipliedTotal));
            }
        }

        return DecodeStatus.Success;
    }
}
