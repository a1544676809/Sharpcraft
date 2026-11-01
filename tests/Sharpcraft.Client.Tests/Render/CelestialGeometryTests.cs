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
    public void QuadUvRectControlsCornerAssignmentForMoonFlip()
    {
        Vector3[] corners = [new(-1, 0, -1), new(1, 0, -1), new(1, 0, 1), new(-1, 0, 1)];

        var (forward, _) = CelestialMeshBuilder.Quad(corners, (0f, 0f, 1f, 1f));
        Assert.Equal(0f, forward[3], 3);
        Assert.Equal(0f, forward[4], 3);
        Assert.Equal(1f, forward[2 * CelestialMeshBuilder.FloatsPerVertex + 3], 3);
        Assert.Equal(1f, forward[2 * CelestialMeshBuilder.FloatsPerVertex + 4], 3);

        // The moon uses the reversed rect (1,1,0,0), which is a 180 degree rotation of the sprite.
        var (flipped, _) = CelestialMeshBuilder.Quad(corners, (1f, 1f, 0f, 0f));
        Assert.Equal(1f, flipped[3], 3);
        Assert.Equal(1f, flipped[4], 3);
        Assert.Equal(0f, flipped[2 * CelestialMeshBuilder.FloatsPerVertex + 3], 3);
        Assert.Equal(0f, flipped[2 * CelestialMeshBuilder.FloatsPerVertex + 4], 3);
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

    [Fact]
    public void SunriseFanWorldMatchesTheExpandedRotX90RotZTransform()
    {
        // angle 90deg, alpha 1: x' = -py, y' = -pz, z' = px.
        var fan = CelestialGeometry.SunriseFanWorld(MathF.PI / 2f, 1f);
        AssertClose(new Vector3(-100f, 0f, 0f), fan[0]);   // centre (0,100,0)
        AssertClose(new Vector3(-120f, 40f, 0f), fan[1]);  // ring (0,120,-40)

        // alpha only scales the y output: y' = -pz*alpha.
        var half = CelestialGeometry.SunriseFanWorld(MathF.PI / 2f, 0.5f);
        AssertClose(new Vector3(-120f, 20f, 0f), half[1]);
    }

    [Fact]
    public void SkyRotateMatchesRotXThenRotYMinusNinety()
    {
        Span<Vector3> destination = stackalloc Vector3[2];
        CelestialGeometry.SkyRotate([new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f)], 0f, destination);

        // RotY(-90) maps +X to +Z and leaves +Y unchanged.
        AssertClose(new Vector3(0f, 0f, 1f), destination[0]);
        AssertClose(new Vector3(0f, 1f, 0f), destination[1]);
    }

    [Fact]
    public void SkyDiscMatchesTheBuildSkyDiscFormula()
    {
        var top = CelestialGeometry.SkyDisc(16f);
        Assert.Equal(10, top.Length);
        AssertClose(new Vector3(0f, 16f, 0f), top[0]);                 // centre
        AssertClose(new Vector3(-512f, 16f, 0f), top[1]);              // i = -180
        AssertClose(new Vector3(-362.039f, 16f, -362.039f), top[2]);   // i = -135
        AssertClose(new Vector3(512f, 16f, 0f), top[5]);               // i = 0
        AssertClose(new Vector3(0f, 16f, 512f), top[7]);               // i = 90

        // The bottom disc negates x (signum(-16) = -1) and sits at y = -16.
        var bottom = CelestialGeometry.SkyDisc(-16f);
        AssertClose(new Vector3(512f, -16f, 0f), bottom[1]);
        AssertClose(new Vector3(0f, -16f, 512f), bottom[7]);
    }
}
