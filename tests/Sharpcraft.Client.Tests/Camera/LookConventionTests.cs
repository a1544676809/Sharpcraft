// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Camera;

/// <summary>
/// Yaw and pitch written on the wire (docs/specs/network/play-movement.md 7.30). Vanilla's angles run the
/// opposite way to the camera's on both axes, so a missing conversion makes a player look up when they look down
/// and mirrored when they turn. The geometric test below is the one that matters: it compares the direction the
/// camera actually points with the direction vanilla would reconstruct from the values we send.
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
    public void TurningRightWritesANegativeYaw()
    {
        // A quarter turn to the right sends the camera towards +X; vanilla describes that as minus ninety.
        Assert.Equal(-90f, LookConvention.ToWireYawDegrees(MathF.PI / 2f), 3);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(90f, 0f)]
    [InlineData(-90f, 0f)]
    [InlineData(180f, 0f)]
    [InlineData(37f, 0f)]
    [InlineData(0f, 45f)]
    [InlineData(0f, -45f)]
    [InlineData(-120f, -60f)]
    [InlineData(215f, 80f)]
    public void TheSentAnglesPointWhereTheCameraLooks(float yawDegrees, float pitchDegrees)
    {
        var camera = new CameraController();
        camera.SetLook(yawDegrees * (MathF.PI / 180f), pitchDegrees * (MathF.PI / 180f));

        var wireYaw = LookConvention.ToWireYawDegrees(camera.Yaw);
        var wirePitch = LookConvention.ToWirePitchDegrees(camera.Pitch);

        // Reconstruct the direction vanilla derives from its yaw and pitch.
        var yawRadians = wireYaw * (MathF.PI / 180f);
        var pitchRadians = wirePitch * (MathF.PI / 180f);
        var vanilla = new Vector3(
            -MathF.Sin(yawRadians) * MathF.Cos(pitchRadians),
            -MathF.Sin(pitchRadians),
            MathF.Cos(yawRadians) * MathF.Cos(pitchRadians));

        var actual = camera.Forward;

        Assert.Equal(vanilla.X, actual.X, 3);
        Assert.Equal(vanilla.Y, actual.Y, 3);
        Assert.Equal(vanilla.Z, actual.Z, 3);
    }

    [Fact]
    public void TheConversionsRoundTrip()
    {
        foreach (var degrees in new[] { -179f, -89f, -45f, 0f, 12.5f, 89f, 179f })
        {
            Assert.Equal(degrees, LookConvention.ToWireYawDegrees(LookConvention.FromWireYawDegrees(degrees)), 4);
            Assert.Equal(degrees, LookConvention.ToWirePitchDegrees(LookConvention.FromWirePitchDegrees(degrees)), 4);
        }
    }

    [Fact]
    public void TheSignsReachThePacket()
    {
        // Layout: header byte, three doubles, yaw float, pitch float, flags byte.
        var packet = PlayMovementPackets.BuildMovePlayerPosRot(
            x: 0,
            y: 0,
            z: 0,
            yawDegrees: LookConvention.ToWireYawDegrees(MathF.PI / 2f),
            pitchDegrees: LookConvention.ToWirePitchDegrees(-(MathF.PI / 2f)),
            onGround: true,
            horizontalCollision: false);

        Assert.Equal(34, packet.Length);

        // The protocol is big endian, so the floats are read that way rather than with BitConverter.
        var yaw = BinaryPrimitives.ReadSingleBigEndian(packet.AsSpan(packet.Length - 9));
        var pitch = BinaryPrimitives.ReadSingleBigEndian(packet.AsSpan(packet.Length - 5));

        Assert.Equal(-90f, yaw, 3);
        Assert.Equal(90f, pitch, 3);
    }
}
