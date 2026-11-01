// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Per-vertex smooth light: the reference <c>LightCoordsUtil.smoothBlend</c> for a full cube face — the average of the
/// three neighbouring cells and the centre cell, after the zero-neighbour correction
/// (docs/analysis/render/lighting-color-space.md #3).
/// </summary>
/// <remarks>
/// Reference rule, when the centre is bright (<c>sky &gt; 2 || block &gt; 2</c>): a neighbour with no light at all
/// (<c>sky == 0 &amp;&amp; block == 0</c>) is replaced by the centre; a neighbour whose only darkness is sky
/// (<c>sky == 0</c>) inherits the centre's sky. The four cells are then averaged independently per channel with an
/// integer shift, which is what a full cube face needs. (The bilinear <c>smoothWeightedBlend</c> branch only applies to
/// partial, non-cubic model faces, which this renderer does not emit.)
/// </remarks>
public static class SmoothLighting
{
    /// <summary>Blends the centre cell with three neighbouring cells into one smooth light pair.</summary>
    public static (int Sky, int Block) Blend(
        int centerSky,
        int centerBlock,
        int sky1,
        int block1,
        int sky2,
        int block2,
        int sky3,
        int block3)
    {
        var brightCenter = centerSky > 2 || centerBlock > 2;
        (int Sky, int Block) Fix(int sky, int block)
        {
            if (!brightCenter)
            {
                return (sky, block);
            }

            if (sky == 0 && block == 0)
            {
                return (centerSky, centerBlock);
            }

            return sky == 0 ? (centerSky, block) : (sky, block);
        }

        var n1 = Fix(sky1, block1);
        var n2 = Fix(sky2, block2);
        var n3 = Fix(sky3, block3);

        return ((centerSky + n1.Sky + n2.Sky + n3.Sky) >> 2, (centerBlock + n1.Block + n2.Block + n3.Block) >> 2);
    }
}
