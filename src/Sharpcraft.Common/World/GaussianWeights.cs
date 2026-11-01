// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

/// <summary>
/// The separable 7-tap gaussian kernel the reference uses to blend biome attributes near the camera
/// (docs/modules/weather-and-sky-color.md 6.2; <c>GaussianSampler</c>). The 3D weight of an offset is the product of the
/// per-axis taps; the whole cube sums to <see cref="TotalWeight"/>.
/// </summary>
public static class GaussianWeights
{
    /// <summary>The 7 per-axis taps (offsets −3..3).</summary>
    public static readonly int[] Kernel = [0, 1, 4, 6, 4, 1, 0];

    /// <summary>Greatest per-axis offset the kernel covers.</summary>
    public const int Radius = 3;

    /// <summary>Sum of every 3D weight in the cube = (Σkernel)³ = 16³.</summary>
    public const int TotalWeight = 16 * 16 * 16;

    /// <summary>3D weight of the offset (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>); 0 outside the cube.</summary>
    public static int Weight(int dx, int dy, int dz)
    {
        if (Math.Abs(dx) > Radius || Math.Abs(dy) > Radius || Math.Abs(dz) > Radius)
        {
            return 0;
        }

        return Kernel[dx + Radius] * Kernel[dy + Radius] * Kernel[dz + Radius];
    }
}
