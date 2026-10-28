// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Coordinates;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Coordinate rounding tests (T-008), focused on the negative-number behaviour that
/// C#'s truncating division gets wrong (docs/analysis/common/coordinates.md).
/// </summary>
public sealed class BlockCoordinatesTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(15, 0, 15)]
    [InlineData(16, 1, 0)]
    [InlineData(-1, -1, 15)]
    [InlineData(-16, -1, 0)]
    [InlineData(-17, -2, 15)]
    [InlineData(31, 1, 15)]
    [InlineData(-33, -3, 15)]
    public void SectionConversionHandlesNegatives(int block, int expectedSection, int expectedRelative)
    {
        Assert.Equal(expectedSection, BlockCoordinates.ToSectionCoord(block));
        Assert.Equal(expectedRelative, BlockCoordinates.SectionRelative(block));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 0)]
    [InlineData(-0.5, -1)]
    [InlineData(-1.0, -1)]
    [InlineData(15.99, 15)]
    [InlineData(-16.01, -17)]
    public void FloorNeverTruncatesTowardZero(double value, int expected)
    {
        Assert.Equal(expected, BlockCoordinates.Floor(value));
    }

    [Theory]
    [InlineData(-1.0, -1)]
    [InlineData(-0.5, -1)]
    [InlineData(15.9, 0)]
    [InlineData(-16.0, -1)]
    [InlineData(-17.0, -2)]
    public void DoubleToSectionFloorsThenShifts(double value, int expectedSection)
    {
        Assert.Equal(expectedSection, BlockCoordinates.ToSectionCoord(value));
    }

    [Theory]
    [InlineData(7, 16, 0)]
    [InlineData(-7, 16, -1)]
    [InlineData(16, 16, 1)]
    [InlineData(-16, 16, -1)]
    [InlineData(-1, 16, -1)]
    [InlineData(-17, 16, -2)]
    public void FloorDivMatchesJavaForNegatives(int dividend, int divisor, int expected)
    {
        Assert.Equal(expected, BlockCoordinates.FloorDiv(dividend, divisor));
        Assert.Equal(expected, BlockCoordinates.FloorDiv((long)dividend, divisor));
    }

    [Fact]
    public void TruncatingDivisionWouldBeWrongOnNegatives()
    {
        // Guard against someone "simplifying" FloorDiv back to '/'.
        Assert.NotEqual(-7 / 16, BlockCoordinates.FloorDiv(-7, 16));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 16)]
    [InlineData(-1, -16)]
    [InlineData(-2, -32)]
    public void ChunkToMinimumBlockIsShift(int section, int expectedBlock)
    {
        Assert.Equal(expectedBlock, BlockCoordinates.ToMinBlockCoord(section));
    }
}
