// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Coordinates;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Lighting;

/// <summary>
/// Client side sky light, recomputed locally whenever a block changes.
/// </summary>
/// <remarks>
/// <para>
/// The server never sends light for a block change: a broken block arrives as one <c>block_update</c> and nothing else,
/// so the reference client owns a light engine and runs it every tick
/// (ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ClientChunkCache.java:38 creates it,
/// LevelChunk.java:292 and 301 feed a block change to it, ClientLevel.java:497 runs the queued updates).
/// Without that layer a cell that becomes visible after a break keeps the zero it was loaded with and draws black.
/// </para>
/// <para>
/// This is the naive first version: one block change recomputes a cube of radius <see cref="Radius"/> around the cell
/// by reloading the sky sources of every affected column and then flooding light outwards to a fixed point. It allocates
/// and touches far more cells than the reference's incremental queue; that is deliberate (principle 14), and the
/// optimisation is a separate task (R-055).
/// </para>
/// <para>
/// Rules, all from the reference sky light engine
/// (ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/SkyLightEngine.java):
/// a cell open to the sky holds 15, light moves to a neighbour with <c>level - opacity</c> where
/// <c>opacity = max(1, lightDampening)</c> (LightEngine.java:77-79), and the downward direction does not decay for a
/// column that is open to the sky because every such cell is itself a source (SkyLightEngine.java:113-135).
/// </para>
/// <para>
/// Two things decide whether the sky reaches a cell at all: how much light a block absorbs, and whether its collision
/// shape covers the face. We only have the first per state plus the generated "occlusion shape is a full block" flag, so
/// a cell blocks the sky when <c>lightDampening &gt; 0</c> or when the block renders solid (glass and leaves are
/// neither). The reference also refuses a step when two partial shapes together cover the face
/// (<c>faceShapeOccludes</c>), which needs per-state shapes we do not have; that residue is registered as
/// SIM-CLIENT-LIGHT-NO-FACE-SHAPE / R-056.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.lighting.LevelLightEngine", "ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/LevelLightEngine.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Sky light only, naive full recompute instead of the incremental queue")]
public sealed class SkyLightEngine
{
    /// <summary>Highest light level.</summary>
    public const int MaxLevel = 15;

    /// <summary>How far a change can reach: one level per block, so a change cannot move a cell beyond this.</summary>
    public const int Radius = MaxLevel + 1;

    private readonly ClientWorld _world;
    private readonly Queue<(int X, int Y, int Z)> _queue = new();

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

    /// <summary>
    /// Recomputes the sky light around one cell, which is what a block change has to trigger.
    /// </summary>
    /// <param name="x">Block X.</param>
    /// <param name="y">Block Y.</param>
    /// <param name="z">Block Z.</param>
    /// <returns>True when at least one light cell changed.</returns>
    public bool CheckBlock(int x, int y, int z)
    {
        DirtyChunks.Clear();
        LastChangedCells = 0;

        var minX = x - Radius;
        var maxX = x + Radius;
        var minZ = z - Radius;
        var maxZ = z + Radius;
        var minY = Math.Max(_world.MinSectionY * 16, y - Radius);
        var maxY = Math.Min((_world.MaxSectionY * 16) + 15, y + Radius);

        // Every column in range gets its sources reloaded first: a cell is open to the sky when nothing above it in the
        // column blocks it, and such a cell holds 15 rather than 14 from its neighbour (that is exactly how the
        // reference models "no attenuation downwards"). The source write counts as a change like any other, otherwise a
        // break that only moves the column source would report "nothing to do" and the caller would not rebuild.
        for (var columnX = minX; columnX <= maxX; columnX++)
        {
            for (var columnZ = minZ; columnZ <= maxZ; columnZ++)
            {
                if (ChunkAt(columnX, columnZ) is null)
                {
                    continue;
                }

                var topAbsorber = TopAbsorberY(columnX, columnZ);
                for (var columnY = minY; columnY <= maxY; columnY++)
                {
                    var level = columnY > topAbsorber ? (byte)MaxLevel : (byte)0;
                    if (Store(columnX, columnY, columnZ, level))
                    {
                        LastChangedCells++;
                    }
                }
            }
        }

        // Flood outwards. The shell just outside the recomputed cube keeps whatever it holds and only feeds inwards, so
        // light that was already there is not thrown away.
        _queue.Clear();
        for (var queueX = minX - 1; queueX <= maxX + 1; queueX++)
        {
            for (var queueZ = minZ - 1; queueZ <= maxZ + 1; queueZ++)
            {
                for (var queueY = minY - 1; queueY <= maxY + 1; queueY++)
                {
                    if (Read(queueX, queueY, queueZ) > 0)
                    {
                        _queue.Enqueue((queueX, queueY, queueZ));
                    }
                }
            }
        }

        Span<(int X, int Y, int Z)> neighbours = stackalloc (int, int, int)[6];
        while (_queue.Count > 0)
        {
            var (fromX, fromY, fromZ) = _queue.Dequeue();
            var fromLevel = Read(fromX, fromY, fromZ);
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
                if (toX < minX || toX > maxX || toZ < minZ || toZ > maxZ || toY < minY || toY > maxY)
                {
                    continue;
                }

                // The reference refuses the step when the two shapes together cover the face (shapeOccludes). We only
                // know whether one of them is a full block, which is enough to stop light entering a solid cell and is
                // registered as an approximation for two partial shapes (R-056).
                if (BlocksSky(toX, toY, toZ))
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
                    _queue.Enqueue((toX, toY, toZ));
                }
            }
        }

        return LastChangedCells > 0;
    }

    /// <summary>
    /// Highest cell in a column that blocks the sky; every cell above it is a source of 15.
    /// </summary>
    /// <remarks>
    /// The reference decides this with <c>isEdgeOccluded</c>, which is true when a cell absorbs light (light dampening
    /// not zero) or when its shape fills a face
    /// (ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/ChunkSkyLightSources.java:140-147).
    /// </remarks>
    private int TopAbsorberY(int x, int z)
    {
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
