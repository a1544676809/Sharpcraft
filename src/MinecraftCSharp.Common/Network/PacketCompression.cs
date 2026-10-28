// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.IO.Compression;

namespace MinecraftCSharp.Common.Network;

/// <summary>
/// Post-login packet compression (zlib), with the length rules of
/// docs/specs/network/protocol-phases.md 5.
/// </summary>
public static class PacketCompression
{
    /// <summary>Compresses or passes through a packet body, writing into <paramref name="destination"/>.</summary>
    /// <returns>Bytes written.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The body exceeds the 8 MiB ceiling.</exception>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public static int Compress(Span<byte> destination, ReadOnlySpan<byte> body, int threshold)
    {
        if (body.Length > ProtocolConstants.MaxUncompressedBody)
        {
            throw new ArgumentOutOfRangeException(nameof(body), body.Length, "packet body exceeds 8 MiB");
        }

        if (body.Length < threshold)
        {
            var written = VarInt.Write(destination, 0);
            body.CopyTo(destination[written..]);
            return written + body.Length;
        }

        var header = VarInt.Write(destination, body.Length);
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(body);
        }

        var compressed = output.GetBuffer().AsSpan(0, (int)output.Length);
        if (destination.Length - header < compressed.Length)
        {
            throw new ArgumentException("destination too small for compressed body", nameof(destination));
        }

        compressed.CopyTo(destination[header..]);
        return header + compressed.Length;
    }

    /// <summary>Decompresses a frame body according to the compression rules.</summary>
    public static DecodeStatus TryDecompress(ReadOnlySpan<byte> frame, int threshold, out byte[] body)
    {
        body = [];

        var status = VarInt.TryRead(frame, out var uncompressedLength, out var prefixLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        var payload = frame[prefixLength..];
        if (uncompressedLength == 0)
        {
            body = payload.ToArray();
            return DecodeStatus.Success;
        }

        if (uncompressedLength < 0 ||
            uncompressedLength > ProtocolConstants.MaxUncompressedBody ||
            uncompressedLength < threshold ||
            payload.Length > ProtocolConstants.MaxCompressedBody)
        {
            return DecodeStatus.Invalid;
        }

        var result = new byte[uncompressedLength];
        using var input = new MemoryStream(payload.ToArray(), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var read = 0;
        while (read < uncompressedLength)
        {
            var chunk = zlib.Read(result, read, uncompressedLength - read);
            if (chunk == 0)
            {
                return DecodeStatus.Invalid;
            }

            read += chunk;
        }

        body = result;
        return DecodeStatus.Success;
    }
}
