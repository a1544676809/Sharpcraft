// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;

namespace Sharpcraft.Framework.Platform;

/// <summary>
/// Raises the operating system's timer resolution while a real-time loop is running.
/// </summary>
/// <remarks>
/// On Windows the default scheduler granularity is about 15.6 ms, so a 50 ms tick wait regularly overshoots
/// and the simulation runs slower than 20 ticks per second. Asking for 1 ms granularity makes short waits
/// accurate. Other platforms already provide fine-grained sleeps, so this is a no-op there
/// (docs/modules/input.md 6).
/// </remarks>
public static partial class TimerResolution
{
    private static readonly object Gate = new();
    private static int _depth;

    /// <summary>Raises the timer resolution until the returned handle is disposed.</summary>
    /// <param name="milliseconds">Requested granularity in milliseconds.</param>
    public static IDisposable Elevate(uint milliseconds = 1)
    {
        if (!OperatingSystem.IsWindows())
        {
            return EmptyHandle.Instance;
        }

        lock (Gate)
        {
            if (_depth++ == 0)
            {
                _ = TimeBeginPeriod(milliseconds);
            }
        }

        return new Handle(milliseconds);
    }

    [LibraryImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static partial uint TimeBeginPeriod(uint period);

    [LibraryImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static partial uint TimeEndPeriod(uint period);

    private sealed class Handle : IDisposable
    {
        private readonly uint _milliseconds;
        private bool _disposed;

        internal Handle(uint milliseconds) => _milliseconds = milliseconds;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (Gate)
            {
                if (--_depth == 0)
                {
                    _ = TimeEndPeriod(_milliseconds);
                }
            }
        }
    }

    private sealed class EmptyHandle : IDisposable
    {
        internal static readonly EmptyHandle Instance = new();

        public void Dispose()
        {
        }
    }
}
