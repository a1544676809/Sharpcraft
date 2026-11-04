// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Coordinates;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.World;

/// <summary>
/// Client-side world storage: loaded chunks, the local player's entity id and the block state
/// lookup. No rendering state lives here (docs/architecture/boundaries.md).
/// </summary>
public sealed class ClientWorld
{
    /// <summary>
    /// Kept for callers that still need to serialise a multi-step world update. The chunk table itself is
    /// concurrent, so the background mesher reads it without taking this
    /// (docs/specs/network/play-movement.md 7.20).
    /// </summary>
    public object Sync { get; } = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, LevelChunk> _chunks = new();
    private volatile LevelChunk[] _snapshot = [];
    private readonly HashSet<long> _dirtyChunks = [];
    private readonly int _minSectionY;
    private readonly int _sectionCount;
    private readonly Dictionary<int, ClockNetworkState> _clocks = new();
    private long _lastTickGameTime;

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

    /// <summary>Overworld clock registry id (WorldClocks registers overworld first).</summary>
    public const int OverworldClockId = 0;

    /// <summary>Full day length in ticks.</summary>
    public const int DayLengthTicks = 24000;

    /// <summary>Network game time from the last <c>set_time</c>.</summary>
    public long GameTime { get; private set; }

    /// <summary>Day time in 0..23999 from the overworld clock (0 when the server has not synced one).</summary>
    public int DayTime
    {
        get
        {
            // The reference derives day time only from the overworld clock (nothing reads the raw game time for it); a
            // server that never sends clock updates therefore leaves the clock at its default, so this is 0 afterwards.
            if (!_clocks.TryGetValue(OverworldClockId, out var clock))
            {
                return 0;
            }

            var ticks = clock.TotalTicks % DayLengthTicks;
            return (int)((ticks + DayLengthTicks) % DayLengthTicks);
        }
    }

    /// <summary>
    /// Sub-tick fraction (0..1) of the overworld clock, used to interpolate environment attributes (sun angle, sky light)
    /// between the previous and current tick, exactly like the reference EnvironmentAttributeProbe (docs/modules/time.md 5).
    /// </summary>
    public float DayTimePartial =>
        _clocks.TryGetValue(OverworldClockId, out var clock) ? Math.Clamp(clock.PartialTick, 0f, 1f) : 0f;

    private float _rainLevel;
    private float _thunderLevel;
    private float _previousRainLevel;
    private float _previousThunderLevel;

    /// <summary>Rain level (0..1) at a sub-tick fraction, interpolated between the previous and current tick.</summary>
    public float RainLevel(float partialTick) => LerpBetween(partialTick, _previousRainLevel, _rainLevel);

    /// <summary>
    /// Thunder level (0..1) at a sub-tick fraction, interpolated between ticks and scaled by the rain level, matching
    /// <c>Level.getThunderLevel</c> (docs/modules/weather-and-sky-color.md 6.3).
    /// </summary>
    public float ThunderLevel(float partialTick) => LerpBetween(partialTick, _previousThunderLevel, _thunderLevel) * RainLevel(partialTick);

    /// <summary>Reference <c>Level.isThundering</c>: the raw thunder level exceeds 0.5.</summary>
    public bool IsThundering => _thunderLevel > 0.5f;

    /// <summary>Sets the rain level; the client takes the server's value directly (previous and current match).</summary>
    public void SetRainLevel(float level)
    {
        var clamped = Math.Clamp(level, 0f, 1f);
        _previousRainLevel = clamped;
        _rainLevel = clamped;
    }

    /// <summary>Sets the thunder level; the client takes the server's value directly.</summary>
    public void SetThunderLevel(float level)
    {
        var clamped = Math.Clamp(level, 0f, 1f);
        _previousThunderLevel = clamped;
        _thunderLevel = clamped;
    }

