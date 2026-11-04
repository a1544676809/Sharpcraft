// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// One entry of a <c>set_entity_data</c> payload: the accessor index, the serializer that typed it, and, for the
/// integer-valued serializers, the value itself.
/// </summary>
/// <param name="Index">Accessor index inside the entity's synced data (inheritance-chain cumulative).</param>
/// <param name="SerializerId">Registered <c>EntityDataSerializers</c> id, used to skip or interpret the value.</param>
/// <param name="Value">
/// Decoded integer value for the byte/integer serializers; <see cref="NotAnInteger"/> for every other serializer.
/// </param>
public readonly record struct EntityDataItem(int Index, int SerializerId, int Value, long LongValue = long.MinValue, float FloatValue = float.NaN)
{
    /// <summary>Sentinel stored in <see cref="Value"/> when the serializer does not carry an integer.</summary>
    public const int NotAnInteger = int.MinValue;

    /// <summary>Sentinel stored in <see cref="LongValue"/> when the serializer does not carry a long.</summary>
    public const long NotALong = long.MinValue;

    /// <summary>True when <see cref="FloatValue"/> is available.</summary>
    public bool HasFloat => !float.IsNaN(FloatValue);
}

/// <summary>
/// Decodes the play <c>set_entity_data</c> packet (clientbound 101).
/// </summary>
/// <remarks>
/// Vanilla sends only the values that differ from the entity's defaults, so a missing item means "still default";
/// callers must fall back instead of assuming index 0. Values are indexed by the cumulative
/// <c>defineId</c> order across the class hierarchy, described in docs/analysis/render/entity-variants.md 2.1.
/// <para>
/// Serializers we do not model (item stacks, resolvable profiles) make parsing stop early and report
/// <paramref name="complete"/> = false. That is deliberate: guessing a length would desynchronise the rest of the
/// packet, and the caller already has a defined fallback for "variant not present".
/// </para>
/// </remarks>
public static class EntityDataPackets
{
    /// <summary>Terminator byte of the packed item list.</summary>
    public const int EndMarker = 255;

    /// <summary>Upper bound on items, so a corrupt packet cannot grow the caller's span beyond reason.</summary>
    public const int MaxItems = 256;

    /// <summary>Reads the entity id and decoded items.</summary>
    /// <param name="body">Packet body, including the packet id.</param>
    /// <param name="entityId">Runtime entity id.</param>
    /// <param name="items">Destination for decoded items; may be shorter than the packet.</param>
    /// <param name="count">Number of items written.</param>
    /// <param name="complete">False when an unsupported serializer or a full destination stopped the scan.</param>
    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        out int entityId,
        Span<EntityDataItem> items,
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

        if (packetId != PacketIds.PlayClientbound.SetEntityData)
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
            status = reader.TryReadByte(out var rawIndex);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            if (rawIndex == EndMarker)
            {
                complete = true;
                return DecodeStatus.Success;
            }

            status = reader.TryReadVarInt(out var serializerId);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            status = TryReadValue(ref reader, serializerId, out var value, out var longValue, out var floatValue, out var unsupported);
            if (unsupported)
            {
                // Everything after this item is unreadable without the codec; stop and let the caller fall back.
                return DecodeStatus.Success;
            }

            if (status != DecodeStatus.Success)
            {
                return status;
            }

