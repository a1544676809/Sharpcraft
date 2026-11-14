// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Stands in for the chunk arenas so the upload path can be exercised without a device. It records every call
/// so a test can see how many times a key was applied or dropped (T-838).
/// </summary>
internal sealed class FakeChunkGeometrySink : IChunkGeometrySink
{
    private readonly List<long> _applied = [];
    private readonly List<long> _removed = [];

    /// <summary>Keys installed, in order, including repeats.</summary>
    public IReadOnlyList<long> Applied => _applied;

    /// <summary>Keys dropped, in order, including repeats.</summary>
    public IReadOnlyList<long> Removed => _removed;

    /// <inheritdoc />
    public void Apply(long chunkKey, GeometryData mesh) => _applied.Add(chunkKey);

    /// <inheritdoc />
    public void Remove(long chunkKey) => _removed.Add(chunkKey);
}
