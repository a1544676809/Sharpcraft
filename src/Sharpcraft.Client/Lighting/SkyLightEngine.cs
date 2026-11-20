// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Coordinates;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Lighting;

/// <summary>
/// Client side sky light, kept up to date incrementally whenever a block changes.
/// </summary>
/// <remarks>
/// <para>
/// The server never sends light for a block change: a broken block arrives as one <c>block_update</c> and nothing else,
/// so the reference client owns a light engine and runs it every tick
/// (ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ClientChunkCache.java:38 creates it,
/// LevelChunk.java:292 and 301 feed a block change to it, ClientLevel.java:497 runs the queued updates).
/// </para>
/// <para>
/// Rules, all from the reference sky light engine
/// (ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/SkyLightEngine.java): a cell open to the sky holds
/// 15, light moves to a neighbour with <c>level - opacity</c> where <c>opacity = max(1, lightDampening)</c>
/// (LightEngine.java:77-79), and the downward direction does not decay for a column that is open to the sky because
/// every such cell is itself a source (SkyLightEngine.java:113-135).
/// </para>
/// <para>
/// Two things decide whether the sky reaches a cell at all: how much light a block absorbs, and whether its shape
/// covers the face. We only have the first per state plus the generated "occlusion shape is a full block" flag, so a
/// cell blocks the sky when <c>lightDampening &gt; 0</c> or when the block renders solid. The reference also refuses a
/// step when two partial shapes together cover the face (<c>faceShapeOccludes</c>), which needs per-state shapes we do
/// not have; that residue is SIM-CLIENT-LIGHT-NO-FACE-SHAPE / R-056.
/// </para>
/// <para>
/// Issue 280 made this incremental. A change used to rebuild a 33 cubed box and rescan the column heights of all 1089
/// columns in it, which cost about 10 ms per broken block. Now only the height of the changed column is rescanned
/// (heights depend on the column alone), and the light itself is repaired by the two queues the reference uses: a
/// removal pass that clears everything the old level could reach, followed by an addition pass from the cells that kept
/// their light plus the new sources. <see cref="RecomputeAll"/> keeps the original brute force recompute as the
/// reference the equivalence tests compare against.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.lighting.LevelLightEngine", "ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/LevelLightEngine.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Sky light only; removal and addition queues plus a per column height cache")]
public sealed class SkyLightEngine
{
    /// <summary>Highest light level.</summary>
    public const int MaxLevel = 15;

    /// <summary>
    /// How far a change can reach: one level per block. Only used by the brute force recompute; the incremental path
    /// is bounded by the light levels themselves.
    /// </summary>
    public const int Radius = MaxLevel + 1;

    private readonly ClientWorld _world;
    private readonly Queue<(int X, int Y, int Z)> _increase = new();
    private readonly Queue<(int X, int Y, int Z, int Level)> _decrease = new();
    private readonly Dictionary<long, int> _columnHeights = [];

