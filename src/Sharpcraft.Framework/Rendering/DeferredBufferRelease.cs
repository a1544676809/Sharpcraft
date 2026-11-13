// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Holds buffers that were replaced mid-frame until the frames that could still be reading them are done.
/// </summary>
/// <remarks>
/// When a pool outgrows a buffer it creates a larger one and destroys the old one immediately. That is fine
/// while a frame is finished the moment it is submitted, but once recording runs ahead of submitting, the
/// buffer being destroyed may belong to a command buffer the GPU has not executed yet.
/// <para>
/// Releases are tagged with the frame that asked for them and only happen once
/// <see cref="FramesInFlight"/> later frames have begun, which is the point at which the earlier submission
/// has completed. This is deliberately not reference counting: the count would have to be maintained across
/// two threads, and a frame counter is one number that only the recording side moves (T-837).
/// </para>
/// <para>
/// The destroy is a delegate bound once at construction rather than a backend reference, so the queue can be
/// exercised without a graphics device.
/// </para>
/// </remarks>
/// <param name="destroy">Destroys one buffer. Bound once; not called from the hot path.</param>
internal sealed class DeferredBufferRelease(Action<BufferHandle> destroy)
{
    /// <summary>
    /// How many frames may be in flight at once: one being submitted while the next is recorded.
    /// </summary>
    public const long FramesInFlight = 2;

    private readonly Action<BufferHandle> _destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
    private readonly List<(long Frame, BufferHandle Handle)> _pending = [];

    private long _frame;

    /// <summary>Number of buffers waiting to be released.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>The frame number the next release will be tagged with.</summary>
    public long Frame => _frame;

    /// <summary>Starts a frame. Releases from older frames become eligible as this advances.</summary>
    public void BeginFrame() => _frame++;

    /// <summary>Asks for a buffer to be destroyed once nothing can be reading it.</summary>
    /// <param name="handle">Buffer to destroy.</param>
    public void Release(BufferHandle handle) => _pending.Add((_frame, handle));

    /// <summary>Destroys every buffer whose last user frame has completed.</summary>
    public void Collect()
    {
        var lastFinished = _frame - FramesInFlight;
        var kept = 0;
        for (var index = 0; index < _pending.Count; index++)
        {
            var entry = _pending[index];
            if (entry.Frame <= lastFinished)
            {
                _destroy(entry.Handle);
            }
            else
            {
                _pending[kept++] = entry;
            }
        }

        if (kept < _pending.Count)
        {
            _pending.RemoveRange(kept, _pending.Count - kept);
        }
    }
}
