// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Client-side squid tentacle clock (reference <c>Squid.aiStep</c>): a locally random <c>tentacleSpeed</c> advances
/// <c>tentacleMovement</c>, which maps to <c>tentacleAngle</c> differently in and out of water. Entity event 19
/// (<c>SQUID_ANIM_SYNCH</c>) resets the movement to zero, matching the reference client.
/// </summary>
/// <remarks>
/// This is a per-creature presentation derivation (P1): the reference keeps it on the client entity, seeded from the
/// local <c>RandomSource</c>, so it is reproduced here with a per-entity <see cref="Random"/>, never read from the
/// server.
/// </remarks>
public sealed class SquidTentacleAnimationState
{
    private const float TwoPi = MathF.PI * 2f;

    private readonly Random _random = new();
    private float _movement;
    private float _previousMovement;
    private float _speed;
    private float _angle;
    private float _previousAngle;

    public SquidTentacleAnimationState()
    {
        // Reference constructor: tentacleSpeed = 1 / (random + 1) * 0.2.
        _speed = (1f / (_random.NextSingle() + 1f)) * 0.2f;
    }

    /// <summary>Reference <c>handleEntityEvent</c>: event 19 resets the tentacle movement.</summary>
    public void OnEntityEvent(int eventId)
    {
        if (eventId == 19)
        {
            _movement = 0f;
        }
    }

    /// <summary>Advances one tick.</summary>
    public void Update(bool isInWater)
    {
        _previousMovement = _movement;
        _previousAngle = _angle;
        _movement += _speed;
        if (_movement > TwoPi)
        {
            // Client branch clamps and does not roll the speed over (that part is server-only).
            _movement = TwoPi;
        }

        if (isInWater)
        {
            if (_movement < MathF.PI)
            {
                var scale = _movement / MathF.PI;
                _angle = MathF.Sin(scale * scale * MathF.PI) * MathF.PI * 0.25f;
            }
            else
            {
                _angle = 0f;
            }
        }
        else
        {
            _angle = MathF.Abs(MathF.Sin(_movement)) * MathF.PI * 0.25f;
        }
    }

    /// <summary>Tentacle angle interpolated by the partial tick.</summary>
    public float Angle(float partialTicks) => _previousAngle + ((_angle - _previousAngle) * partialTicks);
}
