// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers;
using System.Net.Sockets;

namespace Sharpcraft.Common.Network;

/// <summary>
/// A live connection with the three protocol layers stacked in the order vanilla uses:
/// frames, optional compression, optional AES/CFB8 encryption
/// (docs/specs/network/protocol-phases.md 2, 5 and encryption.md 4).
/// </summary>
public sealed class MinecraftConnection : IDisposable
{
    private const int ReceiveBufferSize = 8192;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly PacketFrameBuffer _frames = new();
    private readonly byte[] _receive = new byte[ReceiveBufferSize];

    private AesCfb8? _outbound;
    private AesCfb8? _inbound;
    private int _compressionThreshold = -1;

    private MinecraftConnection(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _stream.ReadTimeout = (int)TimeSpan.FromSeconds(20).TotalMilliseconds;
        _stream.WriteTimeout = (int)TimeSpan.FromSeconds(20).TotalMilliseconds;
    }

    /// <summary>True once encryption is enabled.</summary>
    public bool Encrypted => _inbound is not null;

    /// <summary>True once compression is enabled.</summary>
    public bool Compressed => _compressionThreshold >= 0;

    /// <summary>Current compression threshold, or -1 when disabled.</summary>
    public int CompressionThreshold => _compressionThreshold;

    /// <summary>Opens a TCP connection.</summary>
    public static async Task<MinecraftConnection> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var client = new TcpClient
        {
            NoDelay = true
        };
        try
        {
            await client.ConnectAsync(host, port, cancellationToken)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new MinecraftConnection(client);
    }

    /// <summary>Enables encryption in both directions (must happen at the same moment).</summary>
    public void EnableEncryption(ReadOnlySpan<byte> sharedSecret)
    {
        _outbound = new AesCfb8(sharedSecret, decrypt: false);
        _inbound = new AesCfb8(sharedSecret, decrypt: true);
    }

    /// <summary>Enables compression for both directions.</summary>
    public void EnableCompression(int threshold) => _compressionThreshold = Math.Max(0, threshold);

    /// <summary>Writes one packet body and flushes the stream.</summary>
    public void Send(ReadOnlySpan<byte> body)
    {
        SendWithoutFlush(body);
        _stream.Flush();
    }

    /// <summary>Writes one packet body to the stream without flushing, for batched async writes.</summary>
    public void SendWithoutFlush(ReadOnlySpan<byte> body)
    {
        byte[]? rentedCompressed = null;
        byte[]? rentedFrame = null;
        try
        {
            ReadOnlySpan<byte> payload;
            if (_compressionThreshold < 0)
            {
                payload = body;
            }
            else
            {
                var maxCompressedSize = body.Length + 16 + (body.Length / 8);
                rentedCompressed = ArrayPool<byte>.Shared.Rent(maxCompressedSize);
                var written = PacketCompression.Compress(rentedCompressed, body, _compressionThreshold);
                payload = rentedCompressed.AsSpan(0, written);
            }

            var prefixLength = VarInt.GetByteSize(payload.Length);
            var frameLength = prefixLength + payload.Length;
            rentedFrame = ArrayPool<byte>.Shared.Rent(frameLength);
            var frame = rentedFrame.AsSpan(0, frameLength);
            var headerBytes = PacketFramer.WriteLengthPrefix(frame, payload.Length);
            payload.CopyTo(frame[headerBytes..]);

            _outbound?.TransformInPlace(frame);
            _stream.Write(frame);
        }
        finally
        {
            if (rentedFrame is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedFrame);
            }

            if (rentedCompressed is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedCompressed);
            }
        }
    }

    /// <summary>Flushes buffered outbound bytes on the underlying stream.</summary>
    public void Flush() => _stream.Flush();

    /// <summary>Reads the next packet body, or throws when the peer closes.</summary>
    public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var status = _frames.TryReadFrame(out var frame);
            if (status == DecodeStatus.Invalid)
            {
                throw new InvalidDataException("peer sent a malformed frame");
            }

            if (status == DecodeStatus.Success)
            {
                if (_compressionThreshold < 0)
                {
                    return frame;
                }

                var decompress = PacketCompression.TryDecompress(frame, _compressionThreshold, out var body);
                if (decompress != DecodeStatus.Success)
                {
                    throw new InvalidDataException("peer sent a malformed compressed packet");
                }

                return body;
            }

            var read = await _stream.ReadAsync(_receive, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("connection closed by the peer");
            }

            var chunk = _receive.AsSpan(0, read);
            _inbound?.TransformInPlace(chunk);
            _frames.Append(chunk);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _outbound?.Dispose();
        _inbound?.Dispose();
        _stream.Dispose();
        _client.Dispose();
    }
}
