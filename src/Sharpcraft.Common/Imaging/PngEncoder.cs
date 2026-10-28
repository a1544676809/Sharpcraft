// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Sharpcraft.Common.Imaging;

/// <summary>
/// Minimal PNG writer for RGBA8 frames. No third-party imaging dependency: the format needs
/// only zlib deflate and CRC32, both available in the BCL.
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Encodes RGBA8 pixels (row-major, 4 bytes per pixel) into a PNG byte array.</summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="rgba">Pixel data; must hold <c>width * height * 4</c> bytes.</param>
    public static byte[] EncodeRgba(int width, int height, ReadOnlySpan<byte> rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var expected = checked(width * height * 4);
        if (rgba.Length != expected)
        {
            throw new ArgumentException($"expected {expected} bytes for {width}x{height} RGBA, got {rgba.Length}", nameof(rgba));
        }

        using var output = new MemoryStream();
        output.Write(Signature);

        // IHDR: width, height, bit depth 8, colour type 6 (RGBA), no interlace.
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(output, "IHDR", header);

        WriteChunk(output, "IDAT", CompressScanlines(width, height, rgba));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] CompressScanlines(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var stride = width * 4;
        var raw = new byte[height * (stride + 1)];

        for (var y = 0; y < height; y++)
        {
            var destination = y * (stride + 1);
            raw[destination] = 0;  // filter type 0 (none)
            rgba.Slice(y * stride, stride).CopyTo(raw.AsSpan(destination + 1, stride));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc32.Compute(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }
}

/// <summary>CRC32 (ISO-HDLC) used by PNG chunk checksums.</summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    /// <summary>Computes the CRC32 over two spans concatenated.</summary>
    public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xFFFF_FFFFu;
        crc = Update(crc, first);
        crc = Update(crc, second);
        return crc ^ 0xFFFF_FFFFu;
    }

    /// <summary>Computes the CRC32 of one span.</summary>
    public static uint Compute(ReadOnlySpan<byte> data) => Compute(data, []);

    private static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB8_8320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
