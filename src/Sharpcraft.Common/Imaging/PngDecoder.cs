// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Sharpcraft.Common.Imaging;

/// <summary>Decoded image: straight RGBA8, row-major, no padding.</summary>
/// <param name="Width">Pixel width.</param>
/// <param name="Height">Pixel height.</param>
/// <param name="Pixels">RGBA bytes, four per pixel.</param>
public sealed record RgbaImage(int Width, int Height, byte[] Pixels)
{
    /// <summary>Bytes per pixel in <see cref="Pixels"/>.</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Reads a pixel as packed RGBA.</summary>
    public (byte R, byte G, byte B, byte A) PixelAt(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        var offset = ((y * Width) + x) * BytesPerPixel;
        return (Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
    }
}

/// <summary>
/// PNG decoder for vanilla textures. Supports every bit depth and colour type the official assets
/// use — including the 4-bit indexed textures such as the water and lava stills, which a
/// depth-8-only decoder silently refused — with all five scanline filters. Interlacing stays
/// unsupported and is reported rather than guessed (docs/modules/render.md §6.1).
/// </summary>
public static class PngDecoder
{
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Decodes a PNG into RGBA8.</summary>
    /// <exception cref="InvalidDataException">The data is not a PNG this decoder supports.</exception>
    public static RgbaImage Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < Signature.Length || !png[..Signature.Length].SequenceEqual(Signature))
        {
            throw new InvalidDataException("not a PNG file (bad signature)");
        }

        var offset = Signature.Length;
        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = 0;
        var interlaced = false;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();

        while (offset + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            if (length < 0 || offset + 12 + length > png.Length)
            {
                throw new InvalidDataException("PNG chunk length runs past the end of the data");
            }

            var type = Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            var payload = png.Slice(offset + 8, length);
            offset += 12 + length;

            switch (type)
            {
                case "IHDR":
                    if (payload.Length < 13)
                    {
                        throw new InvalidDataException("PNG IHDR chunk is truncated");
                    }

                    width = BinaryPrimitives.ReadInt32BigEndian(payload);
                    height = BinaryPrimitives.ReadInt32BigEndian(payload[4..]);
                    bitDepth = payload[8];
                    colorType = payload[9];
                    interlaced = payload[12] != 0;

                    if (width <= 0 || height <= 0)
                    {
                        throw new InvalidDataException($"PNG has an invalid size {width}x{height}");
                    }

                    break;

                case "PLTE":
                    palette = payload.ToArray();
                    break;

                case "tRNS":
                    transparency = payload.ToArray();
                    break;

                case "IDAT":
                    idat.Write(payload);
                    break;

                case "IEND":
                    offset = png.Length;
                    break;

                default:
                    // Ancillary and unknown chunks do not affect the pixels we care about.
                    break;
            }
        }

