// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Accumulates bytes from a stream and yields complete packet bodies. Handles arbitrary
/// TCP fragmentation: partial frames report <see cref="DecodeStatus.NeedMoreData"/>.
/// </summary>
public sealed class PacketFrameBuffer
{
    private byte[] _buffer;
    private int _length;

    /// <summary>Creates a buffer with the given initial capacity.</summary>
    public PacketFrameBuffer(int initialCapacity = 8192)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _buffer = new byte[initialCapacity];
    }

    /// <summary>Bytes currently buffered but not yet consumed.</summary>
    public int BufferedBytes => _length;

    /// <summary>Appends freshly received bytes.</summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(_length + data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    /// <summary>Attempts to read one complete body, consuming it from the buffer.</summary>
    public DecodeStatus TryReadFrame(out byte[] body)
    {
        body = [];

        var status = PacketFramer.TryReadLengthPrefix(_buffer.AsSpan(0, _length), out var bodyLength, out var prefixLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (_length - prefixLength < bodyLength)
        {
            return DecodeStatus.NeedMoreData;
        }

        body = _buffer.AsSpan(prefixLength, bodyLength).ToArray();
        Consume(prefixLength + bodyLength);
        return DecodeStatus.Success;
    }

    /// <summary>Wraps a body into a frame (length prefix + body) and returns the frame bytes.</summary>
    public static byte[] Wrap(ReadOnlySpan<byte> body)
    {
        var prefixLength = VarInt.GetByteSize(body.Length);
        var frame = new byte[prefixLength + body.Length];
        var written = PacketFramer.WriteLengthPrefix(frame, body.Length);
        body.CopyTo(frame.AsSpan(written));
        return frame;
    }

    private void Consume(int count)
    {
        _length -= count;
        if (_length > 0)
        {
            _buffer.AsSpan(count, _length).CopyTo(_buffer);
        }
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        var capacity = _buffer.Length;
        while (capacity < required)
        {
            capacity *= 2;
        }

        Array.Resize(ref _buffer, capacity);
    }
}
