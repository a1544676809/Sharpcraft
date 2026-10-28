// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Base-128 variable-length integer encoding for 64-bit values
/// (docs/specs/network/primitives-varint.md).
/// </summary>
public static class VarLong
{
    /// <summary>Maximum encoded size of a 64-bit value.</summary>
    public const int MaxBytes = 10;

    private const long DataMask = 0x7F;
    private const long ContinuationMask = 0x80;
    private const int BitsPerByte = 7;

    /// <summary>Number of bytes needed to encode <paramref name="value"/>.</summary>
    public static int GetByteSize(long value)
    {
        for (var i = 1; i < MaxBytes; i++)
        {
            if ((value & (-1L << (i * BitsPerByte))) == 0L)
            {
                return i;
            }
        }

        return MaxBytes;
    }

    /// <summary>Writes <paramref name="value"/>; returns the number of bytes written.</summary>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public static int Write(Span<byte> destination, long value)
    {
        var written = 0;
        var remaining = value;
        while ((remaining & ~DataMask) != 0L)
        {
            if (written >= destination.Length)
            {
                throw new ArgumentException("destination too small for VarLong", nameof(destination));
            }

            destination[written++] = unchecked((byte)((remaining & DataMask) | ContinuationMask));
            remaining = (long)((ulong)remaining >> BitsPerByte);
        }

        if (written >= destination.Length)
        {
            throw new ArgumentException("destination too small for VarLong", nameof(destination));
        }

        destination[written++] = unchecked((byte)remaining);
        return written;
    }

    /// <summary>Attempts to decode from <paramref name="source"/>.</summary>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> source, out long value, out int consumed)
    {
        long result = 0;
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
            result |= (long)(current & DataMask) << (read - 1) * BitsPerByte;

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
    public static long Read(ReadOnlySpan<byte> source, out int consumed)
    {
        return TryRead(source, out var value, out consumed) switch
        {
            DecodeStatus.Success => value,
            DecodeStatus.NeedMoreData => throw new InvalidDataException("truncated VarLong"),
            _ => throw new InvalidDataException("VarLong too big")
        };
    }
}
