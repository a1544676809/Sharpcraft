// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Game;

/// <summary>
/// One-shot handoff between the debug channel and the render loop's screenshot request.
/// </summary>
/// <remarks>
/// The MCP tool cannot reach the frame loop directly, so it drops a path here and the renderer's
/// <c>screenshotRequest</c> callback takes it on the next frame. Harmless when the debug channel is absent: nothing ever
/// writes it, so the callback always returns null (docs/specs/debug-control.md).
/// </remarks>
internal static class ScreenshotBridge
{
    private static string? _pending;

    /// <summary>Queues a screenshot for the next frame; returns false when a request is already queued.</summary>
    /// <param name="path">Absolute path the PNG is written to.</param>
    internal static bool Request(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Interlocked.CompareExchange(ref _pending, path, null) is null;
    }

    /// <summary>Takes the queued path, if any.</summary>
    internal static string? Take() => Interlocked.Exchange(ref _pending, null);
}
