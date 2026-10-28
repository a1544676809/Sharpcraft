// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Measures managed allocations performed by the current thread while executing an
/// action. Used to prove the "zero steady-state allocation" rule of AGENTS.md 3.6
/// instead of judging code by its syntax.
/// </summary>
internal static class AllocationAssert
{
    /// <summary>
    /// Returns the bytes allocated on the current thread by <paramref name="action"/>.
    /// The action runs once as a warm-up (JIT, static initialisation, delegate caching)
    /// and is excluded from the measurement.
    /// </summary>
    internal static long MeasureBytes(Action action)
    {
        action();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Asserts that the action performs no managed allocation at all.</summary>
    internal static void Zero(Action action, string? because = null)
    {
        var allocated = MeasureBytes(action);
        Assert.True(allocated == 0, $"expected zero allocation but measured {allocated} bytes. {because}");
    }

    /// <summary>Asserts that the action does allocate, proving the measurement is sensitive.</summary>
    internal static void NotZero(Action action, string? because = null)
    {
        var allocated = MeasureBytes(action);
        Assert.True(allocated > 0, $"expected a non-zero allocation but measured {allocated} bytes. {because}");
    }
}
