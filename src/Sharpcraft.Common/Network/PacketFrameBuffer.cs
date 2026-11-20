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
    private int _readOffset;
    private int _writeOffset;

    /// <summary>Creates a buffer with the given initial capacity.</summary>
    public PacketFrameBuffer(int initialCapacity = 8192)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _buffer = new byte[initialCapacity];
    }

    /// <summary>Bytes currently buffered but not yet consumed.</summary>
    public int BufferedBytes => _writeOffset - _readOffset;

    /// <summary>Appends freshly received bytes.</summary>
    public void Append(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_writeOffset));
        _writeOffset += data.Length;
    }

    /// <summary>Attempts to read one complete body, consuming it from the buffer.</summary>
    public DecodeStatus TryReadFrame(out byte[] body)
    {
        body = [];

        var available = _writeOffset - _readOffset;
        var status = PacketFramer.TryReadLengthPrefix(_buffer.AsSpan(_readOffset, available), out var bodyLength, out var prefixLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (available - prefixLength < bodyLength)
        {
            return DecodeStatus.NeedMoreData;
        }

        body = _buffer.AsSpan(_readOffset + prefixLength, bodyLength).ToArray();
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
        _readOffset += count;
        if (_readOffset >= _writeOffset)
        {
            _readOffset = 0;
            _writeOffset = 0;
        }
    }

    private void EnsureCapacity(int additionalBytes)
    {
        if (_writeOffset + additionalBytes <= _buffer.Length)
        {
            return;
        }

        var unread = _writeOffset - _readOffset;
        if (_readOffset > 0)
        {
            if (unread > 0)
            {
                _buffer.AsSpan(_readOffset, unread).CopyTo(_buffer);
            }

            _readOffset = 0;
            _writeOffset = unread;
            if (_writeOffset + additionalBytes <= _buffer.Length)
            {
                return;
            }
        }

        var required = _writeOffset + additionalBytes;
        var capacity = _buffer.Length;
        while (capacity < required)
        {
            capacity *= 2;
        }

        Array.Resize(ref _buffer, capacity);
    }
}
