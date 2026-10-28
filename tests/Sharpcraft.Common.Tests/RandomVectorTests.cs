// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Random;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Replays the reference vectors captured from the official 26.3 jar
/// (tests/Sharpcraft.Common.Tests/TestData/random-vectors.txt,
/// see docs/analysis/common/random-vectors.md).
/// </summary>
/// <remarks>
/// Values are consumed in file order on a single instance per (generator, seed)
/// pair, exactly as the harness recorded them. Never edit the vector file to make
/// a test pass; regenerate it instead.
/// </remarks>
public sealed class RandomVectorTests
{
    private const string VectorFile = "TestData/random-vectors.txt";

    [Fact]
    public void ReferenceVectorsAreReplayedExactly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, VectorFile);
        Assert.True(File.Exists(path), $"vector file missing from test output: {path}");

        var lines = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('|'))
            .Where(parts => parts[0].StartsWith("legacy_", StringComparison.Ordinal) ||
                            parts[0].StartsWith("xoro_", StringComparison.Ordinal) ||
                            parts[0].StartsWith("create_", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(lines);

        var assertions = 0;
        foreach (var group in lines.GroupBy(parts => (Generator: parts[0].Split('_')[0], Seed: parts[1])))
        {
            var source = CreateSource(group.Key.Generator, long.Parse(group.Key.Seed, CultureInfo.InvariantCulture));

            foreach (var parts in group)
            {
                assertions += ReplayLine(source, parts);
            }
        }

        Assert.True(assertions >= 800, $"expected a substantial vector set, replayed {assertions} values");
    }

    [Fact]
    public void FactoryMatchesLegacyStream()
    {
        var factory = RandomSource.Create(42L);
        var legacy = new LegacyRandomSource(42L);

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(legacy.NextLong(), factory.NextLong());
        }
    }

    [Fact]
    public void ReseedingRestartsBothStreams()
    {
        var legacy = new LegacyRandomSource(7L);
        var firstLegacy = legacy.NextLong();
        legacy.SetSeed(7L);
        Assert.Equal(firstLegacy, legacy.NextLong());

        var xoroshiro = new XoroshiroRandomSource(7L);
        var firstXoroshiro = xoroshiro.NextLong();
        xoroshiro.SetSeed(7L);
        Assert.Equal(firstXoroshiro, xoroshiro.NextLong());
    }

    [Fact]
    public void ZeroStateIsReplacedLikeVanilla()
    {
        var zeroed = new Xoroshiro128PlusPlus(0L, 0L);
        var fallback = new Xoroshiro128PlusPlus(SeedSupport.GoldenRatio64, SeedSupport.SilverRatio64);

        Assert.Equal(fallback.NextLong(), zeroed.NextLong());
    }

    private static IRandomSource CreateSource(string generator, long seed) => generator switch
    {
        "legacy" => new LegacyRandomSource(seed),
        "xoro" => new XoroshiroRandomSource(seed),
        "create" => RandomSource.Create(seed),
        _ => throw new InvalidOperationException($"unknown generator '{generator}'")
    };

    private static int ReplayLine(IRandomSource source, string[] parts)
    {
        var kind = parts[0];
        var replayed = 0;

        switch (kind)
        {
            case "legacy_long":
            case "xoro_long":
            case "create_long":
                for (var i = 2; i < parts.Length; i++)
                {
                    var expected = unchecked((long)ParseHex(parts[i]));
                    Assert.Equal(expected, source.NextLong());
                    replayed++;
                }

                break;

            case "legacy_int":
            case "xoro_int":
            case "create_int":
                for (var i = 2; i < parts.Length; i++)
                {
                    var expected = unchecked((int)(uint)ParseHex(parts[i]));
                    Assert.Equal(expected, source.NextInt());
                    replayed++;
                }

                break;

            case "legacy_intBound":
            case "xoro_intBound":
            case "create_intBound":
            {
                var bound = int.Parse(parts[2], CultureInfo.InvariantCulture);
                for (var i = 3; i < parts.Length; i++)
                {
                    var expected = int.Parse(parts[i], CultureInfo.InvariantCulture);
                    Assert.Equal(expected, source.NextInt(bound));
                    replayed++;
                }

                break;
            }

            case "legacy_doubleBits":
            case "xoro_doubleBits":
            case "create_doubleBits":
                for (var i = 2; i < parts.Length; i++)
                {
                    var expected = unchecked((long)ParseHex(parts[i]));
                    var actual = BitConverter.DoubleToInt64Bits(source.NextDouble());
                    Assert.True(expected == actual,
                        $"double mismatch in {kind} at index {i - 2}: expected 0x{expected:X16} actual 0x{actual:X16}");
                    replayed++;
                }

                break;

            case "legacy_floatBits":
            case "xoro_floatBits":
            case "create_floatBits":
                for (var i = 2; i < parts.Length; i++)
                {
                    var expected = unchecked((int)(uint)ParseHex(parts[i]));
                    Assert.Equal(expected, BitConverter.SingleToInt32Bits(source.NextFloat()));
                    replayed++;
                }

                break;

            default:
                // Positional / sequence vectors are covered by later tasks.
                break;
        }

        return replayed;
    }

    private static ulong ParseHex(string value) =>
        ulong.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
