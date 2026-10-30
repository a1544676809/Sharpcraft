// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Launcher.Benchmarking;
using Xunit;

namespace Sharpcraft.Launcher.Tests.Benchmarking;

/// <summary>
/// The benchmark chunk buffer (benchmarks/chunk-mesh/README.md): it exists only behind the dev switch, and releases on
/// whichever comes first, the expected count or the window.
/// </summary>
public sealed class ChunkBufferHarnessTests
{
    [Fact]
    public void WithoutTheDevSwitchNothingIsHeld()
    {
        var previous = Environment.GetEnvironmentVariable(ChunkBufferHarness.DevSwitch);
        Environment.SetEnvironmentVariable(ChunkBufferHarness.DevSwitch, null);

        try
        {
            Assert.Null(ChunkBufferHarness.FromEnvironment(4));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ChunkBufferHarness.DevSwitch, previous);
        }
    }

    [Fact]
    public void ReachingTheExpectedCountReleasesEverything()
    {
        var harness = Build(2, "30");

        Assert.True(harness.Hold("a"));
        Assert.True(harness.Hold("b"));

        Assert.False(harness.Holding);
        Assert.Equal(2, harness.TakeReleased().Count);
        Assert.Equal(0, harness.HeldCount);
    }

    [Fact]
    public void AfterReleaseLaterChunksAreNotHeld()
    {
        var harness = Build(1, "30");
        harness.Hold("a");

        Assert.False(harness.Hold("b"));
    }

    private static ChunkBufferHarness Build(int expected, string seconds)
    {
        Environment.SetEnvironmentVariable(ChunkBufferHarness.DevSwitch, "1");
        Environment.SetEnvironmentVariable(ChunkBufferHarness.BufferSwitch, seconds);

        try
        {
            var harness = ChunkBufferHarness.FromEnvironment(expected);
            Assert.NotNull(harness);
            return harness!;
        }
        finally
        {
            Environment.SetEnvironmentVariable(ChunkBufferHarness.DevSwitch, null);
            Environment.SetEnvironmentVariable(ChunkBufferHarness.BufferSwitch, null);
        }
    }
}
