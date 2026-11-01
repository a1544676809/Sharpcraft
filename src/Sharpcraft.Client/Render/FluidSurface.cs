// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Pure fluid surface math (docs/specs/render/fluid-rendering.md 2-3): surface height from amount, and the vanilla
/// per-corner weighted average of the four neighbouring heights.
/// </summary>
/// <remarks>
/// Heights are in block units. A non-fluid solid neighbour is passed as a negative height and contributes nothing
/// (matching the reference where <c>getHeight</c> returns -1 for a solid non-fluid block).
/// </remarks>
public static class FluidSurface
{
    /// <summary>Surface height of one block's own fluid: <c>amount / 9</c> (source amount 8 → 8/9).</summary>
    public static float OwnHeight(int amount) => amount / 9f;

    /// <summary>
    /// Vanilla <c>calculateAverageHeight</c> for one corner: if either edge neighbour is a full column (height &gt;= 1)
    /// the corner is 1; otherwise a weighted average of the corner cell (only when an edge has fluid), self and the two
    /// edge neighbours, where a height &gt;= 0.8 counts ten times so raised corners dominate.
    /// </summary>
    /// <param name="self">This block's own surface height.</param>
    /// <param name="north">Height of one edge neighbour (negative when it is a solid non-fluid block).</param>
    /// <param name="east">Height of the other edge neighbour (negative when solid non-fluid).</param>
    /// <param name="corner">Height of the diagonal cell (negative when solid non-fluid).</param>
    public static float AverageCorner(float self, float north, float east, float corner)
    {
        if (north >= 1f || east >= 1f)
        {
            return 1f;
        }

        var sum = 0f;
        var weight = 0f;

        // The diagonal only participates when at least one edge carries fluid; otherwise it is an unrelated cell. Per the
        // reference, a full diagonal column (height >= 1) lifts the corner straight to 1 without averaging.
        if (north > 0f || east > 0f)
        {
            if (corner >= 1f)
            {
                return 1f;
            }

            AddWeighted(ref sum, ref weight, corner);
        }

        AddWeighted(ref sum, ref weight, self);
        AddWeighted(ref sum, ref weight, north);
        AddWeighted(ref sum, ref weight, east);

        return weight == 0f ? 0f : sum / weight;
    }

    private static void AddWeighted(ref float sum, ref float weight, float height)
    {
        if (height >= 0.8f)
        {
            sum += height * 10f;
            weight += 10f;
        }
        else if (height >= 0f)
        {
            sum += height;
            weight += 1f;
        }
    }
}
