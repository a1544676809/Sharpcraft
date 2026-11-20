// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

public sealed class ChunkBatchSizeCalculatorTests
{
    [Fact]
    public void InitialDesiredChunksPerTickMatchesVanillaDefault()
    {
        var calc = new ChunkBatchSizeCalculator();
        Assert.Equal(3.5f, calc.DesiredChunksPerTick);
    }

    [Fact]
    public void EmaClampingAndSampleWeightCapMatchVanilla()
    {
        var calc = new ChunkBatchSizeCalculator();

        // Zero batch size is ignored.
        calc.RecordBatchDurationNanos(10_000_000.0, 0);
        Assert.Equal(3.5f, calc.DesiredChunksPerTick);

        // Initial aggregated = 2_000_000, weight = 1.
        // Input 100_000_000 / 1 is clamped to 2_000_000 * 3 = 6_000_000.
        // New aggregated = (2_000_000 * 1 + 6_000_000) / 2 = 4_000_000, weight = 2.
        calc.RecordBatchDurationNanos(100_000_000.0, 1);
        Assert.Equal((float)(7_000_000.0 / 4_000_000.0), calc.DesiredChunksPerTick, precision: 5);

        // Input 100 / 1 is clamped to 4_000_000 / 3.
        // New aggregated = (4_000_000 * 2 + 4_000_000 / 3) / 3 = 2_800_000 / 0.9? -> (8_000_000 + 1_333_333.333...) / 3.
        double agg = 4_000_000.0;
        int weight = 2;
        double clampedLow = agg / 3.0;
        agg = ((agg * weight) + clampedLow) / (weight + 1);
        weight = 3;
        calc.RecordBatchDurationNanos(100.0, 1);
        Assert.Equal((float)(7_000_000.0 / agg), calc.DesiredChunksPerTick, precision: 5);

        // Advance past MaxOldSamplesWeight (49) and verify weight caps at 49.
        for (var i = 0; i < 60; i++)
        {
            double sample = 3_000_000.0;
            double clamped = Math.Clamp(sample, agg / 3.0, agg * 3.0);
            agg = ((agg * weight) + clamped) / (weight + 1);
            weight = Math.Min(49, weight + 1);
            calc.RecordBatchDurationNanos(sample, 1);
        }

        Assert.Equal(49, weight);
        Assert.Equal((float)(7_000_000.0 / agg), calc.DesiredChunksPerTick, precision: 5);
    }
}
