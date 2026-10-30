// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;

namespace Sharpcraft.Client.World;

/// <summary>
/// Saves a loaded world to a file and loads it back, so a frame can be rendered from the same scene every time.
/// </summary>
/// <remarks>
/// Two runs against a live server produced different worlds, which made every pixel comparison meaningless. A recorded
/// scene removes the server from the picture: the file holds the chunk keys, their section ranges and the block states,
/// and loading it rebuilds an identical world, so the renderer sees the same input twice.
/// </remarks>
public static class SceneRecorder
{
    private const uint Magic = 0x53434E31; // "SCN1"

    /// <summary>Writes every loaded chunk to a file.</summary>
    /// <param name="world">World to save.</param>
    /// <param name="path">Destination path.</param>
    public static void Save(ClientWorld world, string path)
    {
        ArgumentNullException.ThrowIfNull(world);

        var chunks = world.GetChunks();
        var keys = new long[chunks.Length];

        for (var index = 0; index < chunks.Length; index++)
        {
            keys[index] = ClientWorld.Pack(chunks[index].ChunkX, chunks[index].ChunkZ);
        }

        // Sorted, so the file itself is deterministic rather than depending on arrival order.
        Array.Sort(keys);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(Magic);
        writer.Write(keys.Length);

        foreach (var key in keys)
        {
            var (chunkX, chunkZ) = ClientWorld.Unpack(key);
            var chunk = Find(chunks, chunkX, chunkZ) ?? throw new InvalidOperationException($"chunk {chunkX},{chunkZ} vanished");

            writer.Write(key);
            writer.Write(chunk.SectionCount);
            writer.Write(chunk.MinSectionY);

            // y is section indexed by absolute y (y >> 4), so the sweep must start at the chunk's lowest section, not at zero.
            for (var y = chunk.MinSectionY * 16; y < (chunk.MinSectionY + chunk.SectionCount) * 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        writer.Write(chunk.GetBlockState(x, y, z));
                    }
                }
            }
        }
    }

    /// <summary>Rebuilds the world written by <see cref="Save"/>.</summary>
    /// <param name="path">File to read.</param>
    public static ClientWorld Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (reader.ReadUInt32() != Magic)
        {
            throw new InvalidOperationException("not a recorded scene");
        }

        var count = reader.ReadInt32();
        ClientWorld? world = null;

        for (var index = 0; index < count; index++)
        {
            var key = reader.ReadInt64();
            var sectionCount = reader.ReadInt32();
            var minSectionY = reader.ReadInt32();
            var (chunkX, chunkZ) = ClientWorld.Unpack(key);

            world ??= new ClientWorld(minSectionY, sectionCount);

            var chunk = new LevelChunk(chunkX, chunkZ, minSectionY, sectionCount);

            // A block write only lands in a section that exists, so the empty sections are created up front; without
            // this the whole scene loads as air and every comparison silently passes against nothing.
            for (var section = 0; section < sectionCount; section++)
            {
                chunk.SetSection(new ChunkSection(singleState: 0, sectionY: minSectionY + section));
            }

            for (var y = minSectionY * 16; y < (minSectionY + sectionCount) * 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        var state = reader.ReadInt32();

                        if (state != 0)
                        {
                            chunk.SetBlockState(x, y, z, state, 0);
                        }
                    }
                }
            }

            world.AddChunk(chunk);
        }

        return world ?? new ClientWorld(0, 1);
    }

    private static LevelChunk? Find(LevelChunk[] chunks, int chunkX, int chunkZ)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.ChunkX == chunkX && chunk.ChunkZ == chunkZ)
            {
                return chunk;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first block that differs between two worlds, for diagnosing a lossy round trip.
    /// </summary>
    /// <param name="expected">Original world.</param>
    /// <param name="actual">World rebuilt from the file.</param>
    /// <param name="difference">Readable description, empty when identical.</param>
    public static bool TryFindFirstDifference(ClientWorld expected, ClientWorld actual, out string difference)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        foreach (var chunk in expected.GetChunks())
        {
            var other = Find(actual.GetChunks(), chunk.ChunkX, chunk.ChunkZ);

            if (other is null)
            {
                difference = $"chunk {chunk.ChunkX},{chunk.ChunkZ} missing";
                return true;
            }

            if (other.SectionCount != chunk.SectionCount || other.MinSectionY != chunk.MinSectionY)
            {
                difference = $"chunk {chunk.ChunkX},{chunk.ChunkZ} layout {other.MinSectionY}/{other.SectionCount} vs {chunk.MinSectionY}/{chunk.SectionCount}";
                return true;
            }

            for (var y = chunk.MinSectionY * 16; y < (chunk.MinSectionY + chunk.SectionCount) * 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        var want = chunk.GetBlockState(x, y, z);
                        var got = other.GetBlockState(x, y, z);

                        if (want != got)
                        {
                            difference = $"block ({x},{y},{z}) in chunk {chunk.ChunkX},{chunk.ChunkZ}: {want} became {got}";
                            return true;
                        }
                    }
                }
            }
        }

        difference = string.Empty;
        return false;
    }
}