// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Star field equivalence (docs/specs/render/sky-celestial.md 3): the seeded LCG matches java.util.Random, and the
/// generated field is deterministic with every star on the distance-100 sphere.
/// </summary>
public sealed class StarFieldTests
{
    [Fact]
    public void LegacyRandomMatchesJavaUtilRandom()
    {
        // Known reference values for new java.util.Random(0).
        Assert.Equal(-1155484576, new LegacyRandom(0L).Next(32));

        var random = new LegacyRandom(0L);
        Assert.Equal(0.73096776f, random.NextFloat(), 6);
    }

    [Fact]
    public void StarFieldIsDeterministicAndOnTheStarSphere()
    {
        var first = StarField.Build(out var keptFirst);
        var second = StarField.Build(out var keptSecond);

        Assert.Equal(keptFirst, keptSecond);
        Assert.InRange(keptFirst, 1, StarField.CandidateCount);
        Assert.Equal(keptFirst * 4, first.Length);

        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i], second[i]);
        }

        for (var star = 0; star < keptFirst; star++)
        {
            var centre = (first[star * 4] + first[(star * 4) + 1] + first[(star * 4) + 2] + first[(star * 4) + 3]) / 4f;
            Assert.Equal(StarField.Distance, centre.Length(), 1);
        }
    }
}
