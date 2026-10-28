// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Sharpcraft.Common.Imaging;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// PNG decoding (docs/modules/render.md §6.1). Fixtures are built in the test: some through our own
/// encoder, some hand-assembled so every scanline filter is exercised.
/// </summary>
public sealed class PngDecoderTests
{
    [Fact]
    public void RoundTripsThroughOurEncoder()
    {
        var pixels = new byte[]
        {
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 128, 128, 128, 0
        };

        var png = PngEncoder.EncodeRgba(2, 2, pixels);
        var image = PngDecoder.Decode(png);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(pixels, image.Pixels);
        Assert.Equal((255, 0, 0, 255), image.PixelAt(0, 0));
        Assert.Equal((128, 128, 128, 0), image.PixelAt(1, 1));
    }

    [Theory]
    [InlineData(0)] // none
    [InlineData(1)] // sub
    [InlineData(2)] // up
    [InlineData(3)] // average
    [InlineData(4)] // paeth
    public void EveryScanlineFilterIsRendered(int filter)
    {
        var expected = new byte[]
        {
            10, 20, 30, 255, 40, 50, 60, 255, 70, 80, 90, 255,
            15, 25, 35, 255, 45, 55, 65, 255, 75, 85, 95, 255
        };

        var png = BuildPng(width: 3, height: 2, colorType: 6, expected, filter);
        var image = PngDecoder.Decode(png);

        Assert.Equal(expected, image.Pixels);
    }

    [Fact]
    public void GrayscaleExpandsToRgba()
    {
        var gray = new byte[] { 0, 128, 255, 64 };
        var png = BuildPng(width: 2, height: 2, colorType: 0, gray, filter: 0);

        var image = PngDecoder.Decode(png);

        Assert.Equal((0, 0, 0, 255), image.PixelAt(0, 0));
        Assert.Equal((128, 128, 128, 255), image.PixelAt(1, 0));
        Assert.Equal((255, 255, 255, 255), image.PixelAt(0, 1));
    }

    [Fact]
    public void IndexedWithTransparencyUsesThePalette()
    {
        var png = BuildIndexedPng();

        var image = PngDecoder.Decode(png);

        Assert.Equal((255, 0, 0, 255), image.PixelAt(0, 0));
        Assert.Equal((0, 255, 0, 0), image.PixelAt(1, 0)); // fully transparent entry
        Assert.Equal((0, 0, 255, 255), image.PixelAt(0, 1));
    }

    [Fact]
    public void RejectsWhatItCannotDecodeWithAUsefulMessage()
    {
        var notPng = PngDecoder.Decode;
        Assert.Contains("signature", Assert.Throws<InvalidDataException>(
            () => notPng([1, 2, 3, 4, 5, 6, 7, 8, 9])).Message, StringComparison.OrdinalIgnoreCase);

        var interlaced = BuildPng(width: 1, height: 1, colorType: 6, [0, 0, 0, 255], filter: 0, interlace: 1);
        Assert.Contains("interlaced", Assert.Throws<InvalidDataException>(
            () => PngDecoder.Decode(interlaced)).Message, StringComparison.OrdinalIgnoreCase);

        var truncated = PngEncoder.EncodeRgba(4, 4, new byte[4 * 4 * 4])[..40];
        Assert.Throws<InvalidDataException>(() => PngDecoder.Decode(truncated));
    }

    [Fact]
    public void FourBitIndexedDecodesLikeTheWaterStills()
    {
        // The real assets contain 4-bit indexed textures; a depth-8-only decoder refused water_still.
        byte[] palette = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160];
        byte[] indices = [0x01, 0x23]; // two pixels per byte: 0,1 and 2,3

        // Each row is one filter byte plus one packed data byte (2 pixels at 4 bits).
        var png = Assemble(width: 2, height: 2, bitDepth: 4, colorType: 3, interlace: 0,
            filtered: [0, indices[0], 0, indices[1]],
            palette: palette, transparency: null);

        var image = PngDecoder.Decode(png);

