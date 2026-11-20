// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 255 truth vectors. Every expected value below was printed by a probe run against the real 26.3 client jar
/// (ref://mc-26.3-analysis/vectors/LightBlendVectors.java, run with <c>run-probe.ps1 -Name LightBlendVectors</c>), so
/// this file anchors the blending maths to the reference rather than to our own reading of it.
/// </summary>
/// <remarks>
/// The probe prints four-bit levels (what the reference stores per vertex); our pass works on the reference's 0..240
/// smooth scale, which is that level times sixteen, so the byte-level comparison is <c>value240 / 16</c>.
/// </remarks>
public sealed class Repro_255_LightBlendVectorsTests
{
    [Theory]
    // Corner samples are block/sky nibbles in the order (corner1, corner2, corner3, corner).
    [InlineData(15, 15, 15, 15, 15, 15, 15, 15, 15, 15)]
    [InlineData(0, 15, 0, 15, 0, 15, 0, 15, 0, 15)]
    [InlineData(4, 15, 4, 15, 4, 15, 15, 15, 6, 15)]
    [InlineData(4, 15, 0, 15, 0, 15, 15, 15, 4, 15)]
    [InlineData(1, 0, 0, 0, 0, 0, 15, 0, 11, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 15, 0, 15, 0)]
    [InlineData(7, 0, 0, 15, 3, 0, 9, 15, 4, 15)]
    [InlineData(15, 3, 2, 0, 11, 0, 5, 15, 8, 12)]
    [InlineData(12, 12, 6, 6, 3, 3, 1, 1, 5, 5)]
    [InlineData(2, 1, 1, 2, 0, 0, 2, 2, 1, 1)]
    public void SmoothBlendMatchesTheReferenceJar(
        int block1, int sky1, int block2, int sky2, int block3, int sky3, int centerBlock, int centerSky,
        int expectedBlock, int expectedSky)
    {
        var (block, sky) = ChunkMesher.SmoothBlendForTest(
            block1 * 16, sky1 * 16, block2 * 16, sky2 * 16, block3 * 16, sky3 * 16, centerBlock * 16, centerSky * 16);

        Assert.Equal(expectedBlock, block / 16);
        Assert.Equal(expectedSky, sky / 16);
    }

    [Theory]
    [InlineData(0.5f, 0.5f, 0f, 0f, 144, 240)]
    [InlineData(0.25f, 0.25f, 0.25f, 0.25f, 156, 240)]
    [InlineData(0f, 0f, 1f, 0f, 192, 240)]
    [InlineData(0.125f, 0.375f, 0.25f, 0.25f, 132, 240)]
    public void SmoothWeightedBlendMatchesTheReferenceJar(
        float weight1, float weight2, float weight3, float weight4, int expectedBlock, int expectedSky)
    {
        // The probe's four samples: block light 15, 3, 12 and 9 at full sky, on the 0..240 smooth scale.
        var (block, sky) = ChunkMesher.SmoothWeightedBlendForTest(
            240, 240, 48, 240, 192, 240, 144, 240, weight1, weight2, weight3, weight4);

        Assert.Equal(expectedBlock, block);
        Assert.Equal(expectedSky, sky);
    }

    /// <summary>
    /// A whole-face vertex keeps only the four-bit level, so the sub-level part of a blend is dropped. The probe shows
    /// this as 4/15,4/15,4/15,15/15 blending to level 6 while the exact average is 6.75.
    /// </summary>
    [Fact]
    public void AWholeFaceVertexDropsTheSubLevelPart()
    {
        Assert.Equal(6, ChunkMesher.TruncatedToLevelForTest(108) / 16);
        Assert.Equal(15, ChunkMesher.TruncatedToLevelForTest(255) / 16);
        Assert.Equal(0, ChunkMesher.TruncatedToLevelForTest(15));
    }
}
