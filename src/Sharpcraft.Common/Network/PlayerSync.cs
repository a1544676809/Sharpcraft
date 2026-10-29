// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Common.Network;

/// <summary>What the client should send to the server for one tick.</summary>
public enum MovePacketKind
{
    /// <summary>Nothing changed; send nothing.</summary>
    None,

    /// <summary>Only the ground or collision flags changed.</summary>
    StatusOnly,

    /// <summary>Only the rotation changed.</summary>
    Rotation,

    /// <summary>Only the position changed.</summary>
    Position,

    /// <summary>Position and rotation both changed.</summary>
    PositionAndRotation
}

/// <summary>What the client did with an incoming server position.</summary>
/// <param name="TeleportId">Id to echo back, or null when nothing should be sent.</param>
/// <param name="Snapped">True when the local position was replaced outright.</param>
/// <param name="Distance">Distance between the predicted and server position.</param>
/// <param name="YawDegrees">Authoritative yaw in degrees after the correction.</param>
/// <param name="PitchDegrees">Authoritative pitch in degrees after the correction.</param>
public sealed record ServerPositionOutcome(
    int? TeleportId,
    bool Snapped,
    double Distance,
    float YawDegrees = 0f,
    float PitchDegrees = 0f);

/// <summary>
/// Keeps a player entity and the server in step (docs/specs/network/play-movement.md 3-4): decides which
/// movement packet to send, and applies server corrections either by snapping or by blending towards them.
/// </summary>
public sealed class PlayerSync
{
    /// <summary>Deviation above which the local prediction is discarded instead of blended towards.</summary>
    public const double SnapThreshold = 2.0;

    /// <summary>Fraction of the remaining deviation corrected per tick when blending.</summary>
    public const float BlendFraction = 0.2f;

    /// <summary>
    /// Squared movement below which nothing is sent, matching vanilla's <c>Mth.square(2.0E-4)</c> test in
    /// <c>LocalPlayer.sendPosition</c>. Vanilla writes raw doubles, so there is no quantisation step.
    /// </summary>
    public const double MovementEpsilonSquared = 4.0E-8;

    /// <summary>Vanilla resends the position at least this often even when standing still.</summary>
    public const int PositionReminderTicks = 20;

    private double _lastX = double.NaN;
    private double _lastY;
    private double _lastZ;
    private float _lastYaw;
    private float _lastPitch;
    private bool _lastOnGround;
    private bool _lastHorizontalCollision;
    private int _positionReminder;

    /// <summary>Yaw in degrees as last set by the server or by the caller.</summary>
    public float CurrentYawDegrees { get; private set; }

    /// <summary>Pitch in degrees as last set by the server or by the caller.</summary>
    public float CurrentPitchDegrees { get; private set; }

    /// <summary>Decides which packet must be sent for the current entity state.</summary>
    /// <param name="player">Player entity after its tick.</param>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    public MovePacketKind Decide(PlayerEntity player, float yawDegrees, float pitchDegrees)
    {
        ArgumentNullException.ThrowIfNull(player);

        var position = player.Position;

        var moved = double.IsNaN(_lastX);
        if (!moved)
        {
            var dx = position.X - _lastX;
            var dy = position.Y - _lastY;
            var dz = position.Z - _lastZ;
            moved = (dx * dx) + (dy * dy) + (dz * dz) > MovementEpsilonSquared;
        }

        // Standing still still reports once a second, so a dropped packet cannot leave the server guessing.
        _positionReminder++;
        if (_positionReminder >= PositionReminderTicks)
        {
            moved = true;
        }

        var rotated = yawDegrees != _lastYaw || pitchDegrees != _lastPitch;
        var flagsChanged = player.OnGround != _lastOnGround ||
                           player.HorizontalCollision != _lastHorizontalCollision;

        if (moved && rotated)
        {
            return MovePacketKind.PositionAndRotation;
        }

        if (moved)
        {
            return MovePacketKind.Position;
        }

        if (rotated)
        {
            return MovePacketKind.Rotation;
        }

        return flagsChanged ? MovePacketKind.StatusOnly : MovePacketKind.None;
    }

