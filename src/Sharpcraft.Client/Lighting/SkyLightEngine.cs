// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
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
/// optimisation is a separate task.
/// </para>
/// <para>
/// Rules, all from the reference sky light engine
/// (ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/SkyLightEngine.java):
/// a cell open to the sky holds 15, light moves to a neighbour with <c>level - opacity</c> where
/// <c>opacity = max(1, lightDampening)</c> (LightEngine.java:77-79), and the downward direction does not decay for a
/// column that is open to the sky because every such cell is itself a source (SkyLightEngine.java:113-135).
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
        // column absorbs light, and such a cell holds 15 rather than 14 from its neighbour (that is exactly how the
        // reference models "no attenuation downwards").
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
                    Store(columnX, columnY, columnZ, level);
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
                    if (queueX >= minX && queueX <= maxX && queueZ >= minZ && queueZ <= maxZ &&
                        queueY >= minY && queueY <= maxY)
                    {
                        if (Read(queueX, queueY, queueZ) > 0)
                        {
                            _queue.Enqueue((queueX, queueY, queueZ));
                        }

                        continue;
                    }

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
    /// Highest cell in a column that absorbs sky light; every cell above it is a source of 15.
    /// </summary>
    /// <remarks>
    /// One scan per column instead of one per cell is still naive but keeps a block change affordable; the reference
    /// keeps an incrementally maintained height map (<c>ChunkSkyLightSources</c>) instead.
    /// </remarks>
    private int TopAbsorberY(int x, int z)
    {
        var top = (_world.MaxSectionY * 16) + 15;
        for (var y = top; y >= _world.MinSectionY * 16; y--)
        {
            if (LightDampening(x, y, z) > 0)
            {
                return y;
            }
        }

        return int.MinValue;
    }

    /// <summary>How much light a cell takes away, at least one so light never passes a block for free.</summary>
    /// <remarks>ref://mc-26.3-analysis/src/net/minecraft/world/level/lighting/LightEngine.java:77-79.</remarks>
    private int Opacity(int x, int y, int z) => Math.Max(1, LightDampening(x, y, z));

    /// <summary>Light dampening of the block in a cell, from the generated vanilla table.</summary>
    private int LightDampening(int x, int y, int z)
    {
        var state = _world.GetBlockState(x, y, z);
        if (state == 0)
        {
            return 0;
        }

        if (VanillaBlockRegistry.Layout.BlockAt(state) is not { } block)
        {
            // An unresolvable state is treated as opaque, which is the safe answer: it cannot make the world brighter.
            return MaxLevel;
        }

        return VanillaBlockOcclusion.TryGet(block, out var info) ? info.LightDampening : MaxLevel;
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

        DirtyChunks.Add((
            Sharpcraft.Common.Coordinates.BlockCoordinates.ToSectionCoord(x),
            Sharpcraft.Common.Coordinates.BlockCoordinates.ToSectionCoord(z)));
        return true;
    }

    private LevelChunk? ChunkAt(int x, int z) =>
        _world.GetChunk(
            Sharpcraft.Common.Coordinates.BlockCoordinates.ToSectionCoord(x),
            Sharpcraft.Common.Coordinates.BlockCoordinates.ToSectionCoord(z));
}
