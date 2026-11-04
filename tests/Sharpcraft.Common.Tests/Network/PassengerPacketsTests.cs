// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>The clientbound <c>set_passengers</c> packet (id 109): vehicle id, count, then passenger ids.</summary>
public sealed class PassengerPacketsTests
{
    [Fact]
    public void ReadsTheVehicleAndPassengers()
    {
        // varint(109)=0x6D, vehicle 42=0x2A, count 2=0x02, passengers 7 and 9.
        byte[] body = [0x6D, 0x2A, 0x02, 0x07, 0x09];

        Span<int> passengers = stackalloc int[PassengerPackets.MaxPassengers];
        Assert.Equal(DecodeStatus.Success, PassengerPackets.TryRead(body, out var vehicleId, passengers, out var count));
        Assert.Equal(42, vehicleId);
        Assert.Equal(2, count);
        Assert.Equal(7, passengers[0]);
        Assert.Equal(9, passengers[1]);
    }

    [Fact]
    public void RejectsADifferentPacketId()
    {
        byte[] body = [0x6E, 0x01, 0x00];

        Span<int> passengers = stackalloc int[PassengerPackets.MaxPassengers];
        Assert.Equal(DecodeStatus.Invalid, PassengerPackets.TryRead(body, out _, passengers, out _));
    }

    [Fact]
    public void RejectsACountLargerThanTheDestination()
    {
        byte[] body = [0x6D, 0x2A, 0x7F];

        Span<int> passengers = stackalloc int[2];
        Assert.Equal(DecodeStatus.Invalid, PassengerPackets.TryRead(body, out _, passengers, out _));
    }
}
