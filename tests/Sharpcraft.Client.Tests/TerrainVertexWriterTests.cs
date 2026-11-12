// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Round-trips every field of the packed terrain vertex (T-832).
/// </summary>
/// <remarks>
/// The slots hold packed integers rather than numbers, and two of them can land on bit patterns that read back as NaN,
/// which is how a comparison against an identical value came out unequal earlier. These tests therefore assert on the
/// decoded fields, never on the raw floats.
/// </remarks>
public sealed class TerrainVertexWriterTests
{
    [Fact]
    public void EveryFieldSurvivesThePacking()
    {
        var sprite = new SpriteRect(0, 0, 16, 16, 1) { RectId = 4242 };
        var vertices = new float[ChunkMesher.FloatsPerVertex];
        var offset = 0;

        TerrainVertexWriter.Write(
            vertices,
            ref offset,
            16f,
            62.8889f,
            -32f,
            0.25f,
            0.5f,
            0.75f,
            12f,
            8f,
            sprite,
            sky: 1f,
            block: 0.2f);

        Assert.Equal(ChunkMesher.FloatsPerVertex, offset);

        var (x, y, z) = VertexFieldReader.Position(vertices, 0);
        Assert.Equal(16f, x);
        Assert.Equal(62.8889f, y, 3);
        Assert.Equal(-32f, z);

        var (red, green, blue) = VertexFieldReader.Colour(vertices, 0);
        Assert.Equal(0.25f, red, 2);
        Assert.Equal(0.5f, green, 2);
        Assert.Equal(0.75f, blue, 2);

        // Halves are exact on integers up to 2048, so a tile coordinate like twelve round-trips without loss.
        var (u, v) = VertexFieldReader.Uv(vertices, 0);
        Assert.Equal(12f, u);
        Assert.Equal(8f, v);

        Assert.Equal(4242, VertexFieldReader.SpriteId(vertices, 0));

        var (sky, block) = VertexFieldReader.Light(vertices, 0);
        Assert.Equal(1f, sky, 2);
        Assert.Equal(0.2f, block, 2);
    }

    [Fact]
    public void AnOpaqueBlueTintPacksToABitPatternThatReadsBackAsNaN()
    {
        // Guards the trap that made a passing comparison fail: a fully blue colour puts 0xFF in the top byte, so the
        // word reads back as NaN, and NaN != NaN even when both sides are the same bits.
        var vertices = new float[ChunkMesher.FloatsPerVertex];
        var offset = 0;

        TerrainVertexWriter.Write(
            vertices,
            ref offset,
            0f,
            0f,
            0f,
            0f,
            0f,
            1f,
            0f,
            0f,
            new SpriteRect(0, 0, 1, 1, 1),
            sky: 1f,
            block: 0f);

        Assert.True(float.IsNaN(vertices[3]));

        var (red, green, blue) = VertexFieldReader.Colour(vertices, 0);
        Assert.Equal(0f, red, 2);
        Assert.Equal(0f, green, 2);
        Assert.Equal(1f, blue, 2);
    }

    [Fact]
    public void TheWrittenVertexIsTwentyFourBytes()
    {
        Assert.Equal(24, ChunkMesher.FloatsPerVertex * sizeof(float));
    }
}
