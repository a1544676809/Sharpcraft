// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Block entity records in the chunk packet (docs/specs/quality/vanilla-equivalence-matrix.md row 6).
/// </summary>
/// <remarks>
/// The reference packs both horizontal coordinates into one byte with X in the high nibble and Z in the low nibble,
/// takes Y as a big-endian short, then a registry id and the NBT tag written straight through with no presence flag.
/// The interesting cases are an X of twelve, whose packed byte is negative, and a tag that is actually present.
/// </remarks>
public sealed class BlockEntityRecordTests
{
    [Fact]
    public void PackedXzKeepsXInTheHighNibbleEvenWhenTheByteIsNegative()
    {
        // X 12, Z 11 packs to 0xCB, which is negative as a signed byte.
        var body = ChunkPacketTests.BuildChunkPacketForTests(
            packedXz: 0xCB,
            blockEntityY: 190,
            blockEntityTypeId: 7);

        Assert.Equal(
            DecodeStatus.Success,
            ChunkPacketParser.TryRead(body, sectionCount: 2, minSectionY: -4, airState: 0, out var parsed));

        var record = Assert.Single(parsed!.BlockEntities);
        Assert.Equal(12, record.LocalX);
        Assert.Equal(11, record.LocalZ);
        Assert.Equal(190, record.Y);
        Assert.Equal(7, record.TypeId);
    }

    [Fact]
    public void APresentTagIsWalkedAndTheRecordStillLands()
    {
        // The network root is unnamed: TAG_Compound straight into its entries, one TAG_Int named "X" holding 100, then End.
        var tag = new byte[] { 0x0A, 0x03, 0x00, 0x01, (byte)'X', 0x00, 0x00, 0x00, 0x64, 0x00 };
        var body = ChunkPacketTests.BuildChunkPacketForTests(packedXz: 0x12, blockEntityY: 65, blockEntityTypeId: 3, blockEntityTag: tag);

        var status = ChunkPacketParser.TryRead(body, sectionCount: 2, minSectionY: -4, airState: 0, out var parsed);

        Assert.True(status == DecodeStatus.Success, $"chunk did not parse: {status} / {ChunkPacketParser.LastFailure}");
        var record = Assert.Single(parsed!.BlockEntities);
        Assert.Equal(1, record.LocalX);
        Assert.Equal(2, record.LocalZ);
        Assert.Equal(65, record.Y);
        Assert.Equal(3, record.TypeId);
    }
}
