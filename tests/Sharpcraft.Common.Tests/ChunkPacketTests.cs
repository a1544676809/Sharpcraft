// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Chunk packet and registry data parsing (docs/specs/network/protocol-phases.md 5.1-5.2).
/// Fixtures are built with the same writer helpers, so a layout mistake shows up as a
/// round-trip failure rather than silently passing.
/// </summary>
public sealed class ChunkPacketTests
{
    [Fact]
    public void ChunkPacketFillsSectionsAndHeightmaps()
    {
        var body = BuildChunkPacket(
            chunkX: 3,
            chunkZ: -2,
            sectionStates: [0, 7],
            heightmaps: [(0, new long[] { 0b1010L, 0b0101L })]);

        Assert.Equal(
            DecodeStatus.Success,
            ChunkPacketParser.TryRead(body, sectionCount: 2, minSectionY: -4, airState: 0, out var parsed));

        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.Chunk.ChunkX);
        Assert.Equal(-2, parsed.Chunk.ChunkZ);
        Assert.True(parsed.Chunk.Loaded);
        Assert.Equal(2, parsed.Chunk.SectionCount);

        // Section -4 holds air, section -3 holds state 7 in every block.
        Assert.Equal(0, parsed.Chunk.GetBlockState(0, -64, 0));
        Assert.Equal(7, parsed.Chunk.GetBlockState(0, -48, 0));
        Assert.Equal(7, parsed.Chunk.GetSection(-3)!.Get(15, 15, 15));

        // Non-air count is derived from the container.
        Assert.Equal(0, parsed.Chunk.GetSection(-4)!.NonAirCount);
        Assert.Equal(ChunkSection.Volume, parsed.Chunk.GetSection(-3)!.NonAirCount);

