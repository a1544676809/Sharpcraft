// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Pure celestial geometry matching the reference SkyRenderer transforms
/// (docs/specs/render/sky-celestial.md 2). The renderer feeds these vertices straight into the sky pass.
/// </summary>
/// <remarks>
/// Reference order (renderSunMoonAndStars + applyCelestialBodyTransform): rotate Y by -90 degrees, rotate X by the body
/// angle, translate (0, height, 0), scale (size, 1, size). The body quad is the XZ plane from -1..1 at y=0.
/// </remarks>
public static class CelestialGeometry
{
    private static readonly (float X, float Z)[] Quad =
    [
        (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)
    ];

    /// <summary>Corner positions of a sun/moon quad for an angle (radians), height and size.</summary>
    public static Vector3[] CelestialQuad(float angleRadians, float height, float size)
    {
        var cos = MathF.Cos(angleRadians);
        var sin = MathF.Sin(angleRadians);
        var result = new Vector3[Quad.Length];

        for (var i = 0; i < Quad.Length; i++)
        {
            var (qx, qz) = Quad[i];
            var sx = qx * size;
            var sz = qz * size;

            // translate (0, height, 0)
            var y = height;

            // rotate about X
            var ry = (y * cos) - (sz * sin);
            var rz = (y * sin) + (sz * cos);
            var rx = sx;

            // rotate about Y by -90 degrees: x' = -z, z' = x
            result[i] = new Vector3(-rz, ry, rx);
        }

        return result;
    }

    /// <summary>
    /// The sunrise/sunset triangle fan in local space: centre (0,100,0) then `steps` ring points at 120/40 radius, as in
    /// the reference buildSunriseFan (18 vertices for 16 steps).
    /// </summary>
    public static Vector3[] SunriseFanLocal(int steps = 16)
    {
        var result = new Vector3[steps + 2];
        result[0] = new Vector3(0f, 100f, 0f);

        for (var i = 0; i <= steps; i++)
        {
            var angle = i * MathF.Tau / steps;
            var sinAngle = MathF.Sin(angle);
            var cosAngle = MathF.Cos(angle);
            result[i + 1] = new Vector3(sinAngle * 120f, cosAngle * 120f, -cosAngle * 40f);
        }

        return result;
    }

    /// <summary>
    /// The sunrise/sunset triangle fan (18 vertices) after the reference transform chain
    /// <c>RotX(90°) · RotZ(angle) · Scale(1,1,alpha)</c> applied to <see cref="SunriseFanLocal"/>, in the same local sky
    /// frame as the sun/moon (add the eye per frame). The scale Z by alpha is what fades the band out.
    /// </summary>
    /// <remarks>Expanded per vertex: x' = px·cos − py·sin, y' = −pz·alpha, z' = px·sin + py·cos.</remarks>
    public static Vector3[] SunriseFanWorld(float angleRadians, float alpha)
    {
        var local = SunriseFanLocal();
        var result = new Vector3[local.Length];
        var cos = MathF.Cos(angleRadians);
        var sin = MathF.Sin(angleRadians);

        for (var i = 0; i < local.Length; i++)
        {
            var p = local[i];
            result[i] = new Vector3((p.X * cos) - (p.Y * sin), -(p.Z * alpha), (p.X * sin) + (p.Y * cos));
        }

        return result;
    }

    /// <summary>
    /// Applies the sky body rotation — <c>RotX(angle)</c> then <c>RotY(−90°)</c> — to star-field points, matching the
    /// reference renderSunMoonAndStars pose order. Star positions are already at their final distance, so there is no
    /// translate/scale here.
    /// </summary>
    public static void SkyRotate(ReadOnlySpan<Vector3> source, float angleRadians, Span<Vector3> destination)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("destination must hold every source point", nameof(destination));
        }

        var cos = MathF.Cos(angleRadians);
        var sin = MathF.Sin(angleRadians);

        for (var i = 0; i < source.Length; i++)
        {
            var p = source[i];

            // rotate about X, then about Y by -90 degrees: x' = -z, z' = x
            var ry = (p.Y * cos) - (p.Z * sin);
            var rz = (p.Y * sin) + (p.Z * cos);
            destination[i] = new Vector3(-rz, ry, p.X);
        }
    }
}