    /// <summary>Creates an engine over one world.</summary>
    /// <param name="world">World whose chunks hold the light storage.</param>
    public SkyLightEngine(ClientWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    /// <summary>Chunks whose light changed in the last <see cref="CheckBlock"/> call.</summary>
    public HashSet<(int ChunkX, int ChunkZ)> DirtyChunks { get; } = [];

    /// <summary>How many cells the last <see cref="CheckBlock"/> call rewrote, for diagnostics and tests.</summary>
    public int LastChangedCells { get; private set; }

    /// <summary>How many column heights were rescanned by the last <see cref="CheckBlock"/> call.</summary>
    public int LastScannedColumns { get; private set; }

    /// <summary>
    /// Repairs the sky light around one block change.
    /// </summary>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <returns>True when at least one light cell changed.</returns>
    public bool CheckBlock(int x, int y, int z)
    {
        DirtyChunks.Clear();
        LastChangedCells = 0;
        LastScannedColumns = 0;

        // The cell the block changed in may still hold the light it had before, and the source pass below can overwrite
        // it, so its old level is captured first.
        var selfLevel = Read(x, y, z);

        // 1. The changed column is the only column whose sky source can move, because the height of a column depends on
        //    that column's blocks alone.
        // A column the cache has never seen (a chunk that just arrived) is treated as if every cell above the new
        // height had just become a source: the light storage holds whatever the packet delivered, and this engine has
        // not established its own fixpoint there yet.
        var oldHeight = _columnHeights.TryGetValue(ColumnKey(x, z), out var cached) ? cached : (_world.MaxSectionY * 16) + 16;
        var newHeight = ScanHeight(x, z);
        _columnHeights[ColumnKey(x, z)] = newHeight;
        LastScannedColumns++;

        var minY = _world.MinSectionY * 16;
        var maxY = (_world.MaxSectionY * 16) + 15;

        // 2. Cells that were sources and are not any more lose their light, and take everything they lit with them.
        for (var y2 = Math.Max(oldHeight + 1, minY); y2 <= Math.Min(newHeight, maxY); y2++)
        {
            var level = Read(x, y2, z);
            if (level > 0)
            {
                _decrease.Enqueue((x, y2, z, level));
            }
        }

        // 3. Cells that became sources go straight to 15 before anything propagates: a source written through the queue
        //    could be beaten there by a neighbour's lower value and keep it, which is exactly the bug this ordering
        //    removes.
        for (var y2 = Math.Max(newHeight + 1, minY); y2 <= Math.Min(oldHeight, maxY); y2++)
        {
            if (Store(x, y2, z, MaxLevel))
            {
                LastChangedCells++;
            }

            _increase.Enqueue((x, y2, z));
        }

        // The changed cell itself: when it sits under an open column it is a source and is already 15, otherwise the
        // level it held before the block changed has to be taken away.
        if (y > newHeight)
        {
            if (Store(x, y, z, MaxLevel))
            {
                LastChangedCells++;
            }

            _increase.Enqueue((x, y, z));
        }
        else
        {
            if (selfLevel > 0)
            {
                _decrease.Enqueue((x, y, z, selfLevel));
            }

            // The cell is not a source, but the block that used to be here may have blocked light that a neighbour can
            // now shine in, so it is always re-evaluated as an addition seed (level 0 makes the queue pull the level
            // from its neighbours).
            _increase.Enqueue((x, y, z));
        }

        RunQueues();

        return LastChangedCells > 0;
    }

    /// <summary>
    /// Recomputes every loaded chunk from scratch, which is the brute force reference the incremental path is checked
    /// against (and the shape an initial full relight would take).
    /// </summary>
    internal void RecomputeAll()
    {
        DirtyChunks.Clear();
        LastChangedCells = 0;
        LastScannedColumns = 0;

        var minY = _world.MinSectionY * 16;
        var maxY = (_world.MaxSectionY * 16) + 15;

        foreach (var chunk in _world.GetChunks())
        {
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var localX = 0; localX < 16; localX++)
            {
                for (var localZ = 0; localZ < 16; localZ++)
                {
                    var x = baseX + localX;
                    var z = baseZ + localZ;
                    var height = ScanHeight(x, z);
                    _columnHeights[ColumnKey(x, z)] = height;
                    LastScannedColumns++;

                    for (var y = minY; y <= maxY; y++)
                    {
                        var level = y > height ? (byte)MaxLevel : (byte)0;
                        if (Store(x, y, z, level))
                        {
                            LastChangedCells++;
                        }
                    }
                }
            }
        }

        _increase.Clear();
        foreach (var chunk in _world.GetChunks())
        {
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var localX = 0; localX < 16; localX++)
            {
                for (var localZ = 0; localZ < 16; localZ++)
                {
                    for (var y = minY; y <= maxY; y++)
                    {
                        if (Read(baseX + localX, y, baseZ + localZ) > 0)
                        {
                            _increase.Enqueue((baseX + localX, y, baseZ + localZ));
                        }
                    }
                }
            }
        }

