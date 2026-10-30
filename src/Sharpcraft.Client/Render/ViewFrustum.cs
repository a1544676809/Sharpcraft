// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Six planes enclosing what the camera can see, used to skip entities outside it.
/// </summary>
/// <remarks>
/// The planes come from the combined view-projection matrix (the standard row-combination extraction), so the caller
/// does not have to repeat the camera's field of view or aspect ratio here and risk the two drifting apart.
/// </remarks>
public struct ViewFrustum
{
    private Plane _left;
    private Plane _right;
    private Plane _bottom;
    private Plane _top;
    private Plane _near;
    private Plane _far;

    /// <summary>Builds the frustum from a combined view-projection matrix.</summary>
    /// <param name="viewProjection">View times projection.</param>
    /// <returns>The enclosing frustum.</returns>
    public static ViewFrustum FromViewProjection(Matrix4x4 viewProjection)
    {
        var frustum = new ViewFrustum
        {
            _left = Normalize(new Plane(
                viewProjection.M14 + viewProjection.M11,
                viewProjection.M24 + viewProjection.M21,
                viewProjection.M34 + viewProjection.M31,
                viewProjection.M44 + viewProjection.M41)),
            _right = Normalize(new Plane(
                viewProjection.M14 - viewProjection.M11,
                viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31,
                viewProjection.M44 - viewProjection.M41)),
            _bottom = Normalize(new Plane(
                viewProjection.M14 + viewProjection.M12,
                viewProjection.M24 + viewProjection.M22,
                viewProjection.M34 + viewProjection.M32,
                viewProjection.M44 + viewProjection.M42)),
            _top = Normalize(new Plane(
                viewProjection.M14 - viewProjection.M12,
                viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32,
                viewProjection.M44 - viewProjection.M42)),
            _near = Normalize(new Plane(
                viewProjection.M13,
                viewProjection.M23,
                viewProjection.M33,
                viewProjection.M43)),
            _far = Normalize(new Plane(
                viewProjection.M14 - viewProjection.M13,
                viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33,
                viewProjection.M44 - viewProjection.M43))
        };

        return frustum;
    }

    /// <summary>
    /// True when an axis-aligned box has any part inside the frustum.
    /// </summary>
    /// <param name="centre">Box centre.</param>
    /// <param name="halfExtent">Half the box size on each axis.</param>
    public readonly bool IsBoxVisible(Vector3 centre, Vector3 halfExtent)
    {
        return Inside(_left) && Inside(_right) && Inside(_bottom) && Inside(_top) && Inside(_near) && Inside(_far);

        bool Inside(Plane plane)
        {
            // Radius of the box projected onto the plane normal: the support point test, which is exact for boxes.
            var radius =
                (MathF.Abs(plane.Normal.X) * halfExtent.X) +
                (MathF.Abs(plane.Normal.Y) * halfExtent.Y) +
                (MathF.Abs(plane.Normal.Z) * halfExtent.Z);

            return Vector3.Dot(plane.Normal, centre) + plane.D >= -radius;
        }
    }

    private static Plane Normalize(Plane plane)
    {
        var length = plane.Normal.Length();

        return length > 0f
            ? new Plane(plane.Normal / length, plane.D / length)
            : plane;
    }
}
