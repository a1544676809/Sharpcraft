// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Skips one anonymous network-NBT tag. Several item components (text components, lore lines) are transmitted as the NBT
/// form of their codec, and NBT is self-delimiting, so an exact skip keeps the packet in sync without modelling the value.
/// </summary>
internal static class NbtTagReader
{
    private const int MaxDepth = 64;

    /// <summary>Reads and discards one tag whose type byte is next in the stream (network NBT has no root name).</summary>
    public static bool TrySkip(ref PayloadReader reader) =>
        reader.TryReadByte(out var type) == DecodeStatus.Success && TrySkipPayload(ref reader, type, 0);

    private static bool TrySkipPayload(ref PayloadReader reader, byte type, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        switch (type)
        {
            case 0: // End
                return true;
            case 1: // Byte
                return reader.TryReadByte(out _) == DecodeStatus.Success;
            case 2: // Short
                return reader.TryReadUnsignedShort(out _) == DecodeStatus.Success;
            case 3: // Int
                return reader.TryReadInt(out _) == DecodeStatus.Success;
            case 4: // Long
                return reader.TryReadLong(out _) == DecodeStatus.Success;
            case 5: // Float
                return reader.TryReadFloat(out _) == DecodeStatus.Success;
            case 6: // Double
                return reader.TryReadDouble(out _) == DecodeStatus.Success;
            case 7: // Byte array
                return reader.TryReadInt(out var byteCount) == DecodeStatus.Success &&
                       byteCount >= 0 &&
                       reader.TryReadBytes(byteCount, out _) == DecodeStatus.Success;
            case 8: // String
                return TrySkipString(ref reader);
            case 9: // List
                {
                    if (reader.TryReadByte(out var elementType) != DecodeStatus.Success ||
                        reader.TryReadInt(out var length) != DecodeStatus.Success ||
                        length < 0)
                    {
                        return false;
                    }

                    for (var i = 0; i < length; i++)
                    {
                        if (!TrySkipPayload(ref reader, elementType, depth + 1))
                        {
                            return false;
                        }
                    }

                    return true;
                }

            case 10: // Compound
                {
                    while (true)
                    {
                        if (reader.TryReadByte(out var childType) != DecodeStatus.Success)
                        {
                            return false;
                        }

                        if (childType == 0)
                        {
                            return true;
                        }

                        if (!TrySkipString(ref reader) || !TrySkipPayload(ref reader, childType, depth + 1))
                        {
                            return false;
                        }
                    }
                }

            case 11: // Int array
                return reader.TryReadInt(out var intCount) == DecodeStatus.Success &&
                       intCount >= 0 &&
                       reader.TryReadBytes(intCount * 4, out _) == DecodeStatus.Success;
            case 12: // Long array
                return reader.TryReadInt(out var longCount) == DecodeStatus.Success &&
                       longCount >= 0 &&
                       reader.TryReadBytes(longCount * 8, out _) == DecodeStatus.Success;
            default:
                return false;
        }
    }

    private static bool TrySkipString(ref PayloadReader reader)
    {
        if (reader.TryReadUnsignedShort(out var length) != DecodeStatus.Success)
        {
            return false;
        }

        return reader.TryReadBytes(length, out _) == DecodeStatus.Success;
    }
}
