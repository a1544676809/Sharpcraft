// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 28: a double chest only pairs with its real other half. Taking the largest opening across
/// every neighbour let an unrelated single chest open the half beside it.
/// </summary>
public sealed class Repro_028_ChestPairingTests
{
    // Facing ids as the block state reader returns them: 0 west, 1 east, 4 north, 5 south.
    [Theory]
    [InlineData(4, 1, 1, 0)]   // left, facing north -> east
    [InlineData(1, 1, 0, 1)]   // left, facing east -> south
    [InlineData(5, 1, -1, 0)]  // left, facing south -> west
    [InlineData(0, 1, 0, -1)]  // left, facing west -> north
    [InlineData(4, 2, -1, 0)]  // right, facing north -> west
    [InlineData(0, 2, 0, 1)]   // right, facing west -> south
    [InlineData(5, 2, 1, 0)]   // right, facing south -> east
    [InlineData(1, 2, 0, -1)]  // right, facing east -> north
    public void ALeftHalfJoinsClockwiseAndARightHalfCounterClockwise(byte facing, byte chestType, int dx, int dz)
    {
        Assert.Equal((dx, dz), ChestPairing.ConnectedOffset(facing, chestType));
    }

    [Fact]
    public void ASingleChestConnectsToNothing()
    {
        foreach (var facing in new byte[] { 0, 1, 4, 5 })
        {
            Assert.Equal((0, 0), ChestPairing.ConnectedOffset(facing, 0));
        }
    }

    [Theory]
    [InlineData(4, 1, 4, 2, true)]   // complementary halves, same facing
    [InlineData(4, 2, 4, 1, true)]
    [InlineData(4, 1, 4, 0, false)]  // a single chest is never a partner
    [InlineData(4, 0, 4, 1, false)]  // and a single chest never seeks one
    [InlineData(4, 1, 4, 1, false)]  // two left halves do not pair
    [InlineData(4, 1, 5, 2, false)]  // different facing does not pair
    public void OnlyTheComplementaryHalfOfTheSameFacingCounts(byte facing, byte chestType, byte otherFacing, byte otherChestType, bool expected)
    {
        Assert.Equal(expected, ChestPairing.IsPartner(facing, chestType, otherFacing, otherChestType));
    }

    [Fact]
    public void ASingleChestBesideADoubleChestIsNotAPartner()
    {
        // The reported defect: opening the single chest must not move the half next to it.
        Assert.False(ChestPairing.IsPartner(facing: 4, chestType: 1, otherFacing: 4, otherChestType: 0));
        Assert.False(ChestPairing.IsPartner(facing: 4, chestType: 2, otherFacing: 4, otherChestType: 0));
    }
}
