// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The six planes of a view frustum, extracted from a view projection matrix.
/// </summary>
/// <remarks>
/// wgpu clips to zero to one in depth like the Direct3D convention, so the near plane is the third row itself rather
/// than the sum of the third and fourth. Planes point inwards and are normalised so the distance test is in world units.
/// </remarks>
public static class FrustumPlanes
{
    /// <summary>Extracts the six inward facing planes, in the order left, right, bottom, top, near, far.</summary>
    /// <param name="viewProjection">Combined view projection matrix, in the row vector convention.</param>
    /// <param name="destination">Receives six planes; must hold at least six entries.</param>
    public static void Extract(Matrix4x4 viewProjection, Span<Vector4> destination)
    {
        if (destination.Length < 6)
        {
            throw new ArgumentException("six planes are required", nameof(destination));
        }

        // System.Numerics multiplies row vectors on the left (p' = p * M), so the clip coordinates are dot products with
        // the matrix columns: the fourth column carries w and the first three carry x, y and z.
        var row0 = new Vector4(viewProjection.M11, viewProjection.M21, viewProjection.M31, viewProjection.M41);
        var row1 = new Vector4(viewProjection.M12, viewProjection.M22, viewProjection.M32, viewProjection.M42);
        var row2 = new Vector4(viewProjection.M13, viewProjection.M23, viewProjection.M33, viewProjection.M43);
        var row3 = new Vector4(viewProjection.M14, viewProjection.M24, viewProjection.M34, viewProjection.M44);

        destination[0] = Normalise(row3 + row0);
        destination[1] = Normalise(row3 - row0);
        destination[2] = Normalise(row3 + row1);
        destination[3] = Normalise(row3 - row1);
        destination[4] = Normalise(row2);
        destination[5] = Normalise(row3 - row2);
    }

    /// <summary>True when any part of the box lies inside the frustum.</summary>
    /// <param name="planes">Six inward facing planes.</param>
    /// <param name="minimum">Box corner with the smallest coordinates.</param>
    /// <param name="maximum">Box corner with the largest coordinates.</param>
    public static bool IsVisible(ReadOnlySpan<Vector4> planes, Vector3 minimum, Vector3 maximum)
    {
        for (var index = 0; index < 6; index++)
        {
            var plane = planes[index];

            // The corner furthest along the plane normal decides: if even that one is behind the plane, all are.
            var corner = new Vector3(
                plane.X >= 0 ? maximum.X : minimum.X,
                plane.Y >= 0 ? maximum.Y : minimum.Y,
                plane.Z >= 0 ? maximum.Z : minimum.Z);

            if (Vector3.Dot(new Vector3(plane.X, plane.Y, plane.Z), corner) + plane.W < 0f)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector4 Normalise(Vector4 plane)
    {
        var length = new Vector3(plane.X, plane.Y, plane.Z).Length();

        return length > 1e-6f ? plane / length : plane;
    }
}
