// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Celestial geometry equivalence (docs/specs/render/sky-celestial.md 2): the sun/moon quad under the reference
/// Scale->Translate->RotX->RotY(-90) chain, and the sunrise fan vertex formula. Expected values are hand-computed.
/// </summary>
public sealed class CelestialGeometryTests
{
    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Z, actual.Z, 3);
    }

    [Fact]
    public void SunQuadAtZeroAngleMatchesHandComputation()
    {
        var quad = CelestialGeometry.CelestialQuad(0f, height: 100f, size: 30f);

        // corner (-1,-1): scaled (-30,-30) -> y=100 -> RotX0 -> (-30,100,-30) -> RotY(-90) -> (30,100,-30)
        AssertClose(new Vector3(30f, 100f, -30f), quad[0]);
        // corner (1,1): -> (-30,100,30)
        AssertClose(new Vector3(-30f, 100f, 30f), quad[2]);
    }

    [Fact]
    public void SunQuadAtQuarterTurnMatchesHandComputation()
    {
        var quad = CelestialGeometry.CelestialQuad(MathF.PI / 2f, height: 100f, size: 30f);

        // corner (-1,-1): scaled (-30,-30), y=100; RotX(90): y = -(-30)=30, z = 100; -> (-30,30,100); RotY(-90) -> (-100,30,-30)
        AssertClose(new Vector3(-100f, 30f, -30f), quad[0]);
    }

    [Fact]
    public void SunriseFanHasCentreAndRingPoints()
    {
        var fan = CelestialGeometry.SunriseFanLocal(16);

        Assert.Equal(18, fan.Length);
        AssertClose(new Vector3(0f, 100f, 0f), fan[0]);
        // i=0 (and i=16): angle 0 -> (0,120,-40)
        AssertClose(new Vector3(0f, 120f, -40f), fan[1]);
        AssertClose(new Vector3(0f, 120f, -40f), fan[17]);
    }
}
