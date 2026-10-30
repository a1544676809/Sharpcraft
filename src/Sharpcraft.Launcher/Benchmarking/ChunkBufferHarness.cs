// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Launcher.Benchmarking;

/// <summary>
/// Holds the chunks the server sends so a benchmark can start timing after the transfer is over.
/// </summary>
/// <remarks>
/// Server throughput is otherwise a confounder: a slow send looks like slow meshing. This exists only when
/// <c>MC_DEV_BENCH=1</c> and a buffer window is set, never in a release path, and it releases on whichever comes first -
/// the expected chunk count or the window elapsing - because either one means the initial burst is over.
/// </remarks>
internal sealed class ChunkBufferHarness
{
    /// <summary>Switch that enables any benchmark behaviour at all.</summary>
    internal const string DevSwitch = "MC_DEV_BENCH";

    /// <summary>Window, in seconds, during which chunks are held.</summary>
    internal const string BufferSwitch = "MC_BENCH_BUFFER";

    private readonly int _expectedChunks;
    private readonly TimeSpan _window;
    private readonly List<object> _held = [];
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private bool _released;

    private ChunkBufferHarness(int expectedChunks, TimeSpan window)
    {
        _expectedChunks = expectedChunks;
        _window = window;
    }

    /// <summary>How many chunks are being held.</summary>
    public int HeldCount => _held.Count;

    /// <summary>True while chunks are still being held.</summary>
    public bool Holding => !_released;

    /// <summary>
    /// Builds a harness from the environment, or returns null when the benchmark switches are off.
    /// </summary>
    /// <param name="expectedChunks">Chunks the view distance implies; reaching it ends the hold early.</param>
    public static ChunkBufferHarness? FromEnvironment(int expectedChunks)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(DevSwitch), "1", StringComparison.Ordinal))
        {
            return null;
        }

        if (!double.TryParse(Environment.GetEnvironmentVariable(BufferSwitch), out var seconds) || seconds <= 0)
        {
            return null;
        }

        Console.WriteLine("benchmark mode: not for normal play");
        return new ChunkBufferHarness(Math.Max(1, expectedChunks), TimeSpan.FromSeconds(seconds));
    }

    /// <summary>
    /// Holds a chunk, releasing everything once the count or the window says the burst is over.
    /// </summary>
    /// <param name="chunk">Chunk received from the server.</param>
    /// <returns>True while the chunk should be held rather than installed.</returns>
    public bool Hold(object chunk)
    {
        if (_released)
        {
            return false;
        }

        if (_held.Count == 0)
        {
            _clock.Restart();
        }

        _held.Add(chunk);

        if (_held.Count >= _expectedChunks || _clock.Elapsed >= _window)
        {
            _released = true;
            Console.WriteLine($"bench: released {_held.Count} chunks after {_clock.Elapsed.TotalMilliseconds:F0} ms " +
                $"(expected {_expectedChunks}, window {_window.TotalSeconds:F0} s)");
        }

        return true;
    }

    /// <summary>Takes the held chunks once they have been released.</summary>
    public IReadOnlyList<object> TakeReleased()
    {
        if (!_released)
        {
            return [];
        }

        var copy = _held.ToArray();
        _held.Clear();
        return copy;
    }
}