        if (interlaced)
        {
            throw new InvalidDataException("interlaced PNG is not supported");
        }

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException($"PNG colour type {colorType} is not supported")
        };

        var validDepth = colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            2 or 4 or 6 => bitDepth is 8 or 16,
            _ => false
        };

        if (!validDepth)
        {
            throw new InvalidDataException(
                $"PNG bit depth {bitDepth} is not valid for colour type {colorType}");
        }

        if (colorType == 3 && palette is null)
        {
            throw new InvalidDataException("indexed PNG has no PLTE chunk");
        }

        var bitsPerPixel = bitDepth * channels;
        var stride = ((width * bitsPerPixel) + 7) / 8;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var filtered = new byte[(stride + 1) * height];

        idat.Position = 0;
        using (var inflate = new ZLibStream(idat, CompressionMode.Decompress))
        {
            var read = 0;
            while (read < filtered.Length)
            {
                var chunk = inflate.Read(filtered, read, filtered.Length - read);
                if (chunk == 0)
                {
                    break;
                }

                read += chunk;
            }

            if (read != filtered.Length)
            {
                throw new InvalidDataException(
                    $"PNG image data is incomplete: expected {filtered.Length} bytes, got {read}");
            }
        }

        Unfilter(filtered, stride, height, bytesPerPixel);
        var pixels = ExpandToRgba(filtered, width, height, bitDepth, colorType, channels, palette, transparency);

        return new RgbaImage(width, height, pixels);
    }

    private static void Unfilter(byte[] data, int stride, int height, int bytesPerPixel)
    {
        var previous = new byte[stride];
        var current = new byte[stride];

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (stride + 1);
            var filter = data[rowStart];
            Buffer.BlockCopy(data, rowStart + 1, current, 0, stride);

            switch (filter)
            {
                case 0:
                    break;

                case 1:
                    for (var i = bytesPerPixel; i < stride; i++)
                    {
                        current[i] = (byte)(current[i] + current[i - bytesPerPixel]);
                    }

                    break;

                case 2:
                    for (var i = 0; i < stride; i++)
                    {
                        current[i] = (byte)(current[i] + previous[i]);
                    }

                    break;

                case 3:
                    for (var i = 0; i < stride; i++)
                    {
                        var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : (byte)0;
                        current[i] = (byte)(current[i] + ((left + previous[i]) >> 1));
                    }

                    break;

                case 4:
                    for (var i = 0; i < stride; i++)
                    {
                        var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : (byte)0;
                        var up = previous[i];
                        var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : (byte)0;
                        current[i] = (byte)(current[i] + Paeth(left, up, upLeft));
                    }

                    break;

                default:
                    throw new InvalidDataException($"PNG scanline filter {filter} is not defined");
            }

            Buffer.BlockCopy(current, 0, data, rowStart + 1, stride);
            (previous, current) = (current, previous);
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

    private static byte[] ExpandToRgba(
        byte[] data,
        int width,
        int height,
        int bitDepth,
        int colorType,
        int channels,
        byte[]? palette,
        byte[]? transparency)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
        var stride = ((width * bitDepth * channels) + 7) / 8;
        var samplesPerByte = 8 / bitDepth;

        for (var y = 0; y < height; y++)
        {
            var rowStart = (y * (stride + 1)) + 1;

            for (var x = 0; x < width; x++)
            {
                var target = ((y * width) + x) * RgbaImage.BytesPerPixel;

                switch (colorType)
                {
                    case 0:
                        var grayValue = ReadSample(data, rowStart, x, bitDepth, samplesPerByte);
                        var gray = ScaleToByte(grayValue, bitDepth);
                        pixels[target] = gray;
                        pixels[target + 1] = gray;
                        pixels[target + 2] = gray;
                        pixels[target + 3] = transparency is { Length: >= 2 } && grayValue == transparency[1]
                            ? (byte)0
                            : (byte)255;
                        break;

                    case 2:
                        ReadSample2(data, rowStart, x, bitDepth, channels, out var red, out var green, out var blue);
                        pixels[target] = red;
                        pixels[target + 1] = green;
                        pixels[target + 2] = blue;
                        pixels[target + 3] = 255;
                        break;

                    case 3:
                        var index = ReadSample(data, rowStart, x, bitDepth, samplesPerByte);
                        var paletteOffset = index * 3;
                        if (palette is null || paletteOffset + 2 >= palette.Length)
                        {
                            throw new InvalidDataException($"PNG palette index {index} is out of range");
                        }

                        pixels[target] = palette[paletteOffset];
                        pixels[target + 1] = palette[paletteOffset + 1];
                        pixels[target + 2] = palette[paletteOffset + 2];
                        pixels[target + 3] = transparency is not null && index < transparency.Length
                            ? transparency[index]
                            : (byte)255;
                        break;

                    case 4:
                        var value = ReadSample(data, rowStart, x, bitDepth, samplesPerByte);
                        var alpha = ReadSample(data, rowStart, x, bitDepth, samplesPerByte, secondChannel: true);
                        var grayAlpha = ScaleToByte(value, bitDepth);
                        pixels[target] = grayAlpha;
                        pixels[target + 1] = grayAlpha;
                        pixels[target + 2] = grayAlpha;
                        pixels[target + 3] = ScaleToByte(alpha, bitDepth);
                        break;

                    case 6:
                        var channelBytes = bitDepth / 8;
                        var baseIndex = rowStart + (x * channels * channelBytes);
                        pixels[target] = data[baseIndex];
                        pixels[target + 1] = data[baseIndex + channelBytes];
                        pixels[target + 2] = data[baseIndex + (2 * channelBytes)];
                        pixels[target + 3] = data[baseIndex + (3 * channelBytes)];
                        break;

                    default:
                        throw new InvalidDataException($"PNG colour type {colorType} is not supported");
                }
            }
        }

        return pixels;
    }

    /// <summary>Reads one sample at a bit depth of 1, 2, 4, 8 or 16.</summary>
    private static int ReadSample(byte[] data, int rowStart, int x, int bitDepth, int samplesPerByte, bool secondChannel = false)
    {
        var index = secondChannel ? (x * 2) + 1 : x;

        if (bitDepth == 8)
        {
            return data[rowStart + index];
        }

        if (bitDepth == 16)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(rowStart + (index * 2), 2));
        }

        var byteIndex = rowStart + (index / samplesPerByte);
        var shift = 8 - bitDepth - ((index % samplesPerByte) * bitDepth);
        var mask = (1 << bitDepth) - 1;
        return (data[byteIndex] >> shift) & mask;
    }

    private static void ReadSample2(
        byte[] data,
        int rowStart,
        int x,
        int bitDepth,
        int channels,
        out byte red,
        out byte green,
        out byte blue)
    {
        var channelBytes = bitDepth / 8;
        var baseIndex = rowStart + (x * channels * channelBytes);
        red = data[baseIndex];
        green = data[baseIndex + channelBytes];
        blue = data[baseIndex + (2 * channelBytes)];
    }

    /// <summary>Scales a sample to 0-255; 16-bit samples keep their high byte.</summary>
    private static byte ScaleToByte(int sample, int bitDepth) => bitDepth switch
    {
        8 => (byte)sample,
        16 => (byte)(sample >> 8),
        _ => (byte)(sample * 255 / ((1 << bitDepth) - 1))
    };
}
