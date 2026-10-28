// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Length-prefixed UTF-8 strings as used by the Minecraft protocol
/// (docs/specs/network/primitives-strings.md). The prefix is the <b>byte</b> count.
/// </summary>
public static class Utf8StringCodec
{
    /// <summary>Vanilla default character limit.</summary>
    public const int DefaultMaxLength = 32767;

    /// <summary>Writes a string with its VarInt byte length; returns bytes written.</summary>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    /// <exception cref="EncoderFallbackException">The value is not encodable as UTF-8.</exception>
    public static int Write(Span<byte> destination, string value, int maxLength = DefaultMaxLength)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Java's String.length() is the UTF-16 unit count, which matches string.Length here.
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"string too big (was {value.Length} characters, max {maxLength})", nameof(value));
        }

        var utf8 = Encoding.UTF8;
        var byteCount = utf8.GetByteCount(value);
        if (byteCount > maxLength * 3)
        {
            throw new ArgumentException(
                $"string too big (was {byteCount} bytes encoded, max {maxLength * 3})", nameof(value));
        }

        var prefixLength = VarInt.GetByteSize(byteCount);
        if (destination.Length < prefixLength + byteCount)
        {
            throw new ArgumentException("destination too small for string", nameof(destination));
        }

        var written = VarInt.Write(destination, byteCount);
        utf8.GetBytes(value, destination[written..]);
        return written + byteCount;
    }

    /// <summary>Attempts to read a string, applying the validation order of the spec.</summary>
    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> source,
        out string value,
        out int consumed,
        int maxLength = DefaultMaxLength)
    {
        value = string.Empty;
        consumed = 0;

        var status = VarInt.TryRead(source, out var byteCount, out var prefixLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (byteCount < 0 || byteCount > maxLength * 3)
        {
            return DecodeStatus.Invalid;
        }

        if (source.Length - prefixLength < byteCount)
        {
            return DecodeStatus.NeedMoreData;
        }

        // Vanilla decodes with the platform UTF-8 decoder, which substitutes U+FFFD for
        // malformed sequences instead of failing, so lenient decoding is the faithful choice.
        Span<char> chars = byteCount <= 1024 ? stackalloc char[byteCount] : new char[byteCount];
        var charCount = Encoding.UTF8.GetChars(source.Slice(prefixLength, byteCount), chars);
        if (charCount > maxLength)
        {
            return DecodeStatus.Invalid;
        }

        value = new string(chars[..charCount]);
        consumed = prefixLength + byteCount;
        return DecodeStatus.Success;
    }

    /// <summary>Reads a string or throws.</summary>
    /// <exception cref="InvalidDataException">The data is malformed or truncated.</exception>
    public static string Read(ReadOnlySpan<byte> source, out int consumed, int maxLength = DefaultMaxLength)
    {
        return TryRead(source, out var value, out consumed, maxLength) switch
        {
            DecodeStatus.Success => value,
            DecodeStatus.NeedMoreData => throw new InvalidDataException("truncated UTF-8 string"),
            _ => throw new InvalidDataException("invalid UTF-8 string")
        };
    }
}
