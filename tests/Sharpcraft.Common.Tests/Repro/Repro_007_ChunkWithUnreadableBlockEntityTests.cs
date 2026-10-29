// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Repro;

/// <summary>
/// A chunk whose block entity payload cannot be walked must still yield its terrain
/// (docs/specs/network/play-movement.md 7.28).
/// </summary>
/// <remarks>
/// This reproduces a live defect: the server sent chunks containing a block entity we could not parse, the whole
/// packet was rejected, and the client rendered a permanent hole surrounded by loaded terrain. The fix keeps the
/// chunk and drops only the block entity and light data.
/// </remarks>
public sealed class Repro_007_ChunkWithUnreadableBlockEntityTests
{
    [Fact]
    public void AnUnreadableBlockEntityDoesNotDiscardTheTerrain()
    {
        var body = BuildChunkWithBrokenBlockEntity();

        var status = ChunkPacketParser.TryRead(body, sectionCount: 24, minSectionY: -64, airState: 0, out var parsed);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.NotNull(parsed);
        Assert.Equal(24, parsed!.Chunk.SectionCount);
        Assert.True(parsed.BlockEntitiesSkipped, "the block entity should have been reported as skipped");
    }

    [Fact]
    public void AChunkWithoutBlockEntitiesStillParsesNormally()
    {
        var body = BuildChunk(blockEntityCount: 0, includeBrokenEntity: false);

        var status = ChunkPacketParser.TryRead(body, sectionCount: 24, minSectionY: -64, airState: 0, out var parsed);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.NotNull(parsed);
        Assert.False(parsed.BlockEntitiesSkipped);
    }

    private static byte[] BuildChunkWithBrokenBlockEntity() =>
        BuildChunk(blockEntityCount: 1, includeBrokenEntity: true);

    private static byte[] BuildChunk(int blockEntityCount, bool includeBrokenEntity)
    {
        // 24 empty sections: non-air count, fluid count, then a single-valued block container and biome container.
        var sections = new byte[24 * 8];
        for (var section = 0; section < 24; section++)
        {
            var offset = section * 8;
            BinaryPrimitives.WriteInt16BigEndian(sections.AsSpan(offset), 0);
            BinaryPrimitives.WriteInt16BigEndian(sections.AsSpan(offset + 2), 0);
            sections[offset + 4] = 0; // block states: bits 0
            sections[offset + 5] = 0; // single state: air
            sections[offset + 6] = 0; // biomes: bits 0
            sections[offset + 7] = 0; // single biome
        }

        var buffer = new List<byte>();
        WriteVarInt(buffer, PacketIds.PlayClientbound.LevelChunkWithLight);
        WriteInt(buffer, 0);   // chunk x
        WriteInt(buffer, 0);   // chunk z
        WriteVarInt(buffer, 0); // no heightmaps
        WriteVarInt(buffer, sections.Length);
        buffer.AddRange(sections);

        WriteVarInt(buffer, blockEntityCount);
        if (includeBrokenEntity)
        {
            buffer.Add(0);            // packed xz
            buffer.Add(0);
            buffer.Add(0);            // y
            WriteVarInt(buffer, 1);   // block entity type
            buffer.Add(0x02);         // the payload opens with a Short tag, so it is not a compound
        }
        else
        {
            // No block entities, so the light section is present: four empty byte arrays and two empty lists.
            for (var mask = 0; mask < 6; mask++)
            {
                buffer.Add(0);
            }
        }

        return [.. buffer];
    }

    private static void WriteVarInt(List<byte> buffer, int value)
    {
        var remaining = (uint)value;
        while (remaining >= 0x80)
        {
            buffer.Add((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        buffer.Add((byte)remaining);
    }

    private static void WriteInt(List<byte> buffer, int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        buffer.AddRange(bytes);
    }
}
