// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The camera uniform must reproduce exactly what the old CPU-side transform produced. System.Numerics
/// uses row vectors while WGSL multiplies a column vector by the matrix, so the write transposes; getting
/// that wrong silently shrinks the world to a dot instead of failing loudly.
/// </summary>
public sealed class CameraUniformTests
{
    [Fact]
    public void TheUniformMatchesTheRowVectorTransform()
    {
        var eye = new Vector3(10f, 20f, 30f);
        var target = new Vector3(0f, 0f, 0f);

        var cpu = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY)
                  * CameraMath.BuildProjection(60f, 16f / 9f, 0.1f, 2000f);

        var block = new float[16];
        CameraUniform.Write(block, eye, target, 60f, 1600, 900);

        foreach (var point in new[]
                 {
                     new Vector3(0f, 0f, 0f),
                     new Vector3(1f, 1f, 1f),
                     new Vector3(-5f, 3f, 2f),
                     new Vector3(0.5f, 12f, -3f)
                 })
        {
            var expected = Vector4.Transform(new Vector4(point, 1f), cpu);

            // Same operation WGSL performs: matrix built column-major from the 16 floats, times the vertex.
            var actual = Multiply(block, new Vector4(point, 1f));

            Assert.Equal(expected.X, actual.X, 4);
            Assert.Equal(expected.Y, actual.Y, 4);
            Assert.Equal(expected.Z, actual.Z, 4);
            Assert.Equal(expected.W, actual.W, 4);
        }
    }

    /// <summary>Column-major matrix from the 16 floats, multiplied by a column vector.</summary>
    private static Vector4 Multiply(float[] columnMajor, Vector4 v)
    {
        static float At(float[] m, int row, int column) => m[(column * 4) + row];

        return new Vector4(
            (At(columnMajor, 0, 0) * v.X) + (At(columnMajor, 0, 1) * v.Y) + (At(columnMajor, 0, 2) * v.Z) + (At(columnMajor, 0, 3) * v.W),
            (At(columnMajor, 1, 0) * v.X) + (At(columnMajor, 1, 1) * v.Y) + (At(columnMajor, 1, 2) * v.Z) + (At(columnMajor, 1, 3) * v.W),
            (At(columnMajor, 2, 0) * v.X) + (At(columnMajor, 2, 1) * v.Y) + (At(columnMajor, 2, 2) * v.Z) + (At(columnMajor, 2, 3) * v.W),
            (At(columnMajor, 3, 0) * v.X) + (At(columnMajor, 3, 1) * v.Y) + (At(columnMajor, 3, 2) * v.Z) + (At(columnMajor, 3, 3) * v.W));
    }
}