    /// <summary>
    /// Applies a <c>game_event</c> to the weather state, mirroring the reference <c>ClientPacketListener.handleGameEvent</c>.
    /// The START/STOP branch values (0/1) are the reference's quirk; the server always follows them with a
    /// RAIN_LEVEL_CHANGE carrying the real level.
    /// </summary>
    public void ApplyWeather(Sharpcraft.Common.Network.GameEventPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        switch (packet.EventType)
        {
            case Sharpcraft.Common.Network.GameEventPacketCodec.StartRaining:
                SetRainLevel(0f);
                break;
            case Sharpcraft.Common.Network.GameEventPacketCodec.StopRaining:
                SetRainLevel(1f);
                break;
            case Sharpcraft.Common.Network.GameEventPacketCodec.RainLevelChange:
                SetRainLevel(packet.Param);
                break;
            case Sharpcraft.Common.Network.GameEventPacketCodec.ThunderLevelChange:
                SetThunderLevel(packet.Param);
                break;
        }
    }

    private static float LerpBetween(float partialTick, float previous, float current) =>
        previous + (Math.Clamp(partialTick, 0f, 1f) * (current - previous));

    /// <summary>
    /// Applies a <c>set_time</c> packet: advance the existing clocks to the packet's game time, then update only the clocks
    /// the packet actually carries. Routine packets carry no clock updates, so they must not reset the clocks — the reference
    /// ClientClockManager.handleUpdates ticks first and then overwrites only the entries present in the update map.
    /// </summary>
    public void ApplyTime(SetTimePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        AdvanceClocks(packet.GameTime);
        GameTime = packet.GameTime;

        foreach (var (id, state) in packet.Clocks)
        {
            _clocks[id] = state;
        }
    }

    /// <summary>Reads one world clock's state.</summary>
    public bool TryGetClock(int clockId, out ClockNetworkState state) => _clocks.TryGetValue(clockId, out state);

    /// <summary>
    /// Advances every clock to <paramref name="gameTime"/>: <c>partialTick += (gameTime - last) * rate</c>, carrying whole
    /// ticks into <c>totalTicks</c>, exactly like the reference ClientClockManager.tick.
    /// </summary>
    public void AdvanceClocks(long gameTime)
    {
        var delta = gameTime - _lastTickGameTime;
        _lastTickGameTime = gameTime;
        if (delta == 0 || _clocks.Count == 0)
        {
            return;
        }

        foreach (var key in new List<int>(_clocks.Keys))
        {
            var clock = _clocks[key];
            var partial = clock.PartialTick + (delta * (double)clock.Rate);
            var full = (long)Math.Floor(partial);
            _clocks[key] = clock with { TotalTicks = clock.TotalTicks + full, PartialTick = (float)(partial - full) };
        }
    }

    /// <summary>One client tick: advance the game time by one tick and step every clock (docs/modules/time.md).</summary>
    public void AdvanceTick()
    {
        GameTime += 1;
        AdvanceClocks(GameTime);
    }

    /// <summary>
    /// Chunks whose geometry is stale. A block on a chunk border also dirties its neighbour, because face
    /// culling across the seam depends on both sides (docs/specs/network/play-movement.md 7.19).
    /// </summary>
    /// <returns>Dirty chunk keys, cleared afterwards.</returns>
    public IReadOnlyCollection<long> TakeDirtyChunks()
    {
        if (_dirtyChunks.Count == 0)
        {
            return [];
        }

        var keys = _dirtyChunks.ToArray();
        _dirtyChunks.Clear();
        return keys;
    }

    /// <summary>Marks a chunk dirty, plus the neighbours a border block affects.</summary>
    /// <param name="chunkX">Chunk x.</param>
    /// <param name="chunkZ">Chunk z.</param>
    /// <param name="borderX">True when the block sits on the x border.</param>
    /// <param name="borderZ">True when the block sits on the z border.</param>
    public void MarkChunkDirty(int chunkX, int chunkZ, bool borderX = false, bool borderZ = false)
    {
        _dirtyChunks.Add(Pack(chunkX, chunkZ));

        if (borderX)
        {
            _dirtyChunks.Add(Pack(chunkX - 1, chunkZ));
            _dirtyChunks.Add(Pack(chunkX + 1, chunkZ));
        }

        if (borderZ)
        {
            _dirtyChunks.Add(Pack(chunkX, chunkZ - 1));
            _dirtyChunks.Add(Pack(chunkX, chunkZ + 1));
        }
    }

