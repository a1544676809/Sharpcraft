// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

public sealed class PacketBundleAssemblerTests
{
    private static byte[] MakePacket(int id, byte tag) => [(byte)id, tag];

    [Fact]
    public void BundledPacketsAreBufferedUntilClosingDelimiterAndDispatchedAtomicallyInOrder()
    {
        var assembler = new PacketBundleAssembler(PacketIds.PlayClientbound.BundleDelimiter);
        var dispatched = new List<byte[]>();
        var delimiter = new byte[] { (byte)PacketIds.PlayClientbound.BundleDelimiter };

        var p1 = MakePacket(PacketIds.PlayClientbound.AddEntity, 1);
        var p2 = MakePacket(PacketIds.PlayClientbound.SetEntityData, 2);
        var p3 = MakePacket(PacketIds.PlayClientbound.SetEquipment, 3);

        assembler.Process(delimiter, dispatched.Add);
        Assert.True(assembler.InBundle);
        Assert.Empty(dispatched);

        assembler.Process(p1, dispatched.Add);
        assembler.Process(p2, dispatched.Add);
        assembler.Process(p3, dispatched.Add);
        Assert.Empty(dispatched);
        Assert.Equal(3, assembler.BufferedCount);

        assembler.Process(delimiter, dispatched.Add);
        Assert.False(assembler.InBundle);
        Assert.Equal(0, assembler.BufferedCount);
        Assert.Equal([p1, p2, p3], dispatched);
    }

    [Fact]
    public void UnclosedBundleFlushesWhenReachingMaxBundlePacketsWithoutDroppingOrGrowingUnboundedly()
    {
        var assembler = new PacketBundleAssembler(PacketIds.PlayClientbound.BundleDelimiter);
        var dispatched = new List<byte[]>();
        var delimiter = new byte[] { (byte)PacketIds.PlayClientbound.BundleDelimiter };

        assembler.Process(delimiter, dispatched.Add);
        for (var i = 0; i < PacketBundleAssembler.MaxBundlePackets; i++)
        {
            assembler.Process(MakePacket(PacketIds.PlayClientbound.BlockUpdate, (byte)(i & 0xFF)), dispatched.Add);
        }

        Assert.Empty(dispatched);
        Assert.Equal(PacketBundleAssembler.MaxBundlePackets, assembler.BufferedCount);

        var overflowPacket = MakePacket(PacketIds.PlayClientbound.SetTime, 99);
        assembler.Process(overflowPacket, dispatched.Add);

        Assert.False(assembler.InBundle);
        Assert.Equal(0, assembler.BufferedCount);
        Assert.Equal(PacketBundleAssembler.MaxBundlePackets + 1, dispatched.Count);
        Assert.Same(overflowPacket, dispatched[^1]);
    }
}
