// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Block update decoding (docs/specs/network/play-movement.md 7.17). The bit layouts are the whole risk here:
/// a wrong shift silently writes the block into a different chunk, so each packing is pinned separately from
/// the packet parsing.
/// </summary>
public sealed class BlockUpdateTests
{
    [Fact]
    public void BlockPositionsRoundTripIncludingNegatives()
    {
        foreach (var (x, y, z) in new[] { (0, 0, 0), (1, 64, -3), (-1000, -64, 2000000), (12, 320, -12) })
        {
            var packed = BlockPositionCodec.PackBlockPosition(x, y, z);
            var (rx, ry, rz) = BlockPositionCodec.UnpackBlockPosition(packed);

            Assert.Equal(x, rx);
            Assert.Equal(y, ry);
            Assert.Equal(z, rz);
        }
    }

    [Fact]
    public void SectionPositionsRoundTripIncludingNegatives()
    {
        foreach (var (x, y, z) in new[] { (0, 0, 0), (3, 4, -2), (-30, -4, 30) })
        {
            var packed = BlockPositionCodec.PackSectionPosition(x, y, z);
            var (rx, ry, rz) = BlockPositionCodec.UnpackSectionPosition(packed);

            Assert.Equal(x, rx);
            Assert.Equal(y, ry);
            Assert.Equal(z, rz);
        }
    }

    [Fact]
    public void TheLocalOffsetPutsXInTheHighBits()
    {
        // SectionPos.sectionRelativeX shifts by eight, so X is the high nibble and Y the low one. Swapping them
        // would scatter block updates across the section.
        var relative = (2 << 8) | 3 | (4 << 4);

        var (x, y, z) = BlockPositionCodec.UnpackSectionRelative(relative);

        Assert.Equal(2, x);
        Assert.Equal(3, y);
        Assert.Equal(4, z);
    }

    [Fact]
    public void ASingleBlockUpdateParses()
    {
        Span<byte> buffer = stackalloc byte[16];
        var writer = new PayloadWriter(buffer);
        writer.WriteLong(BlockPositionCodec.PackBlockPosition(10, 70, -5));
        writer.WriteVarInt(42);

        var status = BlockUpdatePackets.TryReadBlockUpdate(buffer[..writer.Position], out var change);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(10, change.X);
        Assert.Equal(70, change.Y);
        Assert.Equal(-5, change.Z);
        Assert.Equal(42, change.State);
    }

    [Fact]
    public void ASectionUpdateParsesEveryEntry()
    {
        Span<byte> buffer = stackalloc byte[64];
        var writer = new PayloadWriter(buffer);
        writer.WriteLong(BlockPositionCodec.PackSectionPosition(1, 4, -1));
        writer.WriteVarInt(2);

        // Local (2, 3, 4) with state 7, then local (15, 0, 1) with state 9.
        writer.WriteVarLong(((long)7 << 12) | (long)((2 << 8) | 3 | (4 << 4)));
        writer.WriteVarLong(((long)9 << 12) | (long)((15 << 8) | 0 | (1 << 4)));

        var status = BlockUpdatePackets.TryReadSectionBlocksUpdate(buffer[..writer.Position], out var changes);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(2, changes.Length);

        // Section (1, 4, -1) starts at block (16, 64, -16).
        Assert.Equal(new BlockChange(18, 67, -12, 7), changes[0]);
        Assert.Equal(new BlockChange(31, 64, -15, 9), changes[1]);
    }
}
