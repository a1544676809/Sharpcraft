// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Rabbit jump clock, ported from the reference <c>Rabbit</c> fields: entity event 1 starts a 15-tick jump
/// (<c>jumpDuration=15, jumpTicks=0</c>), and every tick advances <c>jumpTicks</c> until it reaches the duration, then
/// resets both. <see cref="Completion"/> is the reference <c>getJumpCompletion</c> value used by the hop animation.
/// </summary>
public sealed class RabbitHopAnimationState
{
    private const int JumpDurationTicks = 15;

    private int _jumpTicks;
    private int _jumpDuration;

    /// <summary>Reference <c>handleEntityEvent(1)</c>: starts a new hop.</summary>
    public void OnEntityEvent(int eventId)
    {
        if (eventId == 1)
        {
            _jumpDuration = JumpDurationTicks;
            _jumpTicks = 0;
        }
    }

    /// <summary>Reference <c>aiStep</c> jump clock advance.</summary>
    public void Tick()
    {
        if (_jumpTicks != _jumpDuration)
        {
            _jumpTicks++;
        }
        else if (_jumpDuration != 0)
        {
            _jumpTicks = 0;
            _jumpDuration = 0;
        }
    }

    /// <summary>True while a hop is in progress (reference <c>hopAnimationState.startIfStopped</c> gate is jumpTicks &gt; 0).</summary>
    public bool IsJumping => _jumpDuration != 0 && _jumpTicks > 0;

    /// <summary>Reference <c>getJumpCompletion(partial)</c>.</summary>
    public float Completion(float partialTicks) =>
        _jumpDuration == 0 ? 0f : (_jumpTicks + partialTicks) / _jumpDuration;
}
