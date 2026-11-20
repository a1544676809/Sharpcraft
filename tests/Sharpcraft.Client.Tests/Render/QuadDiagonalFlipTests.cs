// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Pins the quad diagonal choice under asymmetric vertex brightness (AO diagonal anisotropy fix).
/// </summary>
public sealed class QuadDiagonalFlipTests
{
    /// <summary>Uniform corner colours keep the default (0,1,2)-(0,2,3) split.</summary>
    [Fact]
    public void UniformColoursKeepDefaultDiagonal()
    {
        using var buffer = new MeshBuffer(1);
        buffer.AppendQuad(
            (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
            (1f, 1f, 1f), (1f, 1f, 1f), (1f, 1f, 1f), (1f, 1f, 1f),
            (1f, 0f), (1f, 0f), (1f, 0f), (1f, 0f),
            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f));

        uint[] expected = [0, 1, 2, 0, 2, 3];
        Assert.Equal(expected, buffer.Indices.ToArray());
    }

    /// <summary>When diagonal A+C is darker than B+D, the split flips to (1,2,3)-(1,3,0).</summary>
    [Fact]
    public void DarkerAcDiagonalFlipsToBdSplit()
    {
        using var buffer = new MeshBuffer(1);
        buffer.AppendQuad(
            (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
            (0.4f, 0.4f, 0.4f), (1f, 1f, 1f), (0.8f, 0.8f, 0.8f), (1f, 1f, 1f),
            (1f, 0f), (1f, 0f), (1f, 0f), (1f, 0f),
            (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f));

        uint[] expected = [1, 2, 3, 1, 3, 0];
        Assert.Equal(expected, buffer.Indices.ToArray());
    }
}
