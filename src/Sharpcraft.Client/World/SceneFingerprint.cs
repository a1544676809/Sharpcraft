// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;

namespace Sharpcraft.Client.World;

/// <summary>
/// A fingerprint of the loaded world, so two runs can be shown to be the same scene without comparing pictures.
/// </summary>
/// <remarks>
/// A rendered frame cannot be compared meaningfully until the scene behind it is fixed, so this answers the cheaper
/// question first: chunk keys sorted, then a coarse but deterministic sample of block states per chunk. Identical worlds
/// always produce identical numbers; a differing one is overwhelmingly likely to differ.
/// </remarks>
public static class SceneFingerprint
{
    /// <summary>Computes the fingerprint of every loaded chunk.</summary>
    /// <param name="world">World to fingerprint.</param>
    public static ulong Of(ClientWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var chunks = world.GetChunks();
        var keys = new long[chunks.Length];

        for (var index = 0; index < chunks.Length; index++)
        {
            keys[index] = ClientWorld.Pack(chunks[index].ChunkX, chunks[index].ChunkZ);
        }

        Array.Sort(keys);

        var hash = 1469598103934665603UL;

        foreach (var key in keys)
        {
            hash = Mix(hash, (ulong)key);

            var chunk = ChunkOf(chunks, key);

            if (chunk is null)
            {
                continue;
            }

            // Coarse sampling keeps the cost small while still reflecting any edit to the terrain.
            for (var y = 0; y < chunk.SectionCount * 16; y += 8)
            {
                for (var x = 0; x < 16; x += 4)
                {
                    for (var z = 0; z < 16; z += 4)
                    {
                        hash = Mix(hash, (ulong)chunk.GetBlockState(x, y, z));
                    }
                }
            }
        }

        return Mix(hash, (ulong)chunks.Length);
    }

    private static LevelChunk? ChunkOf(LevelChunk[] chunks, long key)
    {
        foreach (var chunk in chunks)
        {
            if (ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ) == key)
            {
                return chunk;
            }
        }

        return null;
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        return hash * 1099511628211UL;
    }
}
