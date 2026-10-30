// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Hierarchical depth occlusion test: is a box certainly hidden behind what is already drawn.
/// </summary>
/// <remarks>
/// The test must be conservative in one direction only: it may keep a box that is actually hidden, but it must never drop
/// a box that is visible, because that shows as holes. Hence the box is compared by its <em>nearest</em> depth against the
/// <em>farthest</em> depth in the covered region, and the region is the box's screen rectangle unioned over all screen
/// pixels it touches.
/// </remarks>
public static class OcclusionCulling
{
    /// <summary>
    /// Screen rectangle and nearest depth of a box, in zero-to-one screen space.
    /// </summary>
    /// <param name="centre">Box centre in world space.</param>
    /// <param name="halfExtent">Half the box size.</param>
    /// <param name="viewProjection">Combined matrix.</param>
    /// <param name="min">Rectangle minimum.</param>
    /// <param name="max">Rectangle maximum.</param>
    /// <param name="nearestDepth">Smallest depth of the box; zero-to-one.</param>
    /// <returns>False when the box is entirely behind the camera and nothing can be said.</returns>
    public static bool TryProjectBox(
        Vector3 centre,
        Vector3 halfExtent,
        Matrix4x4 viewProjection,
        out Vector2 min,
        out Vector2 max,
        out float nearestDepth)
    {
        min = new Vector2(float.MaxValue);
        max = new Vector2(float.MinValue);
        nearestDepth = float.MaxValue;

        var anyInFront = false;

        for (var corner = 0; corner < 8; corner++)
        {
            var offset = new Vector3(
                (corner & 1) == 0 ? -halfExtent.X : halfExtent.X,
                (corner & 2) == 0 ? -halfExtent.Y : halfExtent.Y,
                (corner & 4) == 0 ? -halfExtent.Z : halfExtent.Z);

            var clip = Vector4.Transform(new Vector4(centre + offset, 1f), viewProjection);

            if (clip.W <= 1e-6f)
            {
                // A corner behind the camera makes the rectangle unbounded; keeping the box is the safe answer.
                return false;
            }

            var screen = new Vector2(
                ((clip.X / clip.W) * 0.5f) + 0.5f,
                0.5f - ((clip.Y / clip.W) * 0.5f));

            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
            nearestDepth = MathF.Min(nearestDepth, clip.Z / clip.W);
            anyInFront = true;
        }

        return anyInFront;
    }

    /// <summary>
    /// True when the box is hidden: its nearest point is still farther than the farthest opaque depth under its rectangle.
    /// </summary>
    /// <param name="nearestDepth">Box nearest depth.</param>
    /// <param name="farthestOccluderDepth">Largest depth already drawn under the box's rectangle.</param>
    /// <param name="bias">Tolerance that keeps the test conservative.</param>
    public static bool IsOccluded(float nearestDepth, float farthestOccluderDepth, float bias = 1e-5f) =>
        nearestDepth > farthestOccluderDepth + bias;
}