        Assert.True(parsed.Heightmaps.ContainsKey(0));
        Assert.Equal([0b1010L, 0b0101L], parsed.Heightmaps[0]);
        Assert.Equal(1, parsed.BlockEntityCount);
        Assert.Equal(2, parsed.Light.SkyUpdates.Count);
        Assert.Empty(parsed.Light.BlockUpdates);
        Assert.True(LightData.IsSet(parsed.Light.SkyMask, 0));
        Assert.True(LightData.IsSet(parsed.Light.SkyMask, 1));
        Assert.False(LightData.IsSet(parsed.Light.SkyMask, 2));
        Assert.Equal(2, LightData.CountSet(parsed.Light.SkyMask));
        Assert.Equal(2 * LightDataCodec.LightArrayBytes, parsed.Light.ByteCount);
    }

    [Fact]
    public void ChunkPacketRejectsOtherPacketIds()
    {
        var body = BuildChunkPacket(0, 0, [0], []);
        body[0] = PacketIds.PlayClientbound.Login;

        Assert.Equal(DecodeStatus.Invalid, ChunkPacketParser.TryRead(body, 1, 0, 0, out _));
    }

    [Fact]
    public void TruncatedChunkBufferIsReportedAsNeedMoreData()
    {
        var body = BuildChunkPacket(0, 0, [0, 0], []);

        // Chop the tail: the buffer length still claims more than is present.
        Assert.Equal(DecodeStatus.NeedMoreData, ChunkPacketParser.TryRead(body.AsSpan(0, body.Length - 4), 2, 0, 0, out _));
    }

    [Fact]
    public void InvalidSectionCountIsRejected()
    {
        var body = BuildChunkPacket(0, 0, [0], []);

        Assert.Equal(DecodeStatus.Invalid, ChunkPacketParser.TryRead(body, 0, 0, 0, out _));
    }

    [Fact]
    public void PalettedCodecReadsLocalPalette()
    {
        // size 4, bits 2, palette [0, 3, 5, 7]; entries (0,1,2,3) packed into one long.
        var packed = 0L | (1L << 2) | (2L << 4) | (3L << 6);
        var body = BuildContainer(2, [0, 3, 5, 7], [packed]);
        var reader = new PayloadReader(body);

        Assert.Equal(
            DecodeStatus.Success,
            PalettedContainerCodec.TryRead(ref reader, 4, PalettedContainerCodec.BlockStateMaxLocalBits, out var container));

        Assert.Equal(2, container.BitsPerEntry);
        Assert.False(container.IsDirect);
        Assert.Equal([0, 3, 5, 7], container.Palette);
        Assert.Equal(0, container.Get(0));
        Assert.Equal(3, container.Get(1));
        Assert.Equal(5, container.Get(2));
        Assert.Equal(7, container.Get(3));
        Assert.Equal(reader.Position, body.Length);
    }

    [Fact]
    public void PalettedCodecReadsDirectValues()
    {
        // bits 9 exceeds the block state palette ceiling, so values are global ids.
        const int bits = 9;
        const int size = 3;
        var packed = 10L | (20L << bits) | (30L << (bits * 2));
        var body = BuildContainer(bits, [], [packed]);
        var reader = new PayloadReader(body);

        Assert.Equal(
            DecodeStatus.Success,
            PalettedContainerCodec.TryRead(ref reader, size, PalettedContainerCodec.BlockStateMaxLocalBits, out var container));

        Assert.True(container.IsDirect);
        Assert.Equal(10, container.Get(0));
        Assert.Equal(20, container.Get(1));
        Assert.Equal(30, container.Get(2));
    }

    [Fact]
    public void RegistryDataParsesEntriesWithAndWithoutPayload()
    {
        var payload = new NbtCompound().Set("min_y", new NbtInt(-64)).Set("height", new NbtInt(384));

        var body = BuildRegistryData(
            "minecraft:dimension_type",
            [
                ("minecraft:overworld", payload),
                ("minecraft:the_nether", null)
            ]);

        Assert.Equal(DecodeStatus.Success, RegistryDataPackets.TryRead(body, out var registryId, out var entries));
        Assert.Equal("minecraft:dimension_type", registryId.ToString());
        Assert.Equal(2, entries.Count);
        Assert.Equal("minecraft:overworld", entries[0].Id.ToString());
        Assert.NotNull(entries[0].Data);
        Assert.Null(entries[1].Data);

        var info = DimensionTypeInfo.TryRead(entries[0].Data);
        Assert.NotNull(info);
        Assert.Equal(-64, info!.MinY);
        Assert.Equal(384, info.Height);
        Assert.Equal(24, info.SectionCount);
        Assert.Equal(-4, info.MinSectionY);
    }

    [Fact]
    public void RegistryDataRejectsWrongPacketId()
    {
        var body = BuildRegistryData("minecraft:dimension_type", [("minecraft:overworld", null)]);
        body[0] = PacketIds.ConfigurationClientbound.RegistryData + 1;

        Assert.Equal(DecodeStatus.Invalid, RegistryDataPackets.TryRead(body, out _, out _));
    }

    [Fact]
    public void DynamicRegistriesResolvePositionalIds()
    {
        var registries = new DynamicRegistries();
        var key = Identifier.Parse("minecraft:dimension_type");

        registries.Apply(key, [
            new DynamicRegistries.Entry(Identifier.Parse("minecraft:overworld"), null),
            new DynamicRegistries.Entry(Identifier.Parse("minecraft:the_nether"), null)
        ]);

        Assert.Equal(0, registries.IdOf(key, Identifier.Parse("minecraft:overworld")));
        Assert.Equal(1, registries.IdOf(key, Identifier.Parse("minecraft:the_nether")));
        Assert.Null(registries.IdOf(key, Identifier.Parse("minecraft:the_end")));
        Assert.Equal("minecraft:the_nether", registries.ById(key, 1)!.Value.Id.ToString());
        Assert.Null(registries.ById(key, 2));
        Assert.Equal(2, registries.TotalEntries);
    }

    [Fact]
    public void DimensionTypeRejectsIncompletePayload()
    {
        Assert.Null(DimensionTypeInfo.TryRead(null));
        Assert.Null(DimensionTypeInfo.TryRead(new NbtCompound().Set("height", new NbtInt(384))));
        Assert.Null(DimensionTypeInfo.TryRead(new NbtCompound().Set("min_y", new NbtInt(0)).Set("height", new NbtInt(300))));
    }

    private static byte[] BuildChunkPacket(
        int chunkX,
        int chunkZ,
        int[] sectionStates,
        (int Type, long[] Values)[] heightmaps)
    {
        // Section payload: states container then biomes container, both "single value".
        var sectionBuffer = new List<byte>();
        foreach (var state in sectionStates)
        {
            // non-air count and fluid count are raw unsigned shorts.
            var nonEmpty = state == 0 ? 0 : ChunkSection.Volume;
            sectionBuffer.Add((byte)(nonEmpty >> 8));
            sectionBuffer.Add((byte)nonEmpty);
            sectionBuffer.Add(0);
            sectionBuffer.Add(0);
            sectionBuffer.Add(0);                       // bits = 0
            sectionBuffer.AddRange(VarIntBytes(state)); // single block state
            sectionBuffer.Add(0);                       // biome bits = 0
            sectionBuffer.AddRange(VarIntBytes(1));     // single biome
        }

        var buffer = new byte[16 + (4 * LightDataCodec.LightArrayBytes) + 512];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.PlayClientbound.LevelChunkWithLight);
        writer.WriteInt(chunkX);
        writer.WriteInt(chunkZ);
        writer.WriteVarInt(heightmaps.Length);
        foreach (var (type, values) in heightmaps)
        {
            writer.WriteVarInt(type);
            writer.WriteVarInt(values.Length);
            foreach (var value in values)
            {
                writer.WriteLong(value);
            }
        }

        var sectionBytes = sectionBuffer.ToArray();
        writer.WriteByteArray(sectionBytes);

        // One block entity without a tag: the tag is optional (bool flag first).
        writer.WriteVarInt(1);
        writer.WriteByte(0x21);                          // packed XZ
        writer.WriteRaw([0x00, 0x41]);                   // Y as a big-endian short
        writer.WriteVarInt(0);                           // block entity type id
        writer.WriteBool(false);                         // no tag

        // Light: four masks encoded as length-prefixed byte arrays (a Java BitSet.toByteArray),
        // then the sky and block light array lists.
        var oneSectionMask = new byte[] { 0b0000_0011 };   // sections 0 and 1
        writer.WriteByteArray(oneSectionMask);             // sky mask
        writer.WriteByteArray(oneSectionMask);             // block mask
        writer.WriteByteArray([]);                         // empty sky mask
        writer.WriteByteArray([]);                         // empty block mask
        writer.WriteVarInt(2);
        writer.WriteByteArray(new byte[LightDataCodec.LightArrayBytes]);
        writer.WriteByteArray(new byte[LightDataCodec.LightArrayBytes]);
        writer.WriteVarInt(0);

        return buffer.AsSpan(0, writer.Position).ToArray();
    }

    private static byte[] BuildContainer(int bits, int[] palette, long[] longs)
    {
        var buffer = new byte[256];
        var writer = new PayloadWriter(buffer);
        writer.WriteByte((byte)bits);
        if (bits > 0 && bits <= PalettedContainerCodec.BlockStateMaxLocalBits)
        {
            writer.WriteVarInt(palette.Length);
            foreach (var entry in palette)
            {
                writer.WriteVarInt(entry);
            }
        }

        foreach (var value in longs)
        {
            writer.WriteLong(value);
        }

        return buffer.AsSpan(0, writer.Position).ToArray();
    }

    private static byte[] BuildRegistryData(string registryKey, (string Id, NbtCompound? Data)[] entries)
    {
        var buffer = new byte[4096];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(PacketIds.ConfigurationClientbound.RegistryData);
        writer.WriteString(registryKey, 32767);
        writer.WriteVarInt(entries.Length);

        foreach (var (id, data) in entries)
        {
            writer.WriteString(id, 32767);
            writer.WriteBool(data is not null);
            if (data is not null)
            {
                // Payload follows its entry immediately; the length is implied by the NBT structure.
                writer.WriteRaw(NbtWriter.WriteNetworkRoot(data));
            }
        }

        return buffer.AsSpan(0, writer.Position).ToArray();
    }

    private static byte[] VarIntBytes(int value)
    {
        var buffer = new byte[5];
        var written = VarInt.Write(buffer, value);
        return buffer.AsSpan(0, written).ToArray();
    }
}
