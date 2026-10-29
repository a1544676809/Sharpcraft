// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Light data that follows the chunk payload in <c>level_chunk_with_light</c>
/// (docs/specs/network/protocol-phases.md 5.3).
/// </summary>
/// <param name="SkyMask">Sections that carry sky light, as raw little-endian bits.</param>
/// <param name="BlockMask">Sections that carry block light.</param>
/// <param name="EmptySkyMask">Sections whose sky light is all zero.</param>
/// <param name="EmptyBlockMask">Sections whose block light is all zero.</param>
/// <param name="SkyUpdates">Raw 2048-byte sky light arrays, in mask order.</param>
/// <param name="BlockUpdates">Raw 2048-byte block light arrays, in mask order.</param>
public sealed record LightData(
    byte[] SkyMask,
    byte[] BlockMask,
    byte[] EmptySkyMask,
    byte[] EmptyBlockMask,
    IReadOnlyList<byte[]> SkyUpdates,
    IReadOnlyList<byte[]> BlockUpdates)
{
    /// <summary>An empty light payload.</summary>
    public static LightData Empty { get; } = new([], [], [], [], [], []);

    /// <summary>Number of light arrays carried by this payload.</summary>
    public int ArrayCount => SkyUpdates.Count + BlockUpdates.Count;

    /// <summary>Total bytes of raw light arrays.</summary>
    public int ByteCount => SkyUpdates.Sum(array => array.Length) + BlockUpdates.Sum(array => array.Length);

    /// <summary>
    /// Tests a bit in a mask. The wire form is a Java <c>BitSet.toByteArray()</c>: little-endian
    /// within each byte, byte order by index.
    /// </summary>
    public static bool IsSet(byte[] mask, int index)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var byteIndex = index >> 3;
        return byteIndex < mask.Length && (mask[byteIndex] & (1 << (index & 7))) != 0;
    }

    /// <summary>Counts set bits in a mask.</summary>
    public static int CountSet(byte[] mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        return mask.Sum(value => System.Numerics.BitOperations.PopCount(value));
    }
}

/// <summary>
/// Parses the light section of a chunk packet. Arrays stay raw until the lighting engine exists,
/// but their structure is validated so a malformed packet fails loudly instead of silently
/// shifting everything after it.
/// </summary>
public static class LightDataCodec
{

    /// <summary>Why the last light read failed, with its position; diagnostic only.</summary>
    public static string LastFailure { get; private set; } = string.Empty;
    /// <summary>Bytes per light array (16x16x16 nibbles).</summary>
    public const int LightArrayBytes = 2048;

    /// <summary>Largest mask we accept in bytes (64 sections plus slack).</summary>
    public const int MaxMaskBytes = 64;

    /// <summary>Reads the four masks and the two array lists.</summary>
    public static DecodeStatus TryRead(ref PayloadReader reader, out LightData light)
    {
        light = LightData.Empty;

        var status = TryReadMask(ref reader, out var skyMask);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = TryReadMask(ref reader, out var blockMask);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = TryReadMask(ref reader, out var emptySkyMask);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = TryReadMask(ref reader, out var emptyBlockMask);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = TryReadArrays(ref reader, out var skyUpdates);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = TryReadArrays(ref reader, out var blockUpdates);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        light = new LightData(skyMask, blockMask, emptySkyMask, emptyBlockMask, skyUpdates, blockUpdates);
        return DecodeStatus.Success;
    }

    private static DecodeStatus TryReadMask(ref PayloadReader reader, out byte[] mask) =>
        reader.TryReadByteArray(out mask, MaxMaskBytes);

    private static DecodeStatus TryReadArrays(ref PayloadReader reader, out List<byte[]> arrays)
    {
        arrays = [];

        var status = reader.TryReadVarInt(out var count);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (count < 0 || count > 64)
        {
            LastFailure = $"light array count {count} at {reader.Position}";
            return DecodeStatus.Invalid;
        }

        arrays = new List<byte[]>(count);
        for (var i = 0; i < count; i++)
        {
            status = reader.TryReadByteArray(out var data, LightArrayBytes);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            if (data.Length != LightArrayBytes)
            {
                LastFailure = $"light array {i} is {data.Length} bytes, expected {LightArrayBytes}";
                return DecodeStatus.Invalid;
            }

            arrays.Add(data);
        }

        return DecodeStatus.Success;
    }
}
