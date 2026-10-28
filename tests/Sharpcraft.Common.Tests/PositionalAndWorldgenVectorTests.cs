// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Random;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Replays section 2 of the reference vectors: positional factories, world-generation
/// seed mixers, slime chunks and per-key loot sequences
/// (see docs/analysis/common/random-vectors.md).
/// </summary>
public sealed class PositionalAndWorldgenVectorTests
{
    private const string VectorFile = "TestData/random-vectors.txt";

    // Fixed in the harness; the vectors are meaningless if these drift.
    private const int LargeFeatureSalt = 34187;
    private const long SlimeSalt = 987234911L;

    [Fact]
    public void PositionalAndWorldgenVectorsAreReplayedExactly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, VectorFile);
        Assert.True(File.Exists(path), $"vector file missing from test output: {path}");

        var replayed = 0;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('|');
            try
            {
                replayed += Replay(parts);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"vector line failed: {line}", ex);
            }
        }

        Assert.True(replayed >= 100, $"expected the section-2 vectors to be exercised, replayed {replayed}");
    }

    private static int Replay(string[] parts)
    {
        switch (parts[0])
        {
            case "posHash":
                Assert.Equal(Hex64(parts[4]), PositionalSeed.Of(Int(parts[1]), Int(parts[2]), Int(parts[3])));
                return 1;

            case "legacyPosAt":
                Assert.Equal(
                    Hex64(parts[5]),
                    new LegacyRandomSource(Long(parts[1])).ForkPositional().At(Int(parts[2]), Int(parts[3]), Int(parts[4]))
                        .NextLong());
                return 1;

            case "xoroPosAt":
                Assert.Equal(
                    Hex64(parts[5]),
                    new XoroshiroRandomSource(Long(parts[1])).ForkPositional().At(Int(parts[2]), Int(parts[3]), Int(parts[4]))
                        .NextLong());
                return 1;

            case "posHashOfName":
            {
                var seed = Long(parts[2]);
                var name = parts[3];
                IPositionalRandomFactory factory = parts[1] == "legacy"
                    ? new LegacyRandomSource(seed).ForkPositional()
                    : new XoroshiroRandomSource(seed).ForkPositional();

                var expected = Hex64(parts[4]);
                var actual = factory.FromHashOf(name).NextLong();
                Assert.True(expected == actual,
                    $"posHashOfName kind={parts[1]} seed={seed} name=\"{name}\" expected=0x{expected:X16} actual=0x{actual:X16}");
                return 1;
            }

            case "wgDecoration":
            {
                var worldgen = NewWorldgen();
                worldgen.SetDecorationSeed(Long(parts[1]), Int(parts[2]), Int(parts[3]));
                Assert.Equal(Hex64(parts[4]), worldgen.NextLong());
                return 1;
            }

            case "wgLargeFeature":
            {
                var worldgen = NewWorldgen();
                worldgen.SetLargeFeatureSeed(Long(parts[1]), Int(parts[2]), Int(parts[3]));
                Assert.Equal(Hex64(parts[4]), worldgen.NextLong());
                return 1;
            }

            case "wgSalt":
            {
                var worldgen = NewWorldgen();
                worldgen.SetLargeFeatureWithSalt(Long(parts[1]), Int(parts[2]), Int(parts[3]), LargeFeatureSalt);
                Assert.Equal(Hex64(parts[4]), worldgen.NextLong());
                return 1;
            }

            case "wgFeature":
            {
                var worldgen = NewWorldgen();
                worldgen.SetFeatureSeed(Long(parts[1]), Int(parts[2]), Int(parts[3]));
                Assert.Equal(Hex64(parts[4]), worldgen.NextLong());
                return 1;
            }

            case "slimeChunk":
                Assert.Equal(
                    Value64(parts[4]),
                    WorldgenRandom.SeedSlimeChunk(Int(parts[2]), Int(parts[3]), Long(parts[1]), SlimeSalt).NextLong());
                return 1;

            case "lootSequenceV2":
            {
                var sequence = new RandomSequence(Long(parts[1]), Identifier.Parse(parts[2]));
                for (var i = 3; i < parts.Length; i++)
                {
                    Assert.Equal(Hex64(parts[i]), sequence.Random.NextLong());
                }

                return parts.Length - 3;
            }


            case "gaussianLegacy":
            case "gaussianXoro":
            {
                IRandomSource source = parts[0] == "gaussianLegacy"
                    ? new LegacyRandomSource(Long(parts[1]))
                    : new XoroshiroRandomSource(Long(parts[1]));

                var count = 0;
                for (var i = 2; i < parts.Length; i++)
                {
                    Assert.Equal(Hex64(parts[i]), BitConverter.DoubleToInt64Bits(source.NextGaussian()));
                    count++;
                }

                return count;
            }

            default:
                return 0;
        }
    }

    private static WorldgenRandom NewWorldgen() =>
        new(WorldgenRandom.Create(WorldgenRandom.Algorithm.Xoroshiro, 0L));

    private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static long Long(string value) => long.Parse(value, CultureInfo.InvariantCulture);

    private static long Hex64(string value) =>
        unchecked((long)ulong.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    /// <summary>Accepts both "0x..." hex and plain decimal, so older captures stay usable.</summary>
    private static long Value64(string value) =>
        value.StartsWith("0x", StringComparison.Ordinal)
            ? Hex64(value)
            : long.Parse(value, CultureInfo.InvariantCulture);
}
