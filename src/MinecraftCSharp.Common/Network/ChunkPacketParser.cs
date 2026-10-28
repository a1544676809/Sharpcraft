// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Nbt;
using MinecraftCSharp.Common.World;

namespace MinecraftCSharp.Common.Network;

/// <summary>A chunk decoded from <c>level_chunk_with_light</c>.</summary>
/// <param name="Chunk">Chunk with its sections filled in.</param>
/// <param name="Heightmaps">Heightmap type id to raw long array.</param>
/// <param name="BlockEntityCount">Number of block entities attached to the chunk.</param>
public sealed record ParsedChunk(
    LevelChunk Chunk,
    IReadOnlyDictionary<int, long[]> Heightmaps,
    int BlockEntityCount);

/// <summary>
/// Parses the play-phase <c>level_chunk_with_light</c> packet (id 46)
/// (docs/specs/network/protocol-phases.md 5.2).
/// </summary>
/// <remarks>
/// Section layout of the embedded buffer, per section: block states container, then biomes
/// container. Both end up as fixed-size long arrays with no length prefix, so any misalignment
/// desynchronises the rest of the chunk.
/// </remarks>
public static class ChunkPacketParser
{
    /// <summary>Entries in a block state container.</summary>
    public const int BlockStateCount = ChunkSection.Volume;

    /// <summary>Entries in a biome container (4x4x4).</summary>
    public const int BiomeCount = 64;

    /// <summary>Reads a chunk packet. <paramref name="sectionCount"/> comes from the dimension type.</summary>
    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        int sectionCount,
        int minSectionY,
        int airState,
        out ParsedChunk? parsed)
    {
        parsed = null;

        if (sectionCount <= 0)
        {
            return DecodeStatus.Invalid;
        }

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.LevelChunkWithLight)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadInt(out var chunkX);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadInt(out var chunkZ);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out var heightmapCount);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (heightmapCount < 0 || heightmapCount > 16)
        {
            return DecodeStatus.Invalid;
        }

        var heightmaps = new Dictionary<int, long[]>(heightmapCount);
        for (var i = 0; i < heightmapCount; i++)
        {
            status = reader.TryReadVarInt(out var heightmapType);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            status = reader.TryReadVarInt(out var longCount);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            if (longCount < 0 || longCount > 4096)
            {
                return DecodeStatus.Invalid;
            }

            var values = new long[longCount];
            for (var j = 0; j < longCount; j++)
            {
                status = reader.TryReadLong(out values[j]);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }
            }

            heightmaps[heightmapType] = values;
        }

        status = reader.TryReadByteArray(out var buffer, 1 << 22);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out var blockEntityCount);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (blockEntityCount < 0 || blockEntityCount > 4096)
        {
            return DecodeStatus.Invalid;
        }

        for (var i = 0; i < blockEntityCount; i++)
        {
            status = SkipBlockEntity(body, ref reader);
            if (status != DecodeStatus.Success)
            {
                return status;
            }
        }

        var chunk = new LevelChunk(chunkX, chunkZ, minSectionY, sectionCount);
        var sectionReader = new PayloadReader(buffer);
        for (var i = 0; i < sectionCount; i++)
        {
            // Each section starts with the non-air block count and the fluid count as raw
            // unsigned shorts, before the two containers.
            status = sectionReader.TryReadUnsignedShort(out var nonEmptyBlockCount);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            status = sectionReader.TryReadUnsignedShort(out _);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            status = PalettedContainerCodec.TryRead(
                ref sectionReader, BlockStateCount, PalettedContainerCodec.BlockStateMaxLocalBits, out var states);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            // Biomes are consumed to keep the reader aligned; they are not stored yet.
            status = PalettedContainerCodec.TryRead(
                ref sectionReader, BiomeCount, PalettedContainerCodec.BiomeMaxLocalBits, out _);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            chunk.SetSection(new ChunkSection(states, minSectionY + i, nonEmptyBlockCount));
        }

        chunk.MarkLoaded();
        parsed = new ParsedChunk(chunk, heightmaps, blockEntityCount);
        return DecodeStatus.Success;
    }

    private static DecodeStatus SkipBlockEntity(ReadOnlySpan<byte> body, ref PayloadReader reader)
    {
        // packed XZ, packed Y, block entity type, then an unnamed NBT compound.
        var status = reader.TryReadByte(out _);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadBytes(2, out _);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out _);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = NbtReader.TryReadNetworkRoot(body[reader.Position..], out _, out var consumed);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        reader.Skip(consumed);
        return DecodeStatus.Success;
    }
}