    /// <summary>Lowest section index of every column.</summary>
    public int MinSectionY => _minSectionY;

    /// <summary>Sections per column.</summary>
    public int SectionCount => _sectionCount;

    /// <summary>Number of loaded chunks.</summary>
    public int LoadedChunkCount => _chunks.Count;

    /// <summary>
    /// The chunks currently loaded. Convenient for callers that accept an allocation (for example
    /// LINQ over the world); hot paths use <see cref="GetChunks"/> instead, whose struct enumerator
    /// does not box.
    /// </summary>
    public IReadOnlyCollection<LevelChunk> LoadedChunks => _snapshot;

    /// <summary>
    /// View of the loaded chunks. The table is concurrent, so this is a snapshot that is safe to read while
    /// the game thread inserts a newly streamed chunk.
    /// </summary>
    public LevelChunk[] GetChunks() => _snapshot;

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
        RefreshSnapshot();

        // Same rule as the other overload: a new column changes which border faces its neighbours can hide.
        MarkChunkDirty(chunkX, chunkZ, borderX: true, borderZ: true);
        return chunk;
    }

    /// <summary>Installs an externally built chunk.</summary>
    /// <summary>Adds a chunk parsed from the network together with its light data.</summary>
    public void AddChunk(Common.Network.ParsedChunk parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        parsed.Chunk.ApplyLight(parsed.Light);
        AddChunk(parsed.Chunk);
    }

    public void AddChunk(LevelChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        _chunks[Pack(chunk.ChunkX, chunk.ChunkZ)] = chunk;
        RefreshSnapshot();

        // A new neighbour changes which border faces are hidden, so any column already meshed beside it must be
        // rebuilt. Without this its stale border faces stay coplanar with the new chunk's faces, which shows up
        // as overlapping texture along the seam (docs/specs/network/play-movement.md 7.22).
        MarkChunkDirty(chunk.ChunkX, chunk.ChunkZ, borderX: true, borderZ: true);
    }

    /// <summary>True when the chunk covering a block position has been downloaded.</summary>
    /// <param name="x">Block x.</param>
    /// <param name="z">Block z.</param>
    public bool IsChunkLoaded(int x, int z) =>
        GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z)) is not null;

    /// <summary>Chunk at chunk coordinates, or null.</summary>
    public LevelChunk? GetChunk(int chunkX, int chunkZ) =>
        _chunks.TryGetValue(Pack(chunkX, chunkZ), out var chunk) ? chunk : null;

    /// <summary>Removes a chunk; returns true when one was present.</summary>
    public bool RemoveChunk(int chunkX, int chunkZ)
    {
        var removed = _chunks.TryRemove(Pack(chunkX, chunkZ), out _);
        if (removed)
        {
            RefreshSnapshot();
        }

        return removed;
    }

    /// <summary>
    /// Removes every chunk farther than <paramref name="radiusChunks"/> (Chebyshev) from the centre column and returns
    /// their packed keys, so the renderer and mesh cache can release the matching GPU/CPU resources.
    /// </summary>
    public List<long> RemoveChunksOutside(int centerChunkX, int centerChunkZ, int radiusChunks)
    {
        var removed = new List<long>();
        foreach (var chunk in GetChunks())
        {
            if (Math.Abs(chunk.ChunkX - centerChunkX) > radiusChunks
                || Math.Abs(chunk.ChunkZ - centerChunkZ) > radiusChunks)
            {
                var key = Pack(chunk.ChunkX, chunk.ChunkZ);
                if (_chunks.TryRemove(key, out _))
                {
                    removed.Add(key);
                }
            }
        }

        if (removed.Count > 0)
        {
            RefreshSnapshot();
        }

        return removed;
    }

    /// <summary>
    /// Rebuilds the immutable view the mesher iterates. The array reference is swapped atomically, so a reader
    /// on another thread either sees the whole previous snapshot or the whole new one
    /// (docs/specs/network/play-movement.md 7.20).
    /// </summary>
    private void RefreshSnapshot() => _snapshot = [.. _chunks.Values];

    /// <summary>
    /// Block state at block coordinates. Negative coordinates rely on the arithmetic-shift
    /// conversions, not on naive division (docs/analysis/common/coordinates.md).
    /// </summary>
    /// <summary>
    /// Sky light (0..15) at world coordinates. Cells outside a loaded chunk read as 0.
    /// </summary>
    public byte GetSkyLight(int x, int y, int z)
    {
        var chunk = GetChunk(x >> 4, z >> 4);
        return chunk is null ? (byte)0 : chunk.GetSkyLight(x & 15, y, z & 15);
    }

    /// <summary>
    /// Block light (0..15) at world coordinates. Cells outside a loaded chunk read as 0.
    /// </summary>
    public byte GetBlockLight(int x, int y, int z)
    {
        var chunk = GetChunk(x >> 4, z >> 4);
        return chunk is null ? (byte)0 : chunk.GetBlockLight(x & 15, y, z & 15);
    }

    /// <summary>
    /// Light levels at world coordinates, reporting whether the cell belongs to a loaded chunk.
    /// Callers use this to distinguish "dark" from "not loaded", which matters at the edge of the
    /// loaded region where 0 would render a wall pitch black.
    /// </summary>
    public bool TryGetLight(int x, int y, int z, out byte sky, out byte block)
    {
        var chunk = GetChunk(x >> 4, z >> 4);
        if (chunk is null)
        {
            sky = 0;
            block = 0;
            return false;
        }

        sky = chunk.GetSkyLight(x & 15, y, z & 15);
        block = chunk.GetBlockLight(x & 15, y, z & 15);
        return true;
    }

    public int GetBlockState(int x, int y, int z, int fallbackState = 0)
    {
        var chunk = GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z));
        if (chunk is null)
        {
            return fallbackState;
        }

        return chunk.GetBlockState(BlockCoordinates.SectionRelative(x), y, BlockCoordinates.SectionRelative(z), fallbackState);
    }

    /// <summary>
    /// True when the block at the position is water (reference <c>FluidTags.WATER</c>). Used by the presentation layer to
    /// derive a client-side <c>isInWater</c> flag; unloaded chunks read as not water.
    /// </summary>
    public bool IsWaterAt(int x, int y, int z)
    {
        var state = GetBlockState(x, y, z);
        var block = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(state);
        return block is { } id && string.Equals(id.Path, "water", StringComparison.Ordinal);
    }

    /// <summary>Biome id at a world block position, or -1 when the chunk/section is not loaded or carries no biome data.</summary>
    public int GetBiomeAt(int x, int y, int z)
    {
        var chunk = GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z));
        var section = chunk?.GetSection(y >> ChunkSection.SideBits);
        return section is null ? -1 : section.GetBiome(x & 15, y & 15, z & 15);
    }

    /// <summary>Writes a block state; silently ignored when the chunk is not loaded.</summary>
    public bool SetBlockState(int x, int y, int z, int state, int airState = 0)
    {
        var chunk = GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z));

        if (chunk is null)
        {
            return false;
        }

        var localX = BlockCoordinates.SectionRelative(x);
        var localZ = BlockCoordinates.SectionRelative(z);
        chunk.SetBlockState(localX, y, localZ, state, airState);

        // A change on a seam changes which faces the neighbour draws, so both columns go stale.
        MarkChunkDirty(chunk.ChunkX, chunk.ChunkZ, borderX: localX == 0 || localX == 15, borderZ: localZ == 0 || localZ == 15);
        return true;
    }

    /// <summary>Packs chunk coordinates into a dictionary key.</summary>
    public static long Pack(int chunkX, int chunkZ) => ((long)chunkX << 32) ^ (uint)chunkZ;

    /// <summary>Unpacks a dictionary key.</summary>
    public static (int ChunkX, int ChunkZ) Unpack(long key) => ((int)(key >> 32), (int)(key & 0xFFFF_FFFFL));
}
