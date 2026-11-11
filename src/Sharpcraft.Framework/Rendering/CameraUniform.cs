// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Writes the camera uniform block. WGSL expects a column-major matrix, while System.Numerics builds
/// row-major matrices, so the write transposes (docs/modules/render.md 6.1).
/// </summary>
public static class CameraUniform
{
    /// <summary>Fills a 16-float block with the view-projection matrix for a camera.</summary>
    /// <param name="destination">Destination span; must hold at least 16 floats.</param>
    /// <param name="position">Eye position in world space.</param>
    /// <param name="target">Look-at target in world space.</param>
    /// <param name="fieldOfViewDegrees">Vertical field of view.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    public static void Write(
        Span<float> destination,
        Vector3 position,
        Vector3 target,
        float fieldOfViewDegrees,
        int width,
        int height)
    {
        if (destination.Length < 16)
        {
            throw new ArgumentException("camera uniform needs 16 floats", nameof(destination));
        }

        // System.Numerics composes for row vectors (v × M) while WGSL multiplies a column vector (M × v).
        // Writing the row-major matrix straight into the buffer is exactly the transposed, column-major
        // layout WGSL expects — transposing here as well cancels the correction and shrinks the world.
        var matrix = CameraMath.BuildViewProjection(position, target, fieldOfViewDegrees, width, height);

        destination[0] = matrix.M11;
        destination[1] = matrix.M12;
        destination[2] = matrix.M13;
        destination[3] = matrix.M14;
        destination[4] = matrix.M21;
        destination[5] = matrix.M22;
        destination[6] = matrix.M23;
        destination[7] = matrix.M24;
        destination[8] = matrix.M31;
        destination[9] = matrix.M32;
        destination[10] = matrix.M33;
        destination[11] = matrix.M34;
        destination[12] = matrix.M41;
        destination[13] = matrix.M42;
        destination[14] = matrix.M43;
        destination[15] = matrix.M44;
    }
}
