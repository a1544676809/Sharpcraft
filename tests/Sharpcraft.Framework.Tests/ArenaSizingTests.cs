// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Arena sizing policy (docs/specs/network/play-movement.md 7.27). Growth is a pure decision, so it is tested
/// without a GPU: what matters is that a known view distance turns many doublings into one deliberate presize,
/// and that a capacity is never chosen that cannot hold the incoming geometry.
/// </summary>
public sealed class ArenaSizingTests
{
    [Fact]
    public void WithoutAViewDistanceItJustDoubles()
    {
        var capacity = ArenaSizing.NextCapacity(
            currentCapacity: 1 << 20,
            needed: 1 << 21,
            residentBytes: 1 << 21,
            residentChunks: 64,
            expectedChunks: 0,
            alreadyPresized: false);

        Assert.Equal(1u << 21, (uint)capacity);
    }

    [Fact]
    public void AViewDistancePresizesForTheWholeView()
    {
        // 64 chunks resident using 64 KiB each; a 289 chunk view needs about 18 MiB plus slack.
        var capacity = ArenaSizing.NextCapacity(
            currentCapacity: 1 << 20,
            needed: 64 * 65536,
            residentBytes: 64 * 65536,
            residentChunks: 64,
            expectedChunks: 289,
            alreadyPresized: false);

        var projected = (ulong)(65536 * 289 * ArenaSizing.PresizeSlack);
        Assert.True(capacity >= projected, $"capacity {capacity} should cover the projected {projected}");
        Assert.True(capacity > 64 * 65536 * 2, "presizing must beat a single doubling");
    }

    [Fact]
    public void PresizingHappensOnlyOnce()
    {
        var capacity = ArenaSizing.NextCapacity(
            currentCapacity: 1 << 20,
            needed: 1 << 21,
            residentBytes: 1 << 21,
            residentChunks: 200,
            expectedChunks: 289,
            alreadyPresized: true);

        Assert.Equal(1u << 21, (uint)capacity);
    }

    [Fact]
    public void TooFewSamplesStillDouble()
    {
        // Four chunks are not enough to estimate an average, so the arena keeps the old behaviour.
        var capacity = ArenaSizing.NextCapacity(
            currentCapacity: 1 << 20,
            needed: 1 << 20,
            residentBytes: 4 * 65536,
            residentChunks: 4,
            expectedChunks: 289,
            alreadyPresized: false);

        Assert.Equal(1u << 21, (uint)capacity);
    }

    [Fact]
    public void TheChosenCapacityAlwaysHoldsTheIncomingGeometry()
    {
        // A single huge mesh must not be truncated by a modest view distance estimate.
        var capacity = ArenaSizing.NextCapacity(
            currentCapacity: 1 << 20,
            needed: 40ul << 20,
            residentBytes: 20ul << 20,
            residentChunks: 40,
            expectedChunks: 289,
            alreadyPresized: false);

        Assert.True(capacity >= 40ul << 20);
    }
}
