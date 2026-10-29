// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Renders between simulation ticks: the entity advances at a fixed 20 Hz, but frames arrive faster, so the
/// eye is interpolated from the previous tick's position to the current one
/// (docs/src/sharpcraft-client/game.md 5).
/// </summary>
public sealed class PlayerInterpolator
{
    private Vector3 _previousEye;
    private Vector3 _currentEye;
    private float _previousYaw;
    private float _currentYaw;

    /// <summary>Creates an interpolator anchored at a player.</summary>
    /// <param name="player">Player whose eye is interpolated.</param>
    /// <param name="yaw">Initial facing in radians.</param>
    public PlayerInterpolator(PlayerEntity player, float yaw)
    {
        ArgumentNullException.ThrowIfNull(player);
        _previousEye = player.EyePosition;
        _currentEye = player.EyePosition;
        _previousYaw = yaw;
        _currentYaw = yaw;
    }

    /// <summary>
    /// Records a finished tick: the previous sample becomes the new start point.
    /// </summary>
    /// <param name="player">Player after the tick.</param>
    /// <param name="yaw">Facing after the tick.</param>
    public void Push(PlayerEntity player, float yaw)
    {
        ArgumentNullException.ThrowIfNull(player);
        _previousEye = _currentEye;
        _previousYaw = _currentYaw;
        _currentEye = player.EyePosition;
        _currentYaw = yaw;
    }

    /// <summary>Snaps both samples to the current state; used after a teleport.</summary>
    /// <param name="player">Player whose eye should be adopted immediately.</param>
    /// <param name="yaw">Facing to adopt.</param>
    public void Snap(PlayerEntity player, float yaw)
    {
        ArgumentNullException.ThrowIfNull(player);
        _previousEye = player.EyePosition;
        _currentEye = player.EyePosition;
        _previousYaw = yaw;
        _currentYaw = yaw;
    }

    /// <summary>Eye position for a frame.</summary>
    /// <param name="alpha">Fraction into the next tick, 0..1.</param>
    public Vector3 Eye(float alpha)
    {
        var t = Math.Clamp(alpha, 0f, 1f);
        return Vector3.Lerp(_previousEye, _currentEye, t);
    }

    /// <summary>Facing for a frame, interpolated along the shortest arc.</summary>
    /// <param name="alpha">Fraction into the next tick, 0..1.</param>
    public float Yaw(float alpha)
    {
        var t = Math.Clamp(alpha, 0f, 1f);
        var delta = _currentYaw - _previousYaw;

        // Keep the interpolation on the short way round, otherwise a wrap at ±π spins the view a full turn.
        while (delta > MathF.PI)
        {
            delta -= MathF.PI * 2f;
        }

        while (delta < -MathF.PI)
        {
            delta += MathF.PI * 2f;
        }

        return _previousYaw + (delta * t);
    }

    /// <summary>Biggest step between samples, for diagnostics of a stalled loop.</summary>
    public float SampleDistance => Vector3.Distance(_previousEye, _currentEye);
}
