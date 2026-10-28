// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>Outcome of a streaming decode attempt.</summary>
public enum DecodeStatus
{
    /// <summary>A value was decoded.</summary>
    Success,

    /// <summary>The buffer ends mid-value; feed more bytes and retry.</summary>
    NeedMoreData,

    /// <summary>The encoding is invalid and the connection should be closed.</summary>
    Invalid
}

/// <summary>
/// Base-128 variable-length integer encoding used by the Minecraft protocol
/// (docs/specs/network/primitives-varint.md).
/// </summary>
public static class VarInt
{
    /// <summary>Maximum encoded size of a 32-bit value.</summary>
    public const int MaxBytes = 5;

    private const int DataMask = 0x7F;
    private const int ContinuationMask = 0x80;
    private const int BitsPerByte = 7;

    /// <summary>Number of bytes needed to encode <paramref name="value"/>.</summary>
    public static int GetByteSize(int value)
    {
        for (var i = 1; i < MaxBytes; i++)
        {
            if ((value & (-1 << (i * BitsPerByte))) == 0)
            {
                return i;
            }
        }

        return MaxBytes;
    }

    /// <summary>Writes <paramref name="value"/>; returns the number of bytes written.</summary>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public static int Write(Span<byte> destination, int value)
    {
        var written = 0;
        var remaining = value;
        while ((remaining & ~DataMask) != 0)
        {
            if (written >= destination.Length)
            {
                throw new ArgumentException("destination too small for VarInt", nameof(destination));
            }

            destination[written++] = unchecked((byte)((remaining & DataMask) | ContinuationMask));
            remaining = (int)((uint)remaining >> BitsPerByte);
        }

        if (written >= destination.Length)
        {
            throw new ArgumentException("destination too small for VarInt", nameof(destination));
        }

        destination[written++] = unchecked((byte)remaining);
        return written;
    }

    /// <summary>Attempts to decode from <paramref name="source"/>.</summary>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> source, out int value, out int consumed)
    {
        var result = 0;
        var read = 0;

        while (true)
        {
            if (read >= source.Length)
            {
                value = 0;
                consumed = 0;
                return DecodeStatus.NeedMoreData;
            }

            var current = source[read++];
            result |= (current & DataMask) << (read - 1) * BitsPerByte;

            if ((current & ContinuationMask) == 0)
            {
                value = result;
                consumed = read;
                return DecodeStatus.Success;
            }

            if (read >= MaxBytes)
            {
                value = 0;
                consumed = 0;
                return DecodeStatus.Invalid;
            }
        }
    }

    /// <summary>Decodes a value or throws.</summary>
    /// <exception cref="InvalidDataException">The encoding is malformed or truncated.</exception>
    public static int Read(ReadOnlySpan<byte> source, out int consumed)
    {
        return TryRead(source, out var value, out consumed) switch
        {
            DecodeStatus.Success => value,
            DecodeStatus.NeedMoreData => throw new InvalidDataException("truncated VarInt"),
            _ => throw new InvalidDataException("VarInt too big")
        };
    }
}
