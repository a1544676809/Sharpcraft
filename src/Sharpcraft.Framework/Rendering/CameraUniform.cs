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
    /// <summary>Fills a 32-float block with the world and the sky view-projection matrices for a camera.</summary>
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
        if (destination.Length < 32)
        {
            throw new ArgumentException("camera uniform needs 32 floats", nameof(destination));
        }

        // System.Numerics composes for row vectors (v × M) while WGSL multiplies a column vector (M × v).
        // Writing the row-major matrix straight into the buffer is exactly the transposed, column-major
        // layout WGSL expects — transposing here as well cancels the correction and shrinks the world.
        WriteMatrix(destination, CameraMath.BuildViewProjection(position, target, fieldOfViewDegrees, width, height), 0);

        // The sky matrix drops the camera translation: celestial geometry is baked on a dome around the origin, so this is
        // what re-centres it on the camera without any per-frame upload (T-808). The reference sky shaders do the same by
        // using a view matrix that carries rotation only.
        WriteMatrix(destination, CameraMath.BuildViewProjection(Vector3.Zero, target - position, fieldOfViewDegrees, width, height), 16);
    }

    /// <summary>Writes one 4x4 matrix as 16 floats, in the layout WGSL reads a mat4x4.</summary>
    /// <param name="destination">Destination span.</param>
    /// <param name="matrix">Matrix to write.</param>
    /// <param name="offset">First float to write.</param>
    private static void WriteMatrix(Span<float> destination, Matrix4x4 matrix, int offset)
    {
        destination[offset + 0] = matrix.M11;
        destination[offset + 1] = matrix.M12;
        destination[offset + 2] = matrix.M13;
        destination[offset + 3] = matrix.M14;
        destination[offset + 4] = matrix.M21;
        destination[offset + 5] = matrix.M22;
        destination[offset + 6] = matrix.M23;
        destination[offset + 7] = matrix.M24;
        destination[offset + 8] = matrix.M31;
        destination[offset + 9] = matrix.M32;
        destination[offset + 10] = matrix.M33;
        destination[offset + 11] = matrix.M34;
        destination[offset + 12] = matrix.M41;
        destination[offset + 13] = matrix.M42;
        destination[offset + 14] = matrix.M43;
        destination[offset + 15] = matrix.M44;
    }
}
