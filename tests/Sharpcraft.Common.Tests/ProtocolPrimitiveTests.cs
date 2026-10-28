// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Protocol primitive tests (M1-b): VarInt/VarLong, frame length prefixes and UTF-8
/// strings, per docs/specs/network/primitives-*.md.
/// </summary>
public sealed class ProtocolPrimitiveTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(255)]
    [InlineData(16383)]
    [InlineData(16384)]
    [InlineData(2097151)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void VarIntRoundTripsAndSizeMatches(int value)
    {
        var buffer = new byte[VarInt.MaxBytes];

        var written = VarInt.Write(buffer, value);
        Assert.Equal(VarInt.GetByteSize(value), written);
        Assert.Equal(DecodeStatus.Success, VarInt.TryRead(buffer[..written], out var decoded, out var consumed));
        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Fact]
    public void NegativeVarIntsAlwaysTakeFiveBytes()
    {
        var buffer = new byte[VarInt.MaxBytes];

        Assert.Equal(5, VarInt.Write(buffer, -1));
        Assert.Equal(5, VarInt.Write(buffer, int.MinValue));
        Assert.Equal(5, VarInt.GetByteSize(-2));
    }

    [Fact]
    public void TruncatedVarIntReportsNeedMoreData()
    {
        // 300 encodes as 0xAC 0x02; give only the first byte.
        var buffer = new byte[VarInt.MaxBytes];
        var written = VarInt.Write(buffer, 300);

        Assert.Equal(DecodeStatus.NeedMoreData, VarInt.TryRead(buffer[..(written - 1)], out _, out _));
        Assert.Throws<InvalidDataException>(() => VarInt.Read(buffer[..(written - 1)], out _));
    }

    [Fact]
    public void OverlongVarIntIsRejected()
    {
        // Six continuation bytes can never be a valid 32-bit VarInt.
        byte[] overlong = [0x80, 0x80, 0x80, 0x80, 0x80, 0x01];

        Assert.Equal(DecodeStatus.Invalid, VarInt.TryRead(overlong, out _, out _));
        Assert.Throws<InvalidDataException>(() => VarInt.Read(overlong, out _));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void VarLongRoundTripsAndSizeMatches(long value)
    {
        var buffer = new byte[VarLong.MaxBytes];

        var written = VarLong.Write(buffer, value);
        Assert.Equal(VarLong.GetByteSize(value), written);
        Assert.Equal(DecodeStatus.Success, VarLong.TryRead(buffer[..written], out var decoded, out var consumed));
        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Fact]
    public void OverlongVarLongIsRejected()
    {
        var overlong = new byte[11];
        overlong.AsSpan().Fill(0x80);
        overlong[10] = 0x01;

        Assert.Equal(DecodeStatus.Invalid, VarLong.TryRead(overlong, out _, out _));
    }

    [Fact]
    public void FramePrefixDescribesBodyLengthWithinThreeBytes()
    {
        var buffer = new byte[PacketFramer.MaxPrefixBytes];

        var written = PacketFramer.WriteLengthPrefix(buffer, 1234);
        Assert.True(written <= PacketFramer.MaxPrefixBytes);
        Assert.Equal(DecodeStatus.Success, PacketFramer.TryReadLengthPrefix(buffer[..written], out var bodyLength, out var consumed));
        Assert.Equal(1234, bodyLength);
        Assert.Equal(written, consumed);
    }

    [Fact]
    public void FrameRejectsBodiesBeyond21Bits()
    {
        var buffer = new byte[PacketFramer.MaxPrefixBytes];

        Assert.Equal((1 << 21) - 1, PacketFramer.MaxBodyLength);
        Assert.Throws<ArgumentOutOfRangeException>(() => PacketFramer.WriteLengthPrefix(buffer, PacketFramer.MaxBodyLength + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PacketFramer.WriteLengthPrefix(buffer, -1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("minecraft:stone")]
    [InlineData("中文测试")]
    [InlineData("emoji 🎮 混合 abc")]
    public void Utf8StringsRoundTrip(string value)
    {
        var buffer = new byte[value.Length * 3 + VarInt.MaxBytes];

        var written = Utf8StringCodec.Write(buffer, value);
        Assert.Equal(DecodeStatus.Success, Utf8StringCodec.TryRead(buffer[..written], out var decoded, out var consumed));
        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Fact]
    public void Utf8LengthPrefixCountsBytesNotCharacters()
    {
        var buffer = new byte[64];

        var written = Utf8StringCodec.Write(buffer, "中");   // 3 UTF-8 bytes, 1 char
        var prefix = VarInt.Read(buffer, out var consumed);

        Assert.Equal(3, prefix);
        Assert.Equal(consumed + 3, written);
    }

    [Fact]
    public void Utf8RejectsTooManyCharactersAndTooManyBytes()
    {
        var justOverCharacters = new string('a', Utf8StringCodec.DefaultMaxLength + 1);
        var buffer = new byte[justOverCharacters.Length + VarInt.MaxBytes];

        Assert.Throws<ArgumentException>(() => Utf8StringCodec.Write(buffer, justOverCharacters));

        // The byte ceiling is (maxLength * 3), and UTF-8 never spends more than 3 bytes per
        // UTF-16 unit, so the character check always fires first; the byte check is defensive.
        var maxChars = new string('中', Utf8StringCodec.DefaultMaxLength);
        var maxBuffer = new byte[(maxChars.Length * 3) + VarInt.MaxBytes];
        var written = Utf8StringCodec.Write(maxBuffer, maxChars);
        Assert.Equal(Utf8StringCodec.DefaultMaxLength * 3, VarInt.Read(maxBuffer, out _));
        Assert.True(written > Utf8StringCodec.DefaultMaxLength);
    }

    [Fact]
    public void Utf8TruncationAndOversizeFramesAreDistinguished()
    {
        var buffer = new byte[32];
        var written = Utf8StringCodec.Write(buffer, "hello");

        Assert.Equal(DecodeStatus.NeedMoreData, Utf8StringCodec.TryRead(buffer[..(written - 1)], out _, out _));

        // Length prefix claims more than the remaining buffer holds.
        var lying = new byte[8];
        var prefix = VarInt.Write(lying, 100);
        Assert.Equal(DecodeStatus.NeedMoreData, Utf8StringCodec.TryRead(lying[..prefix], out _, out _));
    }

    [Fact]
    public void Utf8RejectsNegativeLengthPrefix()
    {
        var buffer = new byte[VarInt.MaxBytes];
        var prefix = VarInt.Write(buffer, -1);

        Assert.Equal(DecodeStatus.Invalid, Utf8StringCodec.TryRead(buffer[..prefix], out _, out _));
    }

    [Fact]
    public void MalformedUtf8IsReplacedNotRejected()
    {
        // 0xFF is not a legal UTF-8 lead byte; vanilla substitutes U+FFFD.
        var buffer = new byte[8];
        var prefix = VarInt.Write(buffer, 1);
        buffer[prefix] = 0xFF;

        Assert.Equal(DecodeStatus.Success, Utf8StringCodec.TryRead(buffer[..(prefix + 1)], out var decoded, out _));
        Assert.Equal("\uFFFD", decoded);
    }

    [Fact]
    public void IdentifierSharesTheUtf8WireFormat()
    {
        var identifier = Identifier.Parse("minecraft:stone");
        var buffer = new byte[64];

        var written = Utf8StringCodec.Write(buffer, identifier.ToString());
        var decoded = Utf8StringCodec.Read(buffer[..written], out _);

        Assert.Equal(identifier, Identifier.Parse(decoded));
    }
}
