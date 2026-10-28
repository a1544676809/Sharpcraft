// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Reads the paletted container layout used inside chunk data
/// (docs/modules/world.md 3.1).
/// </summary>
/// <remarks>
/// Wire format: one byte of bits per entry, then either a VarInt single value (bits 0) or a
/// local palette (bits 1..maxLocalBits), then a <b>fixed-size</b> long array with no length
/// prefix. Direct (global id) containers carry no palette.
/// </remarks>
public static class PalettedContainerCodec
{
    /// <summary>Local palette ceiling for block states.</summary>
    public const int BlockStateMaxLocalBits = 8;

    /// <summary>Local palette ceiling for biomes.</summary>
    public const int BiomeMaxLocalBits = 3;

    /// <summary>Reads one container with <paramref name="size"/> entries.</summary>
    public static DecodeStatus TryRead(
        ref PayloadReader reader,
        int size,
        int maxLocalBits,
        out PalettedContainer container)
    {
        container = new PalettedContainer(size, 0);

        var status = reader.TryReadByte(out var bits);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (bits > 32)
        {
            return DecodeStatus.Invalid;
        }

        if (bits == 0)
        {
            status = reader.TryReadVarInt(out var single);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            container = new PalettedContainer(size, single);
            return DecodeStatus.Success;
        }

        var direct = bits > maxLocalBits;
        int[] palette = [];
        if (!direct)
        {
            status = reader.TryReadVarInt(out var paletteSize);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            if (paletteSize < 0 || paletteSize > 1 << maxLocalBits)
            {
                return DecodeStatus.Invalid;
            }

            palette = new int[paletteSize];
            for (var i = 0; i < paletteSize; i++)
            {
                status = reader.TryReadVarInt(out palette[i]);
                if (status != DecodeStatus.Success)
                {
                    return status;
                }
            }
        }
        var longCount = PalettedContainer.ExpectedLongs(size, bits);
        var longs = new long[longCount];
        for (var i = 0; i < longCount; i++)
        {
            status = reader.TryReadLong(out longs[i]);
            if (status != DecodeStatus.Success)
            {
                return status;
            }
        }

        container = new PalettedContainer(size, bits, palette, longs, direct);
        return DecodeStatus.Success;
    }
}
