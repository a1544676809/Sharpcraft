// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Geometry that is rebuilt every frame, such as placeholder entity boxes
/// (docs/specs/network/entity-sync.md 6).
/// </summary>
/// <remarks>
/// The buffers are owned by the producer and reused, so a steady frame allocates nothing; the renderer only
/// reads the counts. Framework cannot depend on the client's mesh type, so this carries raw arrays and lengths
/// instead of a mesh record.
/// </remarks>
public sealed class DynamicGeometry
{
    /// <summary>Interleaved vertex floats, <see cref="VertexLayout.Stride"/> bytes per vertex.</summary>
    public float[] Vertices { get; set; } = [];

    /// <summary>How many floats in <see cref="Vertices"/> are in use.</summary>
    public int VertexFloats { get; set; }

    /// <summary>Triangle indices, zero based.</summary>
    public uint[] Indices { get; set; } = [];

    /// <summary>How many indices in <see cref="Indices"/> are in use.</summary>
    public int IndexCount { get; set; }

    /// <summary>Drops the current contents without releasing the buffers.</summary>
    public void Clear()
    {
        VertexFloats = 0;
        IndexCount = 0;
    }

    /// <summary>Makes room for the given number of vertices and indices, growing by doubling.</summary>
    /// <param name="vertexFloats">Floats required.</param>
    /// <param name="indexCount">Indices required.</param>
    public void Reserve(int vertexFloats, int indexCount)
    {
        if (Vertices.Length < vertexFloats)
        {
            var capacity = Math.Max(vertexFloats, Vertices.Length == 0 ? 4096 : Vertices.Length * 2);
            Vertices = new float[capacity];
        }

        if (Indices.Length < indexCount)
        {
            var capacity = Math.Max(indexCount, Indices.Length == 0 ? 1536 : Indices.Length * 2);
            Indices = new uint[capacity];
        }
    }
}
