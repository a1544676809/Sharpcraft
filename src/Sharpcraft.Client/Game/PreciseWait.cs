// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Waits until an absolute timestamp, keeping the last fraction of a millisecond in a spin so the caller
/// lands on its deadline instead of on the scheduler's next slot
/// (docs/specs/network/play-movement.md 7.5).
/// </summary>
public static class PreciseWait
{
    /// <summary>Below this remaining time the wait spins instead of sleeping.</summary>
    public const double SpinThresholdMs = 1.5;

    /// <summary>Blocks until the timestamp is reached.</summary>
    /// <param name="targetTimestamp">Deadline from <see cref="Stopwatch.GetTimestamp"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task UntilAsync(long targetTimestamp, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var remainingMs = RemainingMilliseconds(targetTimestamp);
            if (remainingMs <= 0)
            {
                return;
            }

            if (remainingMs > SpinThresholdMs)
            {
                var sleepMs = remainingMs - SpinThresholdMs;
                await Task.Delay(TimeSpan.FromMilliseconds(sleepMs), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Short enough that yielding to the scheduler would cost more than spinning.
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    /// <summary>Milliseconds left until the timestamp; negative once it has passed.</summary>
    /// <param name="targetTimestamp">Deadline from <see cref="Stopwatch.GetTimestamp"/>.</param>
    public static double RemainingMilliseconds(long targetTimestamp) =>
        (targetTimestamp - Stopwatch.GetTimestamp()) * 1000d / Stopwatch.Frequency;

    /// <summary>Converts a duration to stopwatch ticks for deadline arithmetic.</summary>
    /// <param name="duration">Duration to convert.</param>
    public static long ToTimestampDelta(TimeSpan duration) =>
        (long)(duration.TotalSeconds * Stopwatch.Frequency);
}
