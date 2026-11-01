// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Rebuilds the reference star field (SkyRenderer.buildStars): 1500 candidate points drawn from Java's seeded LCG
/// (seed 10842), each kept only when its squared length is in (0.010000001, 1.0), grown to distance 100 and turned into a
/// small quad (docs/specs/render/sky-celestial.md).
/// </summary>
/// <remarks>
/// The reference draws each star with the solid-colour <c>core/stars</c> shader, so a star is a ~0.15-unit square about a
/// pixel wide at distance 100; the per-star in-plane spin is not observable. This port keeps the exact LCG, the accept
/// test, the star centres and sizes, which is what fixes the field's count and layout.
/// </remarks>
public static class StarField
{
    /// <summary>Candidate count (kept stars are fewer; the reference keeps the ones passing the length test).</summary>
    public const int CandidateCount = 1500;

    /// <summary>Stars sit on a sphere of this radius.</summary>
    public const float Distance = 100f;

    /// <summary>
    /// Builds the four corner positions per kept star, in the local sky frame (before the per-frame star angle).
    /// </summary>
    /// <param name="keptStars">Number of stars that survived the length test.</param>
    public static Vector3[] Build(out int keptStars)
    {
        var random = new LegacyRandom(10842L);
        var corners = new List<Vector3>(CandidateCount * 4);

        for (var i = 0; i < CandidateCount; i++)
        {
            var x = (random.NextFloat() * 2f) - 1f;
            var y = (random.NextFloat() * 2f) - 1f;
            var z = (random.NextFloat() * 2f) - 1f;
            var size = 0.15f + (random.NextFloat() * 0.1f);
            var lengthSquared = (x * x) + (y * y) + (z * z);

            if (lengthSquared <= 0.010000001f || lengthSquared >= 1f)
            {
                continue;
            }

            var center = Vector3.Normalize(new Vector3(x, y, z)) * Distance;
            var spin = (float)(random.NextDouble() * Math.PI * 2.0);
            AddQuad(corners, center, size, spin);
        }

        keptStars = corners.Count / 4;
        return [.. corners];
    }

    private static void AddQuad(List<Vector3> corners, Vector3 center, float size, float spin)
    {
        // Billboard facing the origin. A ~0.15-unit square at distance 100 is about a pixel, so the reference's
        // per-star orientation is not observable; the random spin still matches the reference's use of nextDouble.
        var normal = Vector3.Normalize(center);
        var reference = MathF.Abs(normal.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(reference, normal));
        var up = Vector3.Cross(normal, right);
        var cos = MathF.Cos(spin);
        var sin = MathF.Sin(spin);
        var r = (right * cos) + (up * sin);
        var u = (up * cos) - (right * sin);

        corners.Add(center + (r * -size) + (u * -size));
        corners.Add(center + (r * size) + (u * -size));
        corners.Add(center + (r * size) + (u * size));
        corners.Add(center + (r * -size) + (u * size));
    }
}

/// <summary>
/// Java <see cref="System.Random"/>-compatible 48-bit LCG, so a star field seeded with 10842 matches the reference's
/// <c>RandomSource.createThreadLocalInstance(10842)</c> sequence exactly.
/// </summary>
public sealed class LegacyRandom
{
    private const long Multiplier = 0x5DEECE66DL;
    private const long Addend = 0xBL;
    private const long Mask = (1L << 48) - 1;

    private long _seed;

    /// <summary>Seeds the generator the same way <c>java.util.Random(long)</c> does.</summary>
    public LegacyRandom(long seed) => _seed = (seed ^ Multiplier) & Mask;

    /// <summary>Next <paramref name="bits"/> high bits of the state, matching <c>java.util.Random.next(int)</c>.</summary>
    public int Next(int bits)
    {
        _seed = ((_seed * Multiplier) + Addend) & Mask;
        return (int)((ulong)_seed >> (48 - bits));
    }

    /// <summary>Uniform float in [0,1), matching <c>java.util.Random.nextFloat()</c>.</summary>
    public float NextFloat() => Next(24) / (float)(1 << 24);

    /// <summary>Uniform double in [0,1), matching <c>java.util.Random.nextDouble()</c>.</summary>
    public double NextDouble() => (((long)Next(26) << 27) + Next(27)) / (double)(1L << 53);
}
