// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using System.IO.Compression;
using Sharpcraft.Common.Imaging;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// PNG encoding and CRC32, checked against values produced by an independent reference
/// (python's zlib.crc32 and struct) rather than by our own code.
/// </summary>
public sealed class PngEncoderTests
{
    private static readonly byte[] ReferencePixels = [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255];

    [Fact]
    public void Crc32MatchesTheReferenceImplementation()
    {
        // zlib.crc32(b"abc") == 0x352441C2
        Assert.Equal(0x3524_41C2u, Crc32.Compute("abc"u8));
    }

    [Fact]
    public void EncodedPngHasTheExpectedStructureAndChecksums()
    {
        var png = PngEncoder.EncodeRgba(2, 2, ReferencePixels);

        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);

        // IHDR: length 13, type IHDR, then the descriptor; CRC from the reference run.
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8)));
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(0x72B6_0D24u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(29)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        Assert.Equal(8, png[24]);   // bit depth
        Assert.Equal(6, png[25]);   // colour type RGBA

        // IEND closes the stream with the fixed CRC.
        Assert.Equal(0xAE42_6082u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(png.Length - 4)));
    }

    [Fact]
    public void ScanlinesRoundTripThroughInflate()
    {
        var png = PngEncoder.EncodeRgba(2, 2, ReferencePixels);

        var offset = 8;
        byte[]? compressed = null;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IDAT")
            {
                compressed = png.AsSpan(offset + 8, length).ToArray();
                break;
            }

            offset += 12 + length;
        }

        Assert.NotNull(compressed);

        using var input = new MemoryStream(compressed!);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        var raw = output.ToArray();

        // Two scanlines, each a filter byte followed by four RGBA pixels.
        Assert.Equal(18, raw.Length);
        Assert.Equal(0, raw[0]);
        Assert.Equal(0, raw[9]);
        Assert.Equal(ReferencePixels[..8], raw[1..9]);
        Assert.Equal(ReferencePixels[8..], raw[10..18]);
    }

    [Fact]
    public void MismatchedPixelBufferIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PngEncoder.EncodeRgba(2, 2, new byte[15]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.EncodeRgba(0, 2, []));
    }
}
