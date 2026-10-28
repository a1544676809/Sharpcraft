// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.World;

namespace MinecraftCSharp.Client.Render;

/// <summary>
/// Turns blocks into triangle geometry. This is the CPU half of rendering: the GPU layer only
/// consumes the float arrays it produces (docs/architecture/boundaries.md).
/// </summary>
/// <remarks>
/// The first version culled a face when the neighbour is not air and coloured each block state
/// deterministically. Greedy meshing and texture atlases come later; the point here is a
/// correct, testable geometry source.
/// <para>
/// Geometry is written into a reusable <see cref="MeshBuffer"/> rather than a <c>List</c>, and the
/// world's loaded chunks are enumerated directly instead of scanning a fixed coordinate window.
/// Both changes are behaviour-preserving: the same triangles come out, in a possibly different
/// order (verified by an order-insensitive fingerprint in <c>ChunkMesherGoldenTests</c>).
/// </para>
/// </remarks>
public static class ChunkMesher
{
    /// <summary>Floats per vertex: position (3) plus colour (3).</summary>
    public const int FloatsPerVertex = 6;

    /// <summary>Vertices per quad.</summary>
    public const int VerticesPerQuad = 4;

    /// <summary>Indices per quad (two triangles).</summary>
    public const int IndicesPerQuad = 6;

    /// <summary>Meshes every loaded chunk of a world into a freshly allocated mesh.</summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="airState">Block state treated as empty.</param>
    /// <param name="maxBlocks">Safety cap so a huge world cannot exhaust memory.</param>
    public static MeshData Mesh(ClientWorld world, int airState = 0, int maxBlocks = 2_000_000)
    {
        ArgumentNullException.ThrowIfNull(world);

        // Pre-size from the loaded chunk count so the first pass rarely grows.
        using var buffer = new MeshBuffer(Math.Min(maxBlocks, world.LoadedChunkCount * 4096));
        var blocks = Mesh(world, buffer, airState, maxBlocks);
        return buffer.ToMeshData(blocks);
    }

    /// <summary>
    /// Meshes every loaded chunk into <paramref name="buffer"/>, which is cleared first. A warm
    /// buffer performs no steady-state allocation.
    /// </summary>
    /// <returns>Number of solid blocks that contributed faces.</returns>
    public static int Mesh(
        ClientWorld world,
        MeshBuffer buffer,
        int airState = 0,
        int maxBlocks = 2_000_000)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(buffer);

        buffer.Clear();
        var blocks = 0;

        foreach (var chunk in world.GetChunks())
        {
            if (blocks >= maxBlocks)
            {
                break;
            }

            blocks += MeshChunk(world, chunk, airState, buffer, maxBlocks - blocks);
        }

        return blocks;
    }

    /// <summary>Meshes one chunk, using the world for neighbour lookups across chunk borders.</summary>
    /// <returns>Number of solid blocks that contributed faces.</returns>
    public static int MeshChunk(
        ClientWorld world,
        LevelChunk chunk,
        int airState,
        MeshBuffer buffer,
        int maxBlocks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(buffer);

        var baseX = chunk.ChunkX * 16;
        var baseZ = chunk.ChunkZ * 16;
        var emitted = 0;

        for (var sectionIndex = 0; sectionIndex < chunk.SectionCount; sectionIndex++)
        {
            var section = chunk.GetSection(chunk.MinSectionY + sectionIndex);
            if (section is null || section.IsEmpty)
            {
                continue;
            }

            var sectionY = (chunk.MinSectionY + sectionIndex) * 16;

            for (var y = 0; y < 16; y++)
            {
                for (var z = 0; z < 16; z++)
                {
                    for (var x = 0; x < 16; x++)
                    {
                        var state = section.Get(x, y, z);
                        if (state == airState)
                        {
                            continue;
                        }

                        if (emitted >= maxBlocks)
                        {
                            return emitted;
                        }

                        var worldX = baseX + x;
                        var worldY = sectionY + y;
                        var worldZ = baseZ + z;

                        EmitFaces(world, state, airState, worldX, worldY, worldZ, buffer);
                        emitted++;
                    }
                }
            }
        }

        return emitted;
    }

    /// <summary>
    /// Writes a chunk's geometry into <paramref name="buffer"/> without touching the world model,
    /// for callers that already know the neighbouring states (section rebuilds).
    /// </summary>
    public static void EmitFaces(
        ClientWorld world,
        int state,
        int airState,
        int x,
        int y,
        int z,
        MeshBuffer buffer)
    {
        var (red, green, blue) = ColourOf(state);

        // -X
        if (world.GetBlockState(x - 1, y, z, airState) == airState)
        {
            buffer.AppendQuad(
                (x, y, z), (x, y, z + 1), (x, y + 1, z + 1), (x, y + 1, z),
                red, green, blue);
        }

        // +X
        if (world.GetBlockState(x + 1, y, z, airState) == airState)
        {
            buffer.AppendQuad(
                (x + 1, y, z), (x + 1, y + 1, z), (x + 1, y + 1, z + 1), (x + 1, y, z + 1),
                red, green, blue);
        }

        // -Y
        if (world.GetBlockState(x, y - 1, z, airState) == airState)
        {
            buffer.AppendQuad(
                (x, y, z), (x, y, z + 1), (x + 1, y, z + 1), (x + 1, y, z),
                red, green, blue);
        }

        // +Y
        if (world.GetBlockState(x, y + 1, z, airState) == airState)
        {
            buffer.AppendQuad(
                (x, y + 1, z), (x + 1, y + 1, z), (x + 1, y + 1, z + 1), (x, y + 1, z + 1),
                red, green, blue);
        }

        // -Z
        if (world.GetBlockState(x, y, z - 1, airState) == airState)
        {
            buffer.AppendQuad(
                (x, y, z), (x + 1, y, z), (x + 1, y + 1, z), (x, y + 1, z),
                red, green, blue);
        }

        // +Z
        if (world.GetBlockState(x, y, z + 1, airState) == airState)
        {
            buffer.AppendQuad(
                (x, y, z + 1), (x, y + 1, z + 1), (x + 1, y + 1, z + 1), (x + 1, y, z + 1),
                red, green, blue);
        }
    }

    /// <summary>Deterministic colour from a block state id, so the same world always looks the same.</summary>
    public static (float Red, float Green, float Blue) ColourOf(int state)
    {
        // Mix the id into a stable hue; the exact palette does not matter yet, stability does.
        var hash = unchecked((uint)(state * 2654435761u));
        var red = 0.35f + (hash & 0xFF) / 255f * 0.6f;
        var green = 0.35f + ((hash >> 8) & 0xFF) / 255f * 0.6f;
        var blue = 0.35f + ((hash >> 16) & 0xFF) / 255f * 0.6f;
        return (red, green, blue);
    }
}

/// <summary>Geometry produced by the mesher.</summary>
/// <param name="Vertices">Interleaved position and colour floats.</param>
/// <param name="Indices">Triangle indices.</param>
/// <param name="BlockCount">Number of solid blocks that contributed faces.</param>
public sealed record MeshData(float[] Vertices, uint[] Indices, int BlockCount)
{
    /// <summary>Vertex count.</summary>
    public int VertexCount => Vertices.Length / ChunkMesher.FloatsPerVertex;

    /// <summary>Triangle count.</summary>
    public int TriangleCount => Indices.Length / 3;

    /// <summary>An empty mesh.</summary>
    public static MeshData Empty { get; } = new([], [], 0);
}