        _decrease.Clear();
        RunQueues();
    }

    /// <summary>Drops the height cache of a chunk, which a reloaded chunk has to do.</summary>
    /// <param name="chunkX">Chunk X.</param>
    /// <param name="chunkZ">Chunk Z.</param>
    public void ForgetChunk(int chunkX, int chunkZ)
    {
        var baseX = chunkX * 16;
        var baseZ = chunkZ * 16;
        for (var localX = 0; localX < 16; localX++)
        {
            for (var localZ = 0; localZ < 16; localZ++)
            {
                _columnHeights.Remove(ColumnKey(baseX + localX, baseZ + localZ));
            }
        }
    }

    /// <summary>Best level a cell can take from its neighbours, or zero when it stays dark.</summary>
    private int PullLevel(int x, int y, int z)
    {
        if (BlocksSky(x, y, z))
        {
            return 0;
        }

        var opacity = Opacity(x, y, z);
        var best = Math.Max(
            Math.Max(Read(x - 1, y, z), Read(x + 1, y, z)),
            Math.Max(
                Math.Max(Read(x, y - 1, z), Read(x, y + 1, z)),
                Math.Max(Read(x, y, z - 1), Read(x, y, z + 1))));

        return Math.Max(0, best - opacity);
    }

    /// <summary>Runs the removal queue, then the addition queue, until both are empty.</summary>
    private void RunQueues()
    {
        Span<(int X, int Y, int Z)> neighbours = stackalloc (int, int, int)[6];

        while (_decrease.Count > 0)
        {
            var (fromX, fromY, fromZ, oldLevel) = _decrease.Dequeue();
            if (Read(fromX, fromY, fromZ) != oldLevel)
            {
                // Already cleared or rewritten by another removal, so this entry is stale.
                continue;
            }

            if (Store(fromX, fromY, fromZ, 0))
            {
                LastChangedCells++;
            }

            neighbours[0] = (fromX - 1, fromY, fromZ);
            neighbours[1] = (fromX + 1, fromY, fromZ);
            neighbours[2] = (fromX, fromY - 1, fromZ);
            neighbours[3] = (fromX, fromY + 1, fromZ);
            neighbours[4] = (fromX, fromY, fromZ - 1);
            neighbours[5] = (fromX, fromY, fromZ + 1);

            foreach (var (toX, toY, toZ) in neighbours)
            {
                var level = Read(toX, toY, toZ);
                if (level == 0)
                {
                    continue;
                }

                if (level < oldLevel)
                {
                    _decrease.Enqueue((toX, toY, toZ, level));
                }
                else
                {
                    // This neighbour was not lit by the removed level, so it survives and can light the gap again.
                    _increase.Enqueue((toX, toY, toZ));
                }
            }
        }

        while (_increase.Count > 0)
        {
            var (fromX, fromY, fromZ) = _increase.Dequeue();
            int fromLevel = Read(fromX, fromY, fromZ);
            if (fromLevel == 0)
            {
                // An addition seed with no level of its own pulls the best level its neighbours can give it, which is
                // how a cell that just became transparent gets lit again.
                fromLevel = PullLevel(fromX, fromY, fromZ);
                if (fromLevel == 0)
                {
                    continue;
                }

                if (Store(fromX, fromY, fromZ, (byte)fromLevel))
                {
                    LastChangedCells++;
                }
            }

            if (fromLevel <= 1)
            {
                continue;
            }

            if (fromLevel <= 1)
            {
                continue;
            }

            neighbours[0] = (fromX - 1, fromY, fromZ);
            neighbours[1] = (fromX + 1, fromY, fromZ);
            neighbours[2] = (fromX, fromY - 1, fromZ);
            neighbours[3] = (fromX, fromY + 1, fromZ);
            neighbours[4] = (fromX, fromY, fromZ - 1);
            neighbours[5] = (fromX, fromY, fromZ + 1);

            foreach (var (toX, toY, toZ) in neighbours)
            {
                if (ChunkAt(toX, toZ) is null || BlocksSky(toX, toY, toZ))
                {
                    continue;
                }

                var candidate = fromLevel - Opacity(toX, toY, toZ);
                if (candidate <= Read(toX, toY, toZ))
                {
                    continue;
                }

                if (Store(toX, toY, toZ, (byte)candidate))
                {
                    LastChangedCells++;
                }

                if (candidate > 1)
                {
                    _increase.Enqueue((toX, toY, toZ));
                }
            }
        }
    }

    /// <summary>Highest cell in a column that blocks the sky, from the cache when it is known.</summary>
    private int CachedHeight(int x, int z) =>
        _columnHeights.TryGetValue(ColumnKey(x, z), out var height) ? height : ScanHeight(x, z);

    /// <summary>
    /// Highest cell in a column that blocks the sky; every cell above it is a source of 15.
    /// </summary>
    /// <remarks>
    /// The reference decides this with <c>isEdgeOccluded</c>, which is true when a cell absorbs light (light dampening
    /// not zero) or when its shape fills a face
    /// (ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/ChunkSkyLightSources.java:140-147).
    /// </remarks>
    private int ScanHeight(int x, int z)
    {
        if (ChunkAt(x, z) is null)
        {
            return int.MinValue;
        }

        var top = (_world.MaxSectionY * 16) + 15;
        for (var y = top; y >= _world.MinSectionY * 16; y--)
        {
            if (BlocksSky(x, y, z))
            {
                return y;
            }
        }

        return int.MinValue;
    }

    /// <summary>Whether a cell stops sky light: it absorbs some, or its occlusion shape is a full block.</summary>
    private bool BlocksSky(int x, int y, int z) =>
        LightDampening(x, y, z) > 0 || SolidRender(x, y, z);

    /// <summary>How much light a cell takes away, at least one so light never passes a block for free.</summary>
    /// <remarks>ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/LightEngine.java:77-79.</remarks>
    private int Opacity(int x, int y, int z) => Math.Max(1, LightDampening(x, y, z));

    private int LightDampening(int x, int y, int z) => Facts(x, y, z).LightDampening;

    private bool SolidRender(int x, int y, int z) => Facts(x, y, z).SolidRender;

    /// <summary>Generated predicates of the block in a cell.</summary>
    private BlockOcclusionInfo Facts(int x, int y, int z)
    {
        var state = _world.GetBlockState(x, y, z);
        if (state == 0)
        {
            return new BlockOcclusionInfo(false, false, 0);
        }

        if (VanillaBlockRegistry.Layout.BlockAt(state) is not { } block)
        {
            // An unresolvable state is treated as fully opaque, which is the safe answer: it cannot make the world
            // brighter than the reference would.
            return new BlockOcclusionInfo(true, true, MaxLevel);
        }

        return VanillaBlockOcclusion.TryGet(block, out var info) ? info : new BlockOcclusionInfo(true, true, MaxLevel);
    }

    private static long ColumnKey(int x, int z) => ((long)x << 32) ^ (uint)z;

    private byte Read(int x, int y, int z) =>
        ChunkAt(x, z) is { } chunk ? chunk.Light.GetSkyLight(x & 15, y, z & 15) : (byte)0;

    private bool Store(int x, int y, int z, byte level)
    {
        if (ChunkAt(x, z) is not { } chunk)
        {
            return false;
        }

        if (!chunk.Light.SetSkyLight(x & 15, y, z & 15, level))
        {
            return false;
        }

        MarkDirty(x, z);
        return true;
    }

    /// <summary>
    /// Records the chunk a cell belongs to, plus the neighbour it touches when the cell sits on a chunk edge.
    /// </summary>
    /// <remarks>
    /// A cell on the seam feeds the neighbour's border face, so the neighbour has to rebuild as well; recording only the
    /// chunk that actually changed leaves a dark line along chunk borders.
    /// </remarks>
    private void MarkDirty(int x, int z)
    {
        var chunkX = BlockCoordinates.ToSectionCoord(x);
        var chunkZ = BlockCoordinates.ToSectionCoord(z);
        DirtyChunks.Add((chunkX, chunkZ));

        var localX = BlockCoordinates.SectionRelative(x);
        var localZ = BlockCoordinates.SectionRelative(z);

        // Only chunks that exist are recorded: a neighbour that is not loaded has nothing to rebuild, and marking it
        // would make every edge change look like a cross chunk edit.
        if (localX == 0)
        {
            MarkNeighbour(chunkX - 1, chunkZ);
        }
        else if (localX == 15)
        {
            MarkNeighbour(chunkX + 1, chunkZ);
        }

        if (localZ == 0)
        {
            MarkNeighbour(chunkX, chunkZ - 1);
        }
        else if (localZ == 15)
        {
            MarkNeighbour(chunkX, chunkZ + 1);
        }
    }

    private void MarkNeighbour(int chunkX, int chunkZ)
    {
        if (_world.GetChunk(chunkX, chunkZ) is not null)
        {
            DirtyChunks.Add((chunkX, chunkZ));
        }
    }

    private LevelChunk? ChunkAt(int x, int z) =>
        _world.GetChunk(BlockCoordinates.ToSectionCoord(x), BlockCoordinates.ToSectionCoord(z));
}
