// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Game;

/// <summary>
/// Fixed-timestep accumulator: the simulation advances in whole 20 Hz ticks regardless of how fast frames
/// arrive, and the leftover time becomes an interpolation factor for rendering
/// (docs/modules/input.md 6).
/// </summary>
public sealed class TickLoop
{
    /// <summary>Length of one simulation tick.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1.0 / 20.0);

    /// <summary>Upper bound on catch-up ticks per frame, so a stall cannot spiral.</summary>
    public const int MaxTicksPerAdvance = 5;

    private double _accumulatorSeconds;

    /// <summary>Number of ticks pending in the accumulator.</summary>
    public double PendingTicks => _accumulatorSeconds / TickInterval.TotalSeconds;

    /// <summary>
    /// Adds elapsed time and runs whole ticks.
    /// </summary>
    /// <param name="elapsed">Time since the previous frame.</param>
    /// <param name="tick">Called once per simulation step.</param>
    /// <returns>Number of ticks executed.</returns>
    public int Advance(TimeSpan elapsed, Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        _accumulatorSeconds += elapsed.TotalSeconds;

        var ran = 0;
        var interval = TickInterval.TotalSeconds;

        while (_accumulatorSeconds >= interval && ran < MaxTicksPerAdvance)
        {
            tick();
            _accumulatorSeconds -= interval;
            ran++;
        }

        // A long stall drops the backlog instead of running dozens of ticks in one frame; the alternative
        // is a frame that takes longer the further behind it gets.
        if (_accumulatorSeconds > interval * MaxTicksPerAdvance)
        {
            _accumulatorSeconds = interval * MaxTicksPerAdvance;
        }

        return ran;
    }

    /// <summary>Fraction into the next tick, for interpolating rendered positions.</summary>
    public float Alpha => (float)(_accumulatorSeconds / TickInterval.TotalSeconds);
}
