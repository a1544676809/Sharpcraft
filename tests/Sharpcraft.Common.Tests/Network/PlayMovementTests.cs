// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Movement sync (docs/specs/network/play-movement.md): packet ids and field order, the send decision, the
/// relative-coordinate trap in the teleport packet, and the snap-versus-blend policy.
/// </summary>
public sealed class PlayMovementTests
{
    [Fact]
    public void TheMovePlayerPosPacketCarriesIdThenThreeDoubles()
    {
        var packet = PlayMovementPackets.BuildMovePlayerPos(1.5, 64.0, -3.25, onGround: true, horizontalCollision: false);

        var reader = new PayloadReader(packet);
        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var id));
        Assert.Equal(PacketIds.PlayServerbound.MovePlayerPos, id);
        Assert.Equal(DecodeStatus.Success, reader.TryReadDouble(out var x));
        Assert.Equal(1.5, x, 6);
        Assert.Equal(DecodeStatus.Success, reader.TryReadDouble(out var y));
        Assert.Equal(64.0, y, 6);
        Assert.Equal(DecodeStatus.Success, reader.TryReadDouble(out var z));
        Assert.Equal(-3.25, z, 6);
        // One packed byte, not two booleans: bit 0 is on-ground, bit 1 is horizontal collision.
        Assert.Equal(DecodeStatus.Success, reader.TryReadByte(out var flags));
        Assert.Equal(1, flags);
    }

    [Fact]
    public void ThePosRotPacketPutsFloatsBeforeTheFlags()
    {
        var packet = PlayMovementPackets.BuildMovePlayerPosRot(0, 0, 0, 90f, -30f, onGround: false, horizontalCollision: true);

        var reader = new PayloadReader(packet);
        reader.TryReadVarInt(out var id);
        Assert.Equal(PacketIds.PlayServerbound.MovePlayerPosRot, id);
        reader.TryReadDouble(out _);
        reader.TryReadDouble(out _);
        reader.TryReadDouble(out _);
        Assert.Equal(DecodeStatus.Success, reader.TryReadFloat(out var yaw));
        Assert.Equal(90f, yaw, 4);
        Assert.Equal(DecodeStatus.Success, reader.TryReadFloat(out var pitch));
        Assert.Equal(-30f, pitch, 4);
        Assert.Equal(DecodeStatus.Success, reader.TryReadByte(out var flags));
        Assert.Equal(2, flags);
    }

    [Fact]
    public void TheServerPositionPacketRoundTrips()
    {
        Span<byte> buffer = stackalloc byte[80];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(7);
        writer.WriteDouble(10.5);
        writer.WriteDouble(70.0);
        writer.WriteDouble(-2.5);
        writer.WriteDouble(0.0);
        writer.WriteDouble(-0.08);
        writer.WriteDouble(0.0);
        writer.WriteFloat(45f);
        writer.WriteFloat(12.5f);
        writer.WriteInt(0);

        var status = PlayMovementPackets.TryReadPlayerPosition(buffer[..writer.Position], out var info);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.NotNull(info);
        Assert.Equal(7, info!.TeleportId);
        Assert.Equal(10.5, info.X, 6);
        Assert.Equal(70.0, info.Y, 6);
        Assert.Equal(-2.5, info.Z, 6);
        Assert.Equal(-0.08, info.DeltaY, 6);
        Assert.Equal(45f, info.Yaw, 4);
        Assert.Equal(12.5f, info.Pitch, 4);
        Assert.False(info.IsPositionRelative);
    }

    [Fact]
    public void TheRelativeFlagIsDetected()
    {
        Span<byte> buffer = stackalloc byte[80];
        var writer = new PayloadWriter(buffer);
        writer.WriteVarInt(1);
        for (var i = 0; i < 6; i++)
        {
            writer.WriteDouble(0);
        }

        writer.WriteFloat(0);
        writer.WriteFloat(0);
        writer.WriteInt(1);

        PlayMovementPackets.TryReadPlayerPosition(buffer[..writer.Position], out var info);

        // Bit 0 set means the position components are offsets; a client that ignores this teleports wrongly.
        Assert.True(info!.IsPositionRelative);
    }

    [Fact]
    public void QuantisingSnapsToTheServerGrid()
    {
        // The grid is 1/4096 of a block, not a quarter block: quantising keeps the nearest grid point and
        // therefore moves a coordinate by at most half a step.
        foreach (var value in new[] { 0.2501, 0.00001, -1.5002, 123.456 })
        {
            var quantized = PlayMovementPackets.Quantize(value);

            Assert.True(
                Math.Abs(quantized - value) <= PlayMovementPackets.PositionGranularity / 2 + 1e-12,
                $"quantised {quantized} drifted too far from {value}");

            var steps = quantized / PlayMovementPackets.PositionGranularity;
            Assert.Equal(Math.Round(steps), steps, 6);
        }
    }

    [Fact]
    public void NothingIsSentWhenNothingChanged()
    {
        var sync = new PlayerSync();
        var player = Player();

        // The first decision always sends something, because the server knows nothing about us yet; the
        // rotation is unchanged at 0, so it is the position packet that goes out.
        Assert.Equal(MovePacketKind.Position, sync.Decide(player, 0f, 0f));
        Assert.NotNull(sync.Build(MovePacketKind.Position, player, 0f, 0f));

        // Same state again: a client that keeps sending burns bandwidth for nothing.
        Assert.Equal(MovePacketKind.None, sync.Decide(player, 0f, 0f));
        Assert.Null(sync.Build(MovePacketKind.None, player, 0f, 0f));
    }

    [Fact]
    public void TurningOnlySendsTheRotationPacket()
    {
        var sync = new PlayerSync();
        var player = Player();
        sync.Build(sync.Decide(player, 0f, 0f), player, 0f, 0f);

        Assert.Equal(MovePacketKind.None, sync.Decide(player, 0f, 0f));
        Assert.Equal(MovePacketKind.Rotation, sync.Decide(player, 30f, 0f));
    }

    [Fact]
    public void MovingOnlySendsThePositionPacket()
    {
        var sync = new PlayerSync();
        var player = Player();
        sync.Build(sync.Decide(player, 0f, 0f), player, 0f, 0f);

        player.SetPosition(new Vector3(1f, 2f, 3f));

        Assert.Equal(MovePacketKind.Position, sync.Decide(player, 0f, 0f));
    }

    [Fact]
    public void ATeleportSnapsAndAsksForAcceptance()
    {
        var sync = new PlayerSync();
        var player = Player();
        var info = new PlayerPositionInfo(3, 100, 70, 100, 0, 0, 0, 0, 0, 0);

        var outcome = sync.ApplyServerPosition(player, info, isTeleport: true);

        Assert.True(outcome.Snapped);
        Assert.Equal(3, outcome.TeleportId);
        Assert.Equal(100f, player.Position.X, 3);
    }

    [Fact]
    public void ASmallDeviationIsBlendedNotSnapped()
    {
        var sync = new PlayerSync();
        var player = Player();
        player.SetPosition(Vector3.Zero);
        var info = new PlayerPositionInfo(0, 0.5, 0, 0, 0, 0, 0, 0, 0, 0);

        var outcome = sync.ApplyServerPosition(player, info, isTeleport: false);

        Assert.False(outcome.Snapped);
        Assert.Null(outcome.TeleportId);
        Assert.True(player.Position.X > 0f, "should move towards the server value");
        Assert.True(player.Position.X < 0.5f, "should not jump all the way in one tick");
    }

    [Fact]
    public void ALargeDeviationSnapsInsteadOfBlending()
    {
        var sync = new PlayerSync();
        var player = Player();
        player.SetPosition(Vector3.Zero);
        var info = new PlayerPositionInfo(0, 50, 0, 0, 0, 0, 0, 0, 0, 0);

        var outcome = sync.ApplyServerPosition(player, info, isTeleport: false);

        Assert.True(outcome.Snapped);
        Assert.Equal(50f, player.Position.X, 3);
    }

    [Fact]
    public void RelativeCoordinatesAreAppliedAsOffsets()
    {
        var sync = new PlayerSync();
        var player = Player();
        player.SetPosition(new Vector3(10f, 5f, 10f));
        var info = new PlayerPositionInfo(0, 1, 0, 0, 0, 0, 0, 0, 0, RelativeFlags: 1);

        sync.ApplyServerPosition(player, info, isTeleport: true);

        Assert.Equal(11f, player.Position.X, 3);
        Assert.Equal(5f, player.Position.Y, 3);
    }

    private static PlayerEntity Player() =>
        new(EmptyEntityWorld.Instance, new Vector3(0.5f, 1f, 0.5f));
    [Fact]
    public void TheTickEndPacketIsAnEmptyBodyWithItsId()
    {
        var packet = PlayPackets.BuildClientTickEnd();

        var reader = new PayloadReader(packet);
        Assert.Equal(DecodeStatus.Success, reader.TryReadVarInt(out var id));

        // The id matters: the server resets its per-tick movement guards when this arrives, and a client
        // that never sends it is disconnected on its second position packet.
        Assert.Equal(PacketIds.PlayServerbound.ClientTickEnd, id);
        Assert.Single(packet);
    }
}
