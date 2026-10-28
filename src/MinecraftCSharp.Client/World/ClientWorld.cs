// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Coordinates;
using MinecraftCSharp.Common.Network;
using MinecraftCSharp.Common.World;

namespace MinecraftCSharp.Client.World;

/// <summary>
/// Client-side world storage: loaded chunks, the local player's entity id and the block state
/// lookup. No rendering state lives here (docs/architecture/boundaries.md).
/// </summary>
public sealed class ClientWorld
{
    private readonly Dictionary<long, LevelChunk> _chunks = [];
    private readonly int _minSectionY;
    private readonly int _sectionCount;

    /// <summary>Creates a world with a fixed vertical section range.</summary>
    /// <param name="minSectionY">Lowest section index (overworld 1.18+ uses -4).</param>
    /// <param name="sectionCount">Sections per column.</param>
    public ClientWorld(int minSectionY, int sectionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionCount);

        _minSectionY = minSectionY;
        _sectionCount = sectionCount;
    }

    /// <summary>Local player entity id, from JoinGame.</summary>
    public int LocalEntityId { get; private set; }

    /// <summary>Server view distance in chunks.</summary>
    public int ViewDistance { get; private set; }

    /// <summary>Dimensions the server offers, as identifiers.</summary>
    public IReadOnlyList<string> Dimensions { get; private set; } = [];

    /// <summary>Number of loaded chunks.</summary>
    public int LoadedChunkCount => _chunks.Count;

    /// <summary>
    /// The chunks currently loaded. Convenient for callers that accept an allocation (for example
    /// LINQ over the world); hot paths use <see cref="GetChunks"/> instead, whose struct enumerator
    /// does not box.
    /// </summary>
    public IReadOnlyCollection<LevelChunk> LoadedChunks => _chunks.Values;

    /// <summary>
    /// Allocation-free view of the loaded chunks. The returned collection is a struct, so `foreach`
    /// over it never boxes the enumerator, which matters for per-frame meshing.
    /// </summary>
    public Dictionary<long, LevelChunk>.ValueCollection GetChunks() => _chunks.Values;

    /// <summary>Applies the JoinGame header.</summary>
    public void ApplyJoin(JoinGameInfo joinGame)
    {
        ArgumentNullException.ThrowIfNull(joinGame);

        LocalEntityId = joinGame.PlayerId;
        ViewDistance = joinGame.ChunkRadius;
        Dimensions = joinGame.Levels;
    }

    /// <summary>Adds or replaces a chunk.</summary>
    public LevelChunk AddChunk(int chunkX, int chunkZ)
    {
        var chunk = new LevelChunk(chunkX, chunkZ, _minSectionY, _sectionCount);
        _chunks[Pack(chunkX, chunkZ)] = chunk;
        return chunk;
    }

    /// <summary>Installs an externally built chunk.</summary>
    public void AddChunk(LevelChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _chunks[Pack(chunk.ChunkX, chunk.ChunkZ)] = chunk;
    }

    /// <summary>Chunk at chunk coordinates, or null.</summary>
    public LevelChunk? GetChunk(int chunkX, int chunkZ) =>
        _chunks.TryGetValue(Pack(chunkX, chunkZ), out var chunk) ? chunk : null;

    /// <summary>Removes a chunk; returns true when one was present.</summary>
    public bool RemoveChunk(int chunkX, int chunkZ) => _chunks.Remove(Pack(chunkX, chunkZ));

    /// <summary>
    /// Block state at block coordinates. Negative coordinates rely on the arithmetic-shift
    /// conversions, not on naive division (docs/analysis/common/coordinates.md).
    /// </summary>
    public int GetBlockState(int x, int y, int z, int fallbackState = 0)
    {
        var chunk = GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z));
        if (chunk is null)
        {
            return fallbackState;
        }

        return chunk.GetBlockState(BlockCoordinates.SectionRelative(x), y, BlockCoordinates.SectionRelative(z), fallbackState);
    }

    /// <summary>Writes a block state; silently ignored when the chunk is not loaded.</summary>
    public bool SetBlockState(int x, int y, int z, int state, int airState = 0)
    {
        var chunk = GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z));
        if (chunk is null)
        {
            return false;
        }

        chunk.SetBlockState(BlockCoordinates.SectionRelative(x), y, BlockCoordinates.SectionRelative(z), state, airState);
        return true;
    }

    /// <summary>Packs chunk coordinates into a dictionary key.</summary>
    public static long Pack(int chunkX, int chunkZ) => ((long)chunkX << 32) ^ (uint)chunkZ;

    /// <summary>Unpacks a dictionary key.</summary>
    public static (int ChunkX, int ChunkZ) Unpack(long key) => ((int)(key >> 32), (int)(key & 0xFFFF_FFFFL));
}
