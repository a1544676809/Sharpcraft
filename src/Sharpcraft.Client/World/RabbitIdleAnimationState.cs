// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Random;

namespace Sharpcraft.Client.World;

/// <summary>
/// Rabbit idle-head-tilt clock, ported from the reference <c>Rabbit.idleAnimationTimeout</c> and
/// <c>shouldPlayIdleAnimation</c>. The timeout is seeded once at construction (`nextInt(40)+180`) and re-rolled when it
/// fires; the entity-private RNG algorithm is the same as vanilla (ADR-0025), the seed intentionally differs.
/// </summary>
public sealed class RabbitIdleAnimationState
{
    private readonly LegacyRandomSource _random = new(UniqueSeed());
    private static long _seedUniquifier = 8682522807148012L;
    private int _timeout;
    private bool _started;
    private int _startTick;

    private static long UniqueSeed()
    {
        var uniquifier = Interlocked.Add(ref _seedUniquifier, 1181783497276652981L);
        return (uniquifier * 1181783497276652981L) ^ System.Diagnostics.Stopwatch.GetTimestamp();
    }

    public RabbitIdleAnimationState()
    {
        _timeout = _random.NextInt(40) + 180;
    }

    /// <summary>Advances one client tick. <paramref name="isJumping"/> matches the hop clock gate.</summary>
    public void Tick(int gameTime, bool isNoAi, bool isLeashed, bool isJumping)
    {
        if (_timeout <= 0 && !isNoAi && !isLeashed)
        {
            _timeout = _random.NextInt(40) + 180;
            _started = true;
            _startTick = gameTime;
            return;
        }

        if (isJumping)
        {
            _started = false;
            return;
        }

        _timeout--;
    }

    public bool IsStarted => _started;

    /// <summary>Ticks since the idle head tilt started (the reference samples its keyframes by age).</summary>
    public float AgeTicks(int gameTime) => _started ? gameTime - _startTick : 0f;
}
