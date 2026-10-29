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
    public void SubMillimetreJitterDoesNotResend()
    {
        // Vanilla sends only when the squared movement exceeds Mth.square(2.0E-4); there is no quantisation
        // grid on the wire, the packet carries raw doubles.
        var sync = new PlayerSync();
        var player = Player();
        sync.Build(sync.Decide(player, 0f, 0f), player, 0f, 0f);

        player.SetPosition(player.Position + new Vector3(0.00005f, 0f, 0f));
        Assert.Equal(MovePacketKind.None, sync.Decide(player, 0f, 0f));

        player.SetPosition(player.Position + new Vector3(0.01f, 0f, 0f));
        Assert.Equal(MovePacketKind.Position, sync.Decide(player, 0f, 0f));
    }

    [Fact]
    public void StandingStillStillReportsOncePerSecond()
    {
        var sync = new PlayerSync();
        var player = Player();
        sync.Build(sync.Decide(player, 0f, 0f), player, 0f, 0f);

        // Twenty idle ticks force a resend, so a lost packet cannot leave the server guessing.
        for (var tick = 1; tick < PlayerSync.PositionReminderTicks; tick++)
        {
            Assert.Equal(MovePacketKind.None, sync.Decide(player, 0f, 0f));
        }

        Assert.Equal(MovePacketKind.Position, sync.Decide(player, 0f, 0f));
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

        // Bit 0 marks only X: Y and Z are absolute in this packet, so they take the packet values (zero here).
        Assert.Equal(11f, player.Position.X, 3);
        Assert.Equal(0f, player.Position.Y, 3);
    }


    [Fact]
    public void AnAbsoluteTeleportAlsoSetsTheRotation()
    {
        var sync = new PlayerSync();
        var player = Player();
        var info = new PlayerPositionInfo(1, 10, 70, 10, 0, 0, 0, Yaw: 90f, Pitch: -20f, RelativeFlags: 0);

        var outcome = sync.ApplyServerPosition(player, info, isTeleport: true);

        // The join teleport carries the facing; keeping our own heading would leave us looking the wrong way.
        Assert.Equal(90f, outcome.YawDegrees, 3);
        Assert.Equal(-20f, outcome.PitchDegrees, 3);
        Assert.Equal(90f, sync.CurrentYawDegrees, 3);
    }

    [Fact]
    public void RelativeRotationIsAddedToTheCurrentFacing()
    {
        var sync = new PlayerSync();
        var player = Player();

        // Establish a facing first.
        sync.ApplyServerPosition(
            player,
            new PlayerPositionInfo(0, 0, 0, 0, 0, 0, 0, Yaw: 30f, Pitch: 10f, RelativeFlags: 0),
            isTeleport: true);

        // Bits 3 and 4 mean yaw and pitch are offsets; both set here.
        var info = new PlayerPositionInfo(
            0, 0, 0, 0, 0, 0, 0, Yaw: 15f, Pitch: -5f,
            RelativeFlags: PlayerPositionInfo.RelativeYaw | PlayerPositionInfo.RelativePitch);

        var outcome = sync.ApplyServerPosition(player, info, isTeleport: true);

        Assert.Equal(45f, outcome.YawDegrees, 3);
        Assert.Equal(5f, outcome.PitchDegrees, 3);
    }

    [Fact]
    public void OnlyTheAxesMarkedRelativeAreOffsets()
    {
        var sync = new PlayerSync();
        var player = Player();
        player.SetPosition(new Vector3(10f, 5f, 10f));

        // Only X is relative; Y and Z are absolute.
        var info = new PlayerPositionInfo(
            0, 2, 70, 30, 0, 0, 0, 0, 0, RelativeFlags: PlayerPositionInfo.RelativeX);

        sync.ApplyServerPosition(player, info, isTeleport: true);

        Assert.Equal(12f, player.Position.X, 3);
        Assert.Equal(70f, player.Position.Y, 3);
        Assert.Equal(30f, player.Position.Z, 3);
    }

    [Fact]
    public void TheTeleportAlsoCarriesTheVelocity()
    {
        var sync = new PlayerSync();
        var player = Player();

        var info = new PlayerPositionInfo(0, 0, 70, 0, DeltaX: 0.5, DeltaY: -0.08, DeltaZ: 0, 0, 0, RelativeFlags: 0);

        sync.ApplyServerPosition(player, info, isTeleport: true);

        // Knockback and fall velocity arrive the same way; ignoring them would leave stale motion.
        Assert.Equal(0.5f, player.DeltaMovement.X, 3);
        Assert.Equal(-0.08f, player.DeltaMovement.Y, 3);
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