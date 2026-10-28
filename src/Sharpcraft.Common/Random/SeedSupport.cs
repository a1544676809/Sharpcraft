// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Random;

/// <summary>
/// Seed derivation helpers, bit-compatible with Minecraft 26.3's <c>RandomSupport</c>.
/// See docs/analysis/common/random.md.
/// </summary>
public static class SeedSupport
{
    /// <summary>Golden ratio constant used when upgrading a 64-bit seed.</summary>
    public const long GoldenRatio64 = unchecked((long)0x9E37_79B9_7F4A_7C15UL);

    /// <summary>Silver ratio constant used when upgrading a 64-bit seed.</summary>
    public const long SilverRatio64 = unchecked((long)0x6A09_E667_F3BC_C909UL);

    private const long MixMultiplierA = unchecked((long)0xBF58_476D_1CE4_E5B9UL);
    private const long MixMultiplierB = unchecked((long)0x94D0_49BB_1331_11EBUL);

    /// <summary>Stafford variant 13 mixer.</summary>
    public static long MixStafford13(long value)
    {
        value = unchecked((value ^ UnsignedShiftRight(value, 30)) * MixMultiplierA);
        value = unchecked((value ^ UnsignedShiftRight(value, 27)) * MixMultiplierB);
        return value ^ UnsignedShiftRight(value, 31);
    }

    /// <summary>Upgrades a 64-bit seed to 128 bits without mixing.</summary>
    public static (long Lo, long Hi) UpgradeTo128BitUnmixed(long seed)
    {
        var lo = seed ^ SilverRatio64;
        var hi = unchecked(lo + GoldenRatio64);
        return (lo, hi);
    }

    /// <summary>Upgrades a 64-bit seed to 128 bits and mixes both halves.</summary>
    public static (long Lo, long Hi) UpgradeTo128Bit(long seed)
    {
        var (lo, hi) = UpgradeTo128BitUnmixed(seed);
        return (MixStafford13(lo), MixStafford13(hi));
    }

    /// <summary>
    /// Derives a 128-bit seed from a name using MD5: digest bytes 0..7 form the low
    /// word and bytes 8..15 the high word, both big-endian (vanilla layout).
    /// </summary>
    public static (long Lo, long Hi) SeedFromHashOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var bytes = System.Text.Encoding.UTF8.GetBytes(name);
        var digest = System.Security.Cryptography.MD5.HashData(bytes);
        var lo = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(digest.AsSpan(0, 8));
        var hi = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(digest.AsSpan(8, 8));
        return (lo, hi);
    }

    /// <summary>Hashes a block position into a seed (equivalent to <c>Mth.getSeed</c>).</summary>
    public static long PositionSeed(int x, int y, int z) => PositionalSeed.Of(x, y, z);

    /// <summary>Applies the unsigned (logical) right shift used by the Java implementation.</summary>
    public static long UnsignedShiftRight(long value, int bits) => unchecked((long)((ulong)value >> bits));
}