        Assert.Equal((10, 20, 30, 255), image.PixelAt(0, 0));
        Assert.Equal((40, 50, 60, 255), image.PixelAt(1, 0));
        Assert.Equal((70, 80, 90, 255), image.PixelAt(0, 1));
        Assert.Equal((100, 110, 120, 255), image.PixelAt(1, 1));
    }

    [Fact]
    public void SixteenBitKeepsTheHighByte()
    {
        // 1x1 RGBA at 16 bits per channel: 0x1234, 0x5678, 0x9ABC, 0xDEF0.
        byte[] samples = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0];

        var png = Assemble(width: 1, height: 1, bitDepth: 16, colorType: 6, interlace: 0,
            filtered: [0, .. samples], palette: null, transparency: null);

        var image = PngDecoder.Decode(png);

        Assert.Equal((0x12, 0x56, 0x9A, 0xDE), image.PixelAt(0, 0));
    }

    [Fact]
    public void UndefinedFilterValueIsRejected()
    {
        var png = BuildPng(width: 1, height: 1, colorType: 6, [1, 2, 3, 4], filter: 9);

        Assert.Contains("filter", Assert.Throws<InvalidDataException>(
            () => PngDecoder.Decode(png)).Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds a PNG whose scanlines all use the given filter type.</summary>
    private static byte[] BuildPng(
        int width,
        int height,
        int colorType,
        byte[] rawPixels,
        int filter,
        int bitDepth = 8,
        int interlace = 0)
    {
        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => 1
        };

        var stride = width * channels;
        var filtered = new byte[(stride + 1) * height];

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (stride + 1);
            filtered[rowStart] = (byte)filter;

            var previous = y > 0 ? rawPixels.AsSpan((y - 1) * stride, stride) : Span<byte>.Empty;
            var source = rawPixels.AsSpan(y * stride, stride);

            for (var i = 0; i < stride; i++)
            {
                var left = i >= channels ? source[i - channels] : (byte)0;
                var up = previous.IsEmpty ? (byte)0 : previous[i];
                var upLeft = previous.IsEmpty || i < channels ? (byte)0 : previous[i - channels];

                filtered[rowStart + 1 + i] = filter switch
                {
                    0 => source[i],
                    1 => (byte)(source[i] - left),
                    2 => (byte)(source[i] - up),
                    3 => (byte)(source[i] - ((left + up) >> 1)),
                    4 => (byte)(source[i] - Paeth(left, up, upLeft)),
                    _ => source[i]
                };
            }
        }

        return Assemble(width, height, bitDepth, colorType, interlace, filtered, palette: null, transparency: null);
    }

    private static byte[] BuildIndexedPng()
    {
        // 2x2, indices 0,1 / 2,0; palette red, green (transparent), blue.
        byte[] rawPixels = [0, 1, 2, 0];
        var filtered = new byte[(2 + 1) * 2];
        filtered[0] = 0;
        filtered[1] = rawPixels[0];
        filtered[2] = rawPixels[1];
        filtered[3] = 0;
        filtered[4] = rawPixels[2];
        filtered[5] = rawPixels[3];

        byte[] palette = [255, 0, 0, 0, 255, 0, 0, 0, 255];
        byte[] transparency = [255, 0, 255];

        return Assemble(2, 2, 8, 3, 0, filtered, palette, transparency);
    }

    private static byte[] Assemble(
        int width,
        int height,
        int bitDepth,
        int colorType,
        int interlace,
        byte[] filtered,
        byte[]? palette,
        byte[]? transparency)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = (byte)bitDepth;
        header[9] = (byte)colorType;
        header[10] = 0;
        header[11] = 0;
        header[12] = (byte)interlace;
        WriteChunk(output, "IHDR", header);

        if (palette is not null)
        {
            WriteChunk(output, "PLTE", palette);
        }

        if (transparency is not null)
        {
            WriteChunk(output, "tRNS", transparency);
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(filtered);
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] payload)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, payload.Length);
        output.Write(buffer);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(payload);

        var crc = Crc32(typeBytes, payload);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        output.Write(buffer);
    }

    private static uint Crc32(byte[] type, byte[] payload)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
        {
            crc = Update(crc, value);
        }

        foreach (var value in payload)
        {
            crc = Update(crc, value);
        }

        return ~crc;

        static uint Update(uint state, byte value)
        {
            state ^= value;
            for (var i = 0; i < 8; i++)
            {
                state = (state & 1) != 0 ? 0xEDB88320u ^ (state >> 1) : state >> 1;
            }

            return state;
        }
    }

    private static byte Paeth(byte left, byte up, byte upLeft)
    {
        var estimate = left + up - upLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceUp = Math.Abs(estimate - up);
        var distanceUpLeft = Math.Abs(estimate - upLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
        {
            return left;
        }

        return distanceUp <= distanceUpLeft ? up : upLeft;
    }
}
