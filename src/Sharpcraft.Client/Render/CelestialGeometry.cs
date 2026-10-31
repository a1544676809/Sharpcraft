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
    /// the reference buildSunriseFan (18 vertices for 16 steps). Colour alpha is applied by the renderer's Z scale.
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
}
