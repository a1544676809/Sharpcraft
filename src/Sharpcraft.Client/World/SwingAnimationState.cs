// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Per-entity arm-swing clock, ported from the reference <c>LivingEntity.SwingState</c>: a swing runs for a fixed number
/// of ticks, animating 0..1 across its duration, and restarts are rejected while the previous swing is at most half
/// elapsed.
/// </summary>
public sealed class SwingAnimationState
{
    private int _ticks;
    private float _animation;
    private float _previousAnimation;
    private bool _swinging;
    private int _durationTicks;

    /// <summary>Reference <c>startIfAble</c>: starts a swing unless the current one is still in its first half.</summary>
    public bool StartIfAble(int hand, int animationType, int durationTicks)
    {
        if (_swinging && _ticks <= _durationTicks / 2 && _ticks > 0)
        {
            return false;
        }

        Start(hand, animationType, durationTicks);
        return true;
    }

    private void Start(int hand, int animationType, int durationTicks)
    {
        _durationTicks = durationTicks;
        _ticks = 0;
        _previousAnimation = 0f;
        _animation = 0f;
        _swinging = true;
    }

    /// <summary>Advances one tick.</summary>
    public void Tick()
    {
        _previousAnimation = _animation;
        if (_swinging)
        {
            if (_durationTicks > 0)
            {
                _animation = MathF.Min((float)_ticks / _durationTicks, 1f);
            }

            if (_ticks++ > _durationTicks)
            {
                _swinging = false;
                _animation = 0f;
            }
        }
        else
        {
            _animation = 0f;
        }
    }

    /// <summary>Reference <c>getAnimation</c>: interpolates, wrapping across 1 → 0.</summary>
    public float Animation(float partialTicks) =>
        _animation >= _previousAnimation
            ? _previousAnimation + ((_animation - _previousAnimation) * partialTicks)
            : _previousAnimation + (((_animation + 1f) - _previousAnimation) * partialTicks);

    /// <summary>Whether a swing is currently running.</summary>
    public bool IsSwinging => _swinging;
}
