// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Pure sky-visibility predicates matching the reference SkyRenderer (docs/specs/render/sky-celestial.md 2).
/// </summary>
public static class SkyVisibility
{
    /// <summary>
    /// The reference draws the black bottom sky disc only when the eye is below the dimension horizon and the camera is not
    /// underwater: <c>eyeY - horizonY &lt; 0 &amp;&amp; !underwater</c> (SkyRenderer.shouldRenderDarkDisc).
    /// </summary>
    /// <param name="eyeY">Camera eye Y in blocks.</param>
    /// <param name="horizonY">The dimension's horizon height (overworld sea-level-like value).</param>
    /// <param name="underwater">Whether the camera is inside water.</param>
    public static bool ShouldRenderDarkDisc(float eyeY, float horizonY, bool underwater) =>
        eyeY - horizonY < 0f && !underwater;
}
