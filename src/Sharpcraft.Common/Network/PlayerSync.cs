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
public sealed record ServerPositionOutcome(int? TeleportId, bool Snapped, double Distance);

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

    private double _lastX = double.NaN;
    private double _lastY;
    private double _lastZ;
    private float _lastYaw;
    private float _lastPitch;
    private bool _lastOnGround;
    private bool _lastHorizontalCollision;

    /// <summary>Decides which packet must be sent for the current entity state.</summary>
    /// <param name="player">Player entity after its tick.</param>
    /// <param name="yawDegrees">Yaw in degrees.</param>
    /// <param name="pitchDegrees">Pitch in degrees.</param>
    public MovePacketKind Decide(PlayerEntity player, float yawDegrees, float pitchDegrees)
    {
        ArgumentNullException.ThrowIfNull(player);

        var position = player.Position;
        var x = PlayMovementPackets.Quantize(position.X);
        var y = PlayMovementPackets.Quantize(position.Y);
        var z = PlayMovementPackets.Quantize(position.Z);

        var moved = double.IsNaN(_lastX) ||
                    x != _lastX ||
                    y != _lastY ||
                    z != _lastZ;

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
        var x = PlayMovementPackets.Quantize(position.X);
        var y = PlayMovementPackets.Quantize(position.Y);
        var z = PlayMovementPackets.Quantize(position.Z);
        var onGround = player.OnGround;
        var collided = player.HorizontalCollision;

        _lastX = x;
        _lastY = y;
        _lastZ = z;
        _lastYaw = yawDegrees;
        _lastPitch = pitchDegrees;
        _lastOnGround = onGround;
        _lastHorizontalCollision = collided;

        return kind switch
        {
            MovePacketKind.Position => PlayMovementPackets.BuildMovePlayerPos(x, y, z, onGround, collided),
            MovePacketKind.PositionAndRotation =>
                PlayMovementPackets.BuildMovePlayerPosRot(x, y, z, yawDegrees, pitchDegrees, onGround, collided),
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

        // Relative packets carry offsets; treating them as absolute puts the player in the wrong place.
        var target = new Vector3(
            info.IsPositionRelative ? player.Position.X + (float)info.X : (float)info.X,
            info.IsPositionRelative ? player.Position.Y + (float)info.Y : (float)info.Y,
            info.IsPositionRelative ? player.Position.Z + (float)info.Z : (float)info.Z);

        var distance = Vector3.Distance(player.Position, target);

        if (isTeleport || distance > SnapThreshold)
        {
            player.SetPosition(target);
            _lastX = double.NaN;
            return new ServerPositionOutcome(info.TeleportId, Snapped: true, distance);
        }

        if (distance > 0f)
        {
            player.SetPosition(Vector3.Lerp(player.Position, target, BlendFraction));
            _lastX = double.NaN;
        }

        return new ServerPositionOutcome(null, Snapped: false, distance);
    }
}