    /// <summary>Builds the packet for a decision and records what was sent.</summary>
    /// <param name="kind">Decision from <see cref="Decide"/>.</param>
    /// <param name="player">Player entity.</param>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    /// <returns>Packet body, or null when nothing needs sending.</returns>
    public byte[]? Build(MovePacketKind kind, PlayerEntity player, float yawDegrees, float pitchDegrees)
    {
        ArgumentNullException.ThrowIfNull(player);

        var position = player.Position;
        var onGround = player.OnGround;
        var collided = player.HorizontalCollision;

        _lastX = position.X;
        _lastY = position.Y;
        _lastZ = position.Z;

        if (kind is MovePacketKind.Position or MovePacketKind.PositionAndRotation)
        {
            _positionReminder = 0;
        }
        _lastYaw = yawDegrees;
        _lastPitch = pitchDegrees;
        _lastOnGround = onGround;
        _lastHorizontalCollision = collided;

        return kind switch
        {
            MovePacketKind.Position => PlayMovementPackets.BuildMovePlayerPos(position.X, position.Y, position.Z, onGround, collided),
            MovePacketKind.PositionAndRotation =>
                PlayMovementPackets.BuildMovePlayerPosRot(position.X, position.Y, position.Z, yawDegrees, pitchDegrees, onGround, collided),
            MovePacketKind.Rotation => PlayMovementPackets.BuildMovePlayerRot(yawDegrees, pitchDegrees, onGround, collided),
            MovePacketKind.StatusOnly => PlayMovementPackets.BuildMovePlayerStatusOnly(onGround, collided),
            _ => null
        };
    }

    /// <summary>
    /// Applies a server position: teleports and large deviations snap, small ones are blended towards so a
    /// lagging connection does not make the camera jump (docs/specs/network/play-movement.md 4).
    /// </summary>
    /// <param name="player">Player to correct.</param>
    /// <param name="info">Parsed server position.</param>
    /// <param name="isTeleport">
    /// True when the server sent an absolute teleport, which always snaps because it also carries an id that
    /// must be echoed back.
    /// </param>
    public ServerPositionOutcome ApplyServerPosition(PlayerEntity player, PlayerPositionInfo info, bool isTeleport)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(info);

        // Vanilla merges the packet against the current state per component, so a packet may be absolute in
        // some axes and relative in others, and the same applies to rotation and velocity.
        var position = player.Position;
        var target = new Vector3(
            (float)Axis(position.X, info.X, info.Has(PlayerPositionInfo.RelativeX)),
            (float)Axis(position.Y, info.Y, info.Has(PlayerPositionInfo.RelativeY)),
            (float)Axis(position.Z, info.Z, info.Has(PlayerPositionInfo.RelativeZ)));

        var delta = player.DeltaMovement;
        var velocity = new Vector3(
            (float)Axis(delta.X, info.DeltaX, info.Has(PlayerPositionInfo.RelativeDeltaX)),
            (float)Axis(delta.Y, info.DeltaY, info.Has(PlayerPositionInfo.RelativeDeltaY)),
            (float)Axis(delta.Z, info.DeltaZ, info.Has(PlayerPositionInfo.RelativeDeltaZ)));

        var currentYaw = CurrentYawDegrees;
        var currentPitch = CurrentPitchDegrees;
        var yaw = Axis(currentYaw, info.Yaw, info.Has(PlayerPositionInfo.RelativeYaw));
        var pitch = Axis(currentPitch, info.Pitch, info.Has(PlayerPositionInfo.RelativePitch));

        CurrentYawDegrees = (float)yaw;
        CurrentPitchDegrees = (float)pitch;

        var distance = Vector3.Distance(player.Position, target);

        if (isTeleport || distance > SnapThreshold)
        {
            player.ApplyServerSnapshot(target, velocity);
            _lastX = double.NaN;
            return new ServerPositionOutcome(info.TeleportId, Snapped: true, distance, (float)yaw, (float)pitch);
        }

        if (distance > 0f)
        {
            // Small deviations are blended so the camera does not jump; the server treats the next packet as
            // authoritative anyway, so this only smooths the visible path.
            player.SetPosition(Vector3.Lerp(player.Position, target, BlendFraction));
            _lastX = double.NaN;
        }

        return new ServerPositionOutcome(null, Snapped: false, distance, (float)yaw, (float)pitch);
    }

    /// <summary>Applies a rotation-only update and returns the resulting facing in degrees.</summary>
    /// <param name="info">Parsed rotation.</param>
    public (float YawDegrees, float PitchDegrees) ApplyServerRotation(PlayerRotationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        CurrentYawDegrees = (float)Axis(CurrentYawDegrees, info.Yaw, info.RelativeYaw);
        CurrentPitchDegrees = (float)Axis(CurrentPitchDegrees, info.Pitch, info.RelativePitch);
        return (CurrentYawDegrees, CurrentPitchDegrees);
    }

    /// <summary>Resolves one component: relative means add to the current value, absolute means replace.</summary>
    private static double Axis(double current, double value, bool relative) => relative ? current + value : value;
}
