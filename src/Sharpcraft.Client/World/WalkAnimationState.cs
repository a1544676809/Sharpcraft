// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Reference <c>WalkAnimationState</c>: a smoothed walk speed and an accumulated walk position. The tracker advances it
/// once per tick from the entity's actual horizontal movement, and the render state samples it with the frame's partial
/// tick, so walking animation now works in the live game instead of only in the sandbox.
/// </summary>
/// <remarks>
/// Formulas mirror the reference exactly: <c>speed = speed + (target - speed) * factor</c>,
/// <c>position += speed</c>, <c>speed(partial) = min(lerp(partial, speedOld, speed), 1)</c> and
/// <c>position(partial) = (position - speed * (1 - partial)) * positionScale</c>
/// (docs/analysis/render/entity-animation.md).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.WalkAnimationState", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/WalkAnimationState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public sealed class WalkAnimationState
{
    private float _speedOld;
    private float _speed;
    private float _position;
    private float _positionScale = 1f;

    /// <summary>Advances one tick: smooth toward <paramref name="targetSpeed"/> and accumulate the position.</summary>
    public void Update(float targetSpeed, float factor, float positionScale)
    {
        _speedOld = _speed;
        _speed += (targetSpeed - _speed) * factor;
        _position += _speed;
        _positionScale = positionScale;
    }

    /// <summary>Resets to rest, as when the reference stops the animation (passenger or dead).</summary>
    public void Stop()
    {
        _speedOld = 0f;
        _speed = 0f;
        _position = 0f;
    }

    /// <summary>Current speed without interpolation.</summary>
    public float Speed() => _speed;

    /// <summary>Speed interpolated with the partial tick and clamped to one.</summary>
    public float Speed(float partialTicks) => MathF.Min(_speedOld + ((_speed - _speedOld) * partialTicks), 1f);

    /// <summary>Current accumulated position scaled by the configured position scale.</summary>
    public float Position() => _position * _positionScale;

    /// <summary>Position interpolated with the partial tick.</summary>
    public float Position(float partialTicks) => (_position - (_speed * (1f - partialTicks))) * _positionScale;

    /// <summary>Whether the entity is still considered moving.</summary>
    public bool IsMoving => _speed > 1.0E-5f;
}
