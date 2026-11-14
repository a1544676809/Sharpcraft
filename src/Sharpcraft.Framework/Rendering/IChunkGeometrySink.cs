// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Where a frame's chunk geometry goes: replaced or dropped by chunk key.
/// </summary>
/// <remarks>
/// <see cref="ChunkGeometryStore"/> is the only real implementation and it needs a device and a backend to
/// exist at all. The uploader only ever applies and drops keys, so it depends on this instead, and the
/// removals path — the one that used to be invoked twice — can be tested against a recorder (T-838).
/// </remarks>
public interface IChunkGeometrySink
{
    /// <summary>Installs or replaces the geometry of one chunk.</summary>
    /// <param name="chunkKey">Chunk the geometry belongs to.</param>
    /// <param name="mesh">Geometry to install.</param>
    void Apply(long chunkKey, GeometryData mesh);

    /// <summary>Drops one chunk's geometry.</summary>
    /// <param name="chunkKey">Chunk to drop.</param>
    void Remove(long chunkKey);
}
