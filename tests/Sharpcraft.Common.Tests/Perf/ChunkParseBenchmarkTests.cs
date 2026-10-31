// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Perf;

/// <summary>
/// Cost of decoding a chunk packet, the other half of the streaming chain (matrix row 3).
/// </summary>
/// <remarks>The point is to compare it with meshing, so the unit is milliseconds per packet.</remarks>
public sealed class ChunkParseBenchmarkTests
{
    [Fact]
    public void ParseCostPerPacketIsMeasuredAndReported()
    {
        var body = ChunkPacketTests.BuildChunkPacketForTests();

        ChunkPacketParser.TryRead(body, sectionCount: 2, minSectionY: -4, airState: 0, out _);

        const int iterations = 500;
        var watch = Stopwatch.StartNew();

        for (var index = 0; index < iterations; index++)
        {
            ChunkPacketParser.TryRead(body, sectionCount: 2, minSectionY: -4, airState: 0, out _);
        }

        watch.Stop();

        Console.WriteLine($"BENCH parse_ms_per_packet={watch.Elapsed.TotalMilliseconds / iterations:F3} bytes={body.Length}");

        Assert.True(body.Length > 0);
    }
}
