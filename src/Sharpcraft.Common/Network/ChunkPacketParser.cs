// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.World;

namespace Sharpcraft.Common.Network;

/// <summary>A chunk decoded from <c>level_chunk_with_light</c>.</summary>
/// <param name="Chunk">Chunk with its sections filled in.</param>
/// <param name="Heightmaps">Heightmap type id to raw long array.</param>
/// <param name="BlockEntityCount">Number of block entities attached to the chunk.</param>
public sealed record ParsedChunk(
    LevelChunk Chunk,
    IReadOnlyDictionary<int, long[]> Heightmaps,
    int BlockEntityCount,
    LightData Light,
    bool BlockEntitiesSkipped = false);

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
        /// <summary>
    /// Description of why the last parse failed, with the reader position. Diagnostic only: a chunk that never
    /// decodes leaves a permanent hole, so the reason has to be visible
    /// (docs/specs/network/play-movement.md 7.28).
    /// </summary>
    public static string LastFailure { get; private set; } = string.Empty;

    /// <summary>True when a chunk was kept without its block entities because their payload was unreadable.</summary>
    public static bool BlockEntitiesSkipped { get; private set; }


    private static DecodeStatus Fail(string stage, int position)
    {
        LastFailure = $"{stage} at {position}";
        return DecodeStatus.Invalid;
    }

public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        int sectionCount,
        int minSectionY,
        int airState,
        out ParsedChunk? parsed)
    {
        parsed = null;
        var blockEntitiesSkipped = false;

        if (sectionCount <= 0)
        {
            return Fail("section-count", 0);
        }

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.LevelChunkWithLight)
        {
            return Fail("packet-id", reader.Position);
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
            return Fail("heightmap-count", reader.Position);
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
                return Fail("heightmap-longs", reader.Position);
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
            return Fail("block-entity-count", reader.Position);
        }

        var blockEntitiesBroken = false;
        for (var i = 0; i < blockEntityCount; i++)
        {
            status = SkipBlockEntity(body, ref reader);
            if (status != DecodeStatus.Success)
            {
                // A block entity whose payload we cannot walk desynchronises everything after it, including the
                // light section, but the terrain itself is already decoded. Dropping the whole chunk turns a
                // packet we only partly understand into a permanent hole in the world, so the chunk is kept
                // without block entity data and without light instead
                // (docs/specs/network/play-movement.md 7.28).
                blockEntitiesSkipped = true;
                blockEntitiesBroken = true;
                break;
            }
        }

        // Light arrays follow the block entities; they are stored raw for now.
        LightData light;

        if (blockEntitiesBroken)
        {
            light = LightData.Empty;
        }
        else
        {
            status = LightDataCodec.TryRead(ref reader, out light);
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
        parsed = new ParsedChunk(chunk, heightmaps, blockEntityCount, light, blockEntitiesSkipped);
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

        // No "payload follows" flag exists in 26.3: OPTIONAL_COMPOUND_TAG delegates straight to writeNbt, which
        // encodes an absent tag as a lone End byte. Reading a boolean here shifted every record by one byte,
        // which is what produced the unreadable block entities and the chunks that never appeared
        // (docs/specs/network/play-movement.md 7.28).
        var nbtStart = reader.Position;
        status = NbtReader.TryReadNetworkRoot(body[reader.Position..], out _, out var consumed);
        if (status != DecodeStatus.Success)
        {
            var head = string.Join(' ', body.Slice(nbtStart, Math.Min(16, body.Length - nbtStart)).ToArray()
                .Select(value => value.ToString("X2")));
            LastFailure = $"block entity nbt at {nbtStart} head={head} {NbtReader.LastFailure}";
            return status;
        }

        reader.Skip(consumed);
        return DecodeStatus.Success;
    }
}
