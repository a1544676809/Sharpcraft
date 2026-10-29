// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using Sharpcraft.Client.Camera;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Camera;

/// <summary>
/// The pitch written on the wire (docs/specs/network/play-movement.md 7.30). Vanilla's xRot grows downwards, the
/// camera's pitch grows upwards, so the two differ by a sign; getting it wrong is immediately visible to everyone
/// else as a player who looks up when they look down.
/// </summary>
public sealed class LookConventionTests
{
    [Fact]
    public void LookingDownWritesAPositivePitch()
    {
        // Camera pitch is negative when looking down.
        Assert.Equal(90f, LookConvention.ToWirePitchDegrees(-(MathF.PI / 2f)), 3);
    }

    [Fact]
    public void LookingUpWritesANegativePitch()
    {
        Assert.Equal(-90f, LookConvention.ToWirePitchDegrees(MathF.PI / 2f), 3);
    }

    [Fact]
    public void TheConversionRoundTrips()
    {
        foreach (var degrees in new[] { -89f, -45f, 0f, 12.5f, 89f })
        {
            Assert.Equal(degrees, LookConvention.ToWirePitchDegrees(LookConvention.FromWirePitchDegrees(degrees)), 4);
        }
    }

    [Fact]
    public void TheSignReachesThePacket()
    {
        // Layout: header byte, three doubles, yaw float, pitch float, flags byte, so the pitch is the four bytes
        // immediately before the trailing flags byte.

        var packet = PlayMovementPackets.BuildMovePlayerPosRot(
            x: 0,
            y: 0,
            z: 0,
            yawDegrees: 0f,
            pitchDegrees: LookConvention.ToWirePitchDegrees(-(MathF.PI / 2f)),
            onGround: true,
            horizontalCollision: false);

        Assert.Equal(34, packet.Length);

        // Looking down must leave the client as a positive pitch, or observers see it inverted.
        // The protocol is big endian, so the four bytes are read that way rather than with BitConverter.
        var pitch = BinaryPrimitives.ReadSingleBigEndian(packet.AsSpan(packet.Length - 5));
        Assert.True(
            MathF.Abs(pitch - 90f) < 0.001f,
            $"pitch was {pitch}, packet={Convert.ToHexString(packet)}");
    }
}
