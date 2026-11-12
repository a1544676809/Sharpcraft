// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Pins the face table the procedural cloud shader reads (T-827). The shader reconstructs the cell coordinate by
/// shifting the stored word back up and or-ing in a flag bit, so a packing mistake does not fail loudly: it moves
/// clouds one cell sideways. These assertions exist so it fails here instead.
/// </summary>
public sealed class CloudFaceTableTests
{
    private static CloudQuad Quad(int cellX, int cellZ, CloudFaceDirection direction, bool inside = false) =>
        new(cellX, cellZ, direction, inside);

    [Fact]
    public void EachFaceCostsThreeWords()
    {
        var table = CloudFaceTable.Build([Quad(0, 0, CloudFaceDirection.Up), Quad(1, 2, CloudFaceDirection.Up)]);

        Assert.Equal(CloudFaceTable.WordsPerFace, table.Length / 2);
        Assert.Equal(2 * CloudFaceTable.WordsPerFace, table.Length);
    }

    [Fact]
    public void AnEmptyListProducesAnEmptyTable()
    {
        Assert.Empty(CloudFaceTable.Build([]));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(7, 11)]
    [InlineData(-1, -1)]
    [InlineData(-8, 13)]
    public void TheCoordinateSurvivesTheHalvingAndTheFlagBit(int cellX, int cellZ)
    {
        var table = CloudFaceTable.Build([Quad(cellX, cellZ, CloudFaceDirection.Up)]);

        var flags = table[2];
        var rebuiltX = (table[0] << 1) | ((flags & CloudFaceTable.FlagExtraX) >> 7);
        var rebuiltZ = (table[1] << 1) | ((flags & CloudFaceTable.FlagExtraZ) >> 6);

        Assert.Equal(cellX, rebuiltX);
        Assert.Equal(cellZ, rebuiltZ);
    }

    [Theory]
    [InlineData(CloudFaceDirection.Down, 0)]
    [InlineData(CloudFaceDirection.Up, 1)]
    [InlineData(CloudFaceDirection.North, 2)]
    [InlineData(CloudFaceDirection.South, 3)]
    [InlineData(CloudFaceDirection.West, 4)]
    [InlineData(CloudFaceDirection.East, 5)]
    public void TheDirectionOccupiesTheLowBits(CloudFaceDirection direction, int expected)
    {
        var table = CloudFaceTable.Build([Quad(4, 6, direction)]);

        Assert.Equal(expected, table[2] & CloudFaceTable.DirectionMask);
    }

    [Fact]
    public void InsideFacesAreMarkedInTheirOwnBit()
    {
        var outside = CloudFaceTable.Build([Quad(4, 6, CloudFaceDirection.Up)]);
        var inside = CloudFaceTable.Build([Quad(4, 6, CloudFaceDirection.Up, inside: true)]);

        Assert.Equal(0, outside[2] & CloudFaceTable.FlagInsideFace);
        Assert.NotEqual(0, inside[2] & CloudFaceTable.FlagInsideFace);

        // The direction is untouched by the winding flag.
        Assert.Equal(
            outside[2] & CloudFaceTable.DirectionMask,
            inside[2] & CloudFaceTable.DirectionMask);
    }

    [Fact]
    public void TheTopColourFlagStaysClearUntilTheReferenceComparisonTakesItUp()
    {
        var table = CloudFaceTable.Build(
        [
            Quad(0, 0, CloudFaceDirection.Down),
            Quad(0, 0, CloudFaceDirection.Up),
            Quad(0, 0, CloudFaceDirection.North)
        ]);

        for (var face = 0; face < 3; face++)
        {
            Assert.Equal(0, table[(face * CloudFaceTable.WordsPerFace) + 2] & CloudFaceTable.FlagUseTopColour);
        }
    }

    [Fact]
    public void FacesStayInTheOrderTheyWereGiven()
    {
        var table = CloudFaceTable.Build(
        [
            Quad(1, 0, CloudFaceDirection.North),
            Quad(2, 0, CloudFaceDirection.South),
            Quad(3, 0, CloudFaceDirection.East)
        ]);

        int[] expectedX = [1, 2, 3];
        int[] expectedDirection = [2, 3, 5];

        for (var face = 0; face < 3; face++)
        {
            var word = face * CloudFaceTable.WordsPerFace;
            var rebuiltX = (table[word] << 1) | ((table[word + 2] & CloudFaceTable.FlagExtraX) >> 7);
            Assert.Equal(expectedX[face], rebuiltX);
            Assert.Equal(expectedDirection[face], table[word + 2] & CloudFaceTable.DirectionMask);
        }
    }
}
