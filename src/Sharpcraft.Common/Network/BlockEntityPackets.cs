// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// The single block entity update packet (play clientbound 6).
/// </summary>
/// <remarks>
/// The reference sends a packed block position, the block entity type registry id, then the NBT tag of the update. The
/// position packs X in the top 26 bits, Z in the next 26 and Y in the low 12, each sign extended.
/// </remarks>
public static class BlockEntityPackets
{
    /// <summary>Decodes one block entity update.</summary>
    /// <param name="body">Packet body including the id.</param>
    /// <param name="blockEntity">The decoded update, or default when the packet is malformed.</param>
    /// <returns>Decode status.</returns>
    public static DecodeStatus TryReadBlockEntityData(ReadOnlySpan<byte> body, out BlockEntityUpdate blockEntity)
    {
        blockEntity = default;

        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success ||
            packetId != PacketIds.PlayClientbound.BlockEntityData)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadLong(out var packed) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        var x = (int)(packed >> 38);
        var y = (int)(packed << 52 >> 52);
        var z = (int)(packed << 26 >> 38);

        if (reader.TryReadVarInt(out var typeId) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        var status = NbtReader.TryReadNetworkRoot(body[reader.Position..], out var tag, out _);

        if (status != DecodeStatus.Success)
        {
            return status;
        }

        blockEntity = new BlockEntityUpdate(x, y, z, typeId, tag);
        return DecodeStatus.Success;
    }
}

/// <summary>One block entity update: the absolute block position and the registry id of its type.</summary>
/// <param name="X">Absolute block X.</param>
/// <param name="Y">Absolute block Y.</param>
/// <param name="Z">Absolute block Z.</param>
/// <param name="TypeId">Registry id of the block entity type.</param>
/// <param name="Tag">Optional NBT compound carried with the update.</param>
public readonly record struct BlockEntityUpdate(int X, int Y, int Z, int TypeId, NbtCompound? Tag = null);