            if (count < items.Length)
            {
                items[count++] = new EntityDataItem(rawIndex, serializerId, value, longValue, floatValue);
            }
            else
            {
                return DecodeStatus.Success;
            }
        }
    }

    /// <summary>
    /// Reads or skips one serializer value.
    /// </summary>
    /// <remarks>
    /// The ids follow the static registration order in <c>EntityDataSerializers</c>. Only the byte/integer family
    /// returns a value; everything else is skipped so the scan can continue to a later index.
    /// </remarks>
    private static DecodeStatus TryReadValue(ref PayloadReader reader, int serializerId, out int value, out long longValue, out float floatValue, out bool unsupported)
    {
        value = EntityDataItem.NotAnInteger;
        longValue = EntityDataItem.NotALong;
        floatValue = float.NaN;
        unsupported = false;
        DecodeStatus status;

        switch (serializerId)
        {
            case 0: // BYTE
                status = reader.TryReadByte(out var b);
                value = b;
                return status;

            case 1: // INT
                status = reader.TryReadVarInt(out value);
                return status;

            case 2: // LONG
                status = reader.TryReadVarLong(out var readLong);
                if (status == DecodeStatus.Success)
                {
                    longValue = readLong;
                }

                return status;

            case 3: // FLOAT
                status = reader.TryReadFloat(out var readFloat);
                if (status == DecodeStatus.Success)
                {
                    floatValue = readFloat;
                }

                return status;

            case 4: // STRING
                return reader.TryReadString(out _, 32767);

            case 5: // COMPONENT (trusted, unnamed network root)
                return SkipNbt(ref reader);

            case 6: // OPTIONAL_COMPONENT
                status = reader.TryReadBool(out var hasComponent);
                return status == DecodeStatus.Success && hasComponent ? SkipNbt(ref reader) : status;

            case 8: // BOOLEAN
                status = reader.TryReadBool(out var flag);
                value = flag ? 1 : 0;
                return status;

            case 9: // ROTATIONS: three floats
                return SkipBytes(ref reader, 12);

            case 10: // BLOCK_POS: packed long
                return SkipBytes(ref reader, 8);

            case 11: // OPTIONAL_BLOCK_POS
                status = reader.TryReadBool(out var hasPos);
                return status == DecodeStatus.Success && hasPos ? SkipBytes(ref reader, 8) : status;

            case 12: // DIRECTION: VarInt id
                return reader.TryReadVarInt(out _);

            case 13: // OPTIONAL_LIVING_ENTITY_REFERENCE: optional UUID
                status = reader.TryReadBool(out var hasReference);
                return status == DecodeStatus.Success && hasReference ? SkipBytes(ref reader, 16) : status;

            case 14: // BLOCK_STATE: VarInt id
                return reader.TryReadVarInt(out _);

            case 15: // OPTIONAL_BLOCK_STATE: VarInt id, zero means empty
                return reader.TryReadVarInt(out _);

            case 16: // PARTICLE: registry VarInt
                return reader.TryReadVarInt(out _);

            case 17: // PARTICLES: count-prefixed registry VarInts
                status = reader.TryReadVarInt(out var particleCount);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }

                for (var i = 0; i < particleCount; i++)
                {
                    status = reader.TryReadVarInt(out _);
                    if (status != DecodeStatus.Success)
                    {
                        return status;
                    }
                }

                return DecodeStatus.Success;

            case 18: // VILLAGER_DATA: type holder, profession holder, level
                status = reader.TryReadVarInt(out value);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }

                for (var i = 0; i < 2; i++)
                {
                    status = reader.TryReadVarInt(out _);
                    if (status != DecodeStatus.Success)
                    {
                        return status;
                    }
                }

                return DecodeStatus.Success;

            case 19: // OPTIONAL_UNSIGNED_INT
                return reader.TryReadVarInt(out _);

            case 20: // POSE: VarInt ordinal (STANDING 0 .. INHALING 17)
                return reader.TryReadVarInt(out value);

            case 21: // CAT_VARIANT
            case 22: // CAT_SOUND_VARIANT
            case 23: // COW_VARIANT
            case 24: // COW_SOUND_VARIANT
            case 25: // WOLF_VARIANT
            case 26: // WOLF_SOUND_VARIANT
            case 27: // FROG_VARIANT
            case 28: // PIG_VARIANT
            case 29: // PIG_SOUND_VARIANT
            case 30: // CHICKEN_VARIANT
            case 31: // CHICKEN_SOUND_VARIANT
            case 32: // ZOMBIE_NAUTILUS_VARIANT
                status = reader.TryReadVarInt(out value);
                return status;

            case 33: // OPTIONAL_GLOBAL_POS: optional identifier + packed long
                status = reader.TryReadBool(out var hasGlobalPos);
                if (status != DecodeStatus.Success || !hasGlobalPos)
                {
                    return status;
                }

                status = reader.TryReadString(out _, 32767);
                return status == DecodeStatus.Success ? SkipBytes(ref reader, 8) : status;

            case 34: // PAINTING_VARIANT
            case 35: // SNIFFER_STATE
            case 36: // ARMADILLO_STATE
            case 37: // COPPER_GOLEM_STATE
            case 38: // WEATHERING_COPPER_STATE
            case 42: // HUMANOID_ARM
            case 43: // DYE_COLOR
                return reader.TryReadVarInt(out _);

            case 39: // VECTOR3
                return SkipBytes(ref reader, 12);

            case 40: // QUATERNION
                return SkipBytes(ref reader, 16);

            default:
                // ITEM_STACK (7), RESOLVABLE_PROFILE (41) and anything newer: not modelled.
                unsupported = true;
                return DecodeStatus.Success;
        }
    }

    private static DecodeStatus SkipBytes(ref PayloadReader reader, int count) => reader.TrySkip(count);

    private static DecodeStatus SkipNbt(ref PayloadReader reader)
    {
        var status = NbtReader.TryReadNetworkRoot(reader.RemainingSpan, out _, out var consumed);
        if (status == DecodeStatus.Success)
        {
            reader.Skip(consumed);
        }

        return status;
    }
}

