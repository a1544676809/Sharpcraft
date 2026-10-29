// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>One block change from the server.</summary>
/// <param name="X">Block x.</param>
/// <param name="Y">Block y.</param>
/// <param name="Z">Block z.</param>
/// <param name="State">New block state id.</param>
public readonly record struct BlockChange(int X, int Y, int Z, int State);

/// <summary>
/// Parses the two packets that change blocks after the world has been sent
/// (docs/specs/network/play-movement.md 7.17). A single block arrives as a packed position plus a state id; a
/// multi-block update arrives as a section position, a count and packed entries whose low twelve bits are the
/// local offset and whose remaining bits are the state.
/// </summary>
public static class BlockUpdatePackets
{
    /// <summary>Parses a single block update by its body, everything after the id.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="change">Parsed change when successful.</param>
    public static DecodeStatus TryReadBlockUpdate(ReadOnlySpan<byte> body, out BlockChange change)
    {
        change = default;
        var reader = new PayloadReader(body);

        if (reader.TryReadLong(out var packed) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var state) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        var (x, y, z) = BlockPositionCodec.UnpackBlockPosition(packed);
        change = new BlockChange(x, y, z, state);
        return DecodeStatus.Success;
    }

    /// <summary>Parses a multi-block section update by its body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="changes">Parsed changes; empty when the packet carries none.</param>
    public static DecodeStatus TryReadSectionBlocksUpdate(ReadOnlySpan<byte> body, out BlockChange[] changes)
    {
        changes = [];
        var reader = new PayloadReader(body);

        if (reader.TryReadLong(out var packedSection) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var count) != DecodeStatus.Success ||
            count < 0)
        {
            return DecodeStatus.NeedMoreData;
        }

        var (sectionX, sectionY, sectionZ) = BlockPositionCodec.UnpackSectionPosition(packedSection);
        var result = new BlockChange[count];

        for (var index = 0; index < count; index++)
        {
            if (reader.TryReadVarLong(out var entry) != DecodeStatus.Success)
            {
                return DecodeStatus.NeedMoreData;
            }

            var (localX, localY, localZ) = BlockPositionCodec.UnpackSectionRelative((int)(entry & 4095L));
            result[index] = new BlockChange(
                (sectionX * 16) + localX,
                (sectionY * 16) + localY,
                (sectionZ * 16) + localZ,
                (int)(entry >> 12));
        }

        changes = result;
        return DecodeStatus.Success;
    }
}
