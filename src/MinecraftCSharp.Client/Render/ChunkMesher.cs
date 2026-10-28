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
/// The first version culls a face when the neighbour is not air and colours each block state
/// deterministically. Greedy meshing and texture atlases come later; the point here is a
/// correct, testable geometry source.
/// </remarks>
public static class ChunkMesher
{
    /// <summary>Floats per vertex: position (3) plus colour (3).</summary>
    public const int FloatsPerVertex = 6;

    /// <summary>Vertices per quad.</summary>
    public const int VerticesPerQuad = 4;

    /// <summary>Indices per quad (two triangles).</summary>
    public const int IndicesPerQuad = 6;

    /// <summary>Meshes every loaded chunk of a world.</summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="airState">Block state treated as empty.</param>
    /// <param name="maxBlocks">Safety cap so a huge world cannot exhaust memory.</param>
    public static MeshData Mesh(ClientWorld world, int airState = 0, int maxBlocks = 2_000_000)
    {
        ArgumentNullException.ThrowIfNull(world);

        var vertices = new List<float>();
        var indices = new List<uint>();
        var blocks = 0;

        for (var chunkX = -512; chunkX <= 512 && blocks < maxBlocks; chunkX++)
        {
            for (var chunkZ = -512; chunkZ <= 512 && blocks < maxBlocks; chunkZ++)
            {
                var chunk = world.GetChunk(chunkX, chunkZ);
                if (chunk is null)
                {
                    continue;
                }

                blocks += MeshChunk(world, chunk, airState, vertices, indices, maxBlocks - blocks);
            }
        }

        return new MeshData(vertices.ToArray(), indices.ToArray(), blocks);
    }

    /// <summary>Meshes one chunk, using the world for neighbour lookups across chunk borders.</summary>
    public static int MeshChunk(
        ClientWorld world,
        LevelChunk chunk,
        int airState,
        List<float> vertices,
        List<uint> indices,
        int maxBlocks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);

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
                        var worldY = (chunk.MinSectionY + sectionIndex) * 16 + y;
                        var worldZ = baseZ + z;

                        EmitFaces(world, state, airState, worldX, worldY, worldZ, vertices, indices);
                        emitted++;
                    }
                }
            }
        }

        return emitted;
    }

    private static void EmitFaces(
        ClientWorld world,
        int state,
        int airState,
        int x,
        int y,
        int z,
        List<float> vertices,
        List<uint> indices)
    {
        var (red, green, blue) = ColourOf(state);

        // -X
        if (world.GetBlockState(x - 1, y, z) == airState)
        {
            AddQuad(vertices, indices, red, green, blue,
                (x, y, z), (x, y, z + 1), (x, y + 1, z + 1), (x, y + 1, z));
        }

        // +X
        if (world.GetBlockState(x + 1, y, z) == airState)
        {
            AddQuad(vertices, indices, red, green, blue,
                (x + 1, y, z), (x + 1, y + 1, z), (x + 1, y + 1, z + 1), (x + 1, y, z + 1));
        }

        // -Y
        if (world.GetBlockState(x, y - 1, z) == airState)
        {
            AddQuad(vertices, indices, red, green, blue,
                (x, y, z), (x, y, z + 1), (x + 1, y, z + 1), (x + 1, y, z));
        }

        // +Y
        if (world.GetBlockState(x, y + 1, z) == airState)
        {
            AddQuad(vertices, indices, red, green, blue,
                (x, y + 1, z), (x + 1, y + 1, z), (x + 1, y + 1, z + 1), (x, y + 1, z + 1));
        }

        // -Z
        if (world.GetBlockState(x, y, z - 1) == airState)
        {
            AddQuad(vertices, indices, red, green, blue,
                (x, y, z), (x + 1, y, z), (x + 1, y + 1, z), (x, y + 1, z));
        }

        // +Z
        if (world.GetBlockState(x, y, z + 1) == airState)
        {
            AddQuad(vertices, indices, red, green, blue,
                (x, y, z + 1), (x, y + 1, z + 1), (x + 1, y + 1, z + 1), (x + 1, y, z + 1));
        }
    }

    private static void AddQuad(
        List<float> vertices,
        List<uint> indices,
        float red,
        float green,
        float blue,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d)
    {
        var start = (uint)(vertices.Count / FloatsPerVertex);

        AddVertex(vertices, a, red, green, blue);
        AddVertex(vertices, b, red, green, blue);
        AddVertex(vertices, c, red, green, blue);
        AddVertex(vertices, d, red, green, blue);

        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }

    private static void AddVertex(List<float> vertices, (int X, int Y, int Z) position, float red, float green, float blue)
    {
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        vertices.Add(red);
        vertices.Add(green);
        vertices.Add(blue);
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
