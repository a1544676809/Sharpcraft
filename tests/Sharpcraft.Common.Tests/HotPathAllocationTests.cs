// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Common.Diagnostics;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Proves the measurement mechanism behind the hot-path rule (AGENTS.md 3.6) before any
/// real hot code exists: a loop-based implementation must measure zero, and an obviously
/// allocating one must measure non-zero, otherwise the rule could never be enforced.
/// </summary>
public sealed class HotPathAllocationTests
{
    private static readonly int[] Values = [3, 1, 4, 1, 5, 9, 2, 6];

    [Fact]
    public void LoopImplementationAllocatesNothing()
    {
        AllocationAssert.Zero(() => TickSum(), "a plain indexed loop over an existing array must not allocate");
    }

    [Fact]
    public void LinqImplementationAllocates()
    {
        AllocationAssert.NotZero(() => TickSumLinq(), "a LINQ pipeline inside a hot path allocates and is therefore forbidden");
    }

    [Fact]
    public void BothImplementationsProduceTheSameResult()
    {
        Assert.Equal(TickSum(), TickSumLinq());
    }

    [HotPath("allocation test sample: per-tick summation")]
    private static int TickSum()
    {
        var total = 0;
        for (var i = 0; i < Values.Length; i++)
        {
            total += Values[i];
        }

        return total;
    }

    private static int TickSumLinq() => Values.Where(value => value >= 0).Sum();
}
