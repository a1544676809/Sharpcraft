// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// One finished chunk: the opaque geometry plus the cutout and translucent layers the mesher produced with it.
/// </summary>
/// <param name="Key">Packed chunk key.</param>
/// <param name="Opaque">Geometry drawn by the solid pass.</param>
/// <param name="Cutout">Geometry whose texture has holes, or default when the chunk has none.</param>
/// <param name="Translucent">Geometry drawn with blending, or default when the chunk has none.</param>
/// <remarks>
/// The mesher already builds all three layers per chunk; carrying them together is what lets a streamed chunk render its
/// water and glass instead of only the initially requested batch.
/// </remarks>
public readonly record struct MeshedChunk(long Key, MeshData Opaque, MeshData? Cutout, MeshData? Translucent)
{
    /// <summary>A chunk with only opaque geometry, for callers that have no layers.</summary>
    /// <param name="key">Packed chunk key.</param>
    /// <param name="opaque">Opaque geometry.</param>
    public static MeshedChunk FromOpaque(long key, MeshData opaque) => new(key, opaque, null, null);
}
