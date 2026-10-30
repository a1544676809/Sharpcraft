// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// How much meshing one call may do.
/// </summary>
/// <param name="MaxChunksPerPass">Chunks to build in one pass.</param>
/// <param name="MaxMillisecondsPerPass">Wall-clock budget for one pass.</param>
/// <remarks>
/// One policy type shared by the initial load, streaming and reload, so the numbers are chosen once instead of per caller.
/// A reload is a deliberate act and may spend longer per pass; the interactive path must keep frames short.
/// </remarks>
public readonly record struct MeshBuildPolicy(int MaxChunksPerPass, double MaxMillisecondsPerPass)
{
    /// <summary>Chunks are built a few at a time so the first frames stay short.</summary>
    public static MeshBuildPolicy Interactive { get; } = new(8, 250d);

    /// <summary>Bulk catch-up, for a reload that already paused the frame.</summary>
    public static MeshBuildPolicy CatchUp { get; } = new(int.MaxValue, 5_000d);
}
