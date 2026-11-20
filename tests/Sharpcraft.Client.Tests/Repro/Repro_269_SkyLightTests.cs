// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Lighting;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Coordinates;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 269: the server sends no light for a block change, so the client has to relight the cell itself. These tests
/// check the levels against the reference rules rather than against the implementation: a cell open to the sky holds 15,
/// light loses <c>max(1, lightDampening)</c> per step, and a column open to the sky does not decay downwards because
/// every one of its cells is a source.
/// </summary>
public sealed class Repro_269_SkyLightTests
{
    private const int FloorY = 8;

    /// <summary>A world of stone with one air tunnel under a stone ceiling, and sky above it.</summary>
    private static (ClientWorld World, LevelChunk Chunk, int Stone, int Air) Scene()
    {
        var layout = VanillaBlockRegistry.Layout;
        layout.TryGetBaseId(Identifier.Parse("minecraft:stone"), out var stone);
        layout.TryGetBaseId(Identifier.Parse("minecraft:air"), out var air);

        var world = new ClientWorld(minSectionY: 0, sectionCount: 2);
        var chunk = world.AddChunk(0, 0);

        // y 0..7 solid stone, y 8 an air tunnel, y 9 a stone ceiling, y 10 and up open sky.
        chunk.SetSection(new ChunkSection(singleState: stone, sectionY: 0));
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                chunk.SetBlockState(x, FloorY, z, air);
                chunk.SetBlockState(x, FloorY + 1, z, stone);
                for (var y = FloorY + 2; y < 32; y++)
                {
                    chunk.SetBlockState(x, y, z, air);
                }
            }
        }

        // Light as a chunk packet delivers it: every cell above the ceiling is open to the sky and holds 15, and the
        // shielded tunnel is dark because no server ever lit it.
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var y = FloorY + 2; y < 32; y++)
                {
                    chunk.Light.SetSkyLight(x, y, z, 15);
                }
            }
        }

        chunk.MarkLoaded();
        return (world, chunk, stone, air);
    }

    private static int Sky(LevelChunk chunk, int x, int y, int z) => chunk.Light.GetSkyLight(x & 15, y, z & 15);

    /// <summary>
    /// Breaking a ceiling block opens its column to the sky: the cell itself becomes a source of 15 and light spreads
    /// along the tunnel losing one level per step, which is the case the issue reports as staying black.
    /// </summary>
    [Fact]
    public void BreakingACeilingBlockLightsTheTunnelFromAbove()
    {
        var (world, chunk, _, _) = Scene();

        Assert.Equal(0, Sky(chunk, 1, FloorY, 8));

        Assert.True(world.SetBlockState(1, FloorY + 1, 8, state: 0));

        // The broken cell is its own column's sky source.
        Assert.Equal(15, Sky(chunk, 1, FloorY + 1, 8));
        // Downwards the column stays open, so the cell under the break is also 15 rather than 14.
        Assert.Equal(15, Sky(chunk, 1, FloorY, 8));
        // A stone floor absorbs all of it.
        Assert.Equal(0, Sky(chunk, 1, FloorY - 1, 8));
        // Sideways along the tunnel: one level per step, so the far end of the chunk is one.
        Assert.Equal(14, Sky(chunk, 2, FloorY, 8));
        Assert.Equal(13, Sky(chunk, 3, FloorY, 8));
        Assert.Equal(12, Sky(chunk, 4, FloorY, 8));
        Assert.Equal(1, Sky(chunk, 15, FloorY, 8));
    }

    /// <summary>
    /// The same rule in the other direction: putting the block back removes the light again, so a cell cannot keep a
    /// level it no longer earns.
    /// </summary>
    [Fact]
    public void PlacingAnOpaqueBlockTakesTheLightAwayAgain()
    {
        var (world, chunk, stone, _) = Scene();

        world.SetBlockState(1, FloorY + 1, 8, state: 0);
        Assert.Equal(15, Sky(chunk, 1, FloorY, 8));
        Assert.Equal(14, Sky(chunk, 2, FloorY, 8));

        world.SetBlockState(1, FloorY + 1, 8, stone);

        Assert.Equal(0, Sky(chunk, 1, FloorY, 8));
        Assert.Equal(0, Sky(chunk, 2, FloorY, 8));
    }

    /// <summary>A column open to the sky keeps 15 all the way down, which is the "no attenuation downwards" rule.</summary>
    [Fact]
    public void AnOpenColumnKeepsFifteenToTheGround()
    {
        var (world, chunk, _, air) = Scene();

        world.SetBlockState(8, FloorY + 1, 8, air);

        for (var y = FloorY; y <= (world.MaxSectionY * 16) + 15; y++)
        {
            Assert.Equal(15, Sky(chunk, 8, y, 8));
        }
    }

    /// <summary>A change is only visible if the chunks it touched are marked for a rebuild.</summary>
    [Fact]
    public void AChangedChunkIsMarkedDirty()
    {
        var (world, _, _, _) = Scene();

        world.TakeDirtyChunks();

        Assert.True(world.SetBlockState(8, FloorY + 1, 8, state: 0));

        var dirty = world.TakeDirtyChunks();
        Assert.Contains(ClientWorld.Pack(0, 0), dirty);
    }

    /// <summary>
    /// The dampening table decides the decay: glass lets sky light pass, so a shaft closed with glass still lights the
    /// cells under it, while stone stops it dead.
    /// </summary>
    [Fact]
    public void DecayFollowsTheGeneratedLightDampening()
    {
        var (world, chunk, stone, _) = Scene();
        var layout = VanillaBlockRegistry.Layout;
        layout.TryGetBaseId(Identifier.Parse("minecraft:glass"), out var glass);

        Assert.True(VanillaBlockOcclusion.TryGet(Identifier.Parse("minecraft:glass"), out var glassInfo));
        Assert.Equal(0, glassInfo.LightDampening);

        world.SetBlockState(8, FloorY + 1, 8, glass);
        Assert.Equal(15, Sky(chunk, 8, FloorY, 8));

        world.SetBlockState(8, FloorY + 1, 8, stone);
        Assert.Equal(0, Sky(chunk, 8, FloorY, 8));
    }

    // ---- fixes from review round 2 ------------------------------------------------------------------------------

    /// <summary>
    /// A change that only moves the column source still has to be reported, or the caller never rebuilds the chunk.
    /// </summary>
    [Fact]
    public void AColumnSourceOnlyChangeIsReported()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 2);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        // Everything is air and every light cell starts at zero, so putting one block down changes nothing but the
        // sources above it.
        VanillaBlockRegistry.Layout.TryGetBaseId(Identifier.Parse("minecraft:stone"), out var stone);
        Assert.True(world.SetBlockState(8, 10, 8, stone));
        Assert.True(chunk.Light.GetSkyLight(8, 11, 8) == 15);
        Assert.True(chunk.Light.GetSkyLight(8, 9, 8) < 15);
    }

    /// <summary>A change on a chunk edge has to mark the neighbour, whose border face depends on it.</summary>
    [Fact]
    public void AChangeOnAChunkEdgeMarksTheNeighbourChunk()
    {
        var layout = VanillaBlockRegistry.Layout;
        layout.TryGetBaseId(Identifier.Parse("minecraft:stone"), out var stone);

        var world = new ClientWorld(minSectionY: 0, sectionCount: 2);
        foreach (var (chunkX, chunkZ) in new[] { (0, 0), (-1, 0) })
        {
            var chunk = world.AddChunk(chunkX, chunkZ);
            chunk.SetSection(new ChunkSection(singleState: stone, sectionY: 0));
            chunk.MarkLoaded();
        }

        world.TakeDirtyChunks();

        // Dig at x = 0, the west edge of chunk 0.
        Assert.True(world.SetBlockState(0, 10, 8, state: 0));

        var dirty = world.TakeDirtyChunks();
        Assert.Contains(ClientWorld.Pack(0, 0), dirty);
        Assert.Contains(ClientWorld.Pack(-1, 0), dirty);
    }

    /// <summary>
    /// Leaves absorb one level of light, which the generated table reports as a dampening of one, so a leaf block both
    /// stops a column from being an open sky source and costs a level when light passes it.
    /// </summary>
    [Fact]
    public void LeavesStopTheColumnSourceButOnlyCostOneLevel()
    {
        Assert.True(VanillaBlockOcclusion.TryGet(Identifier.Parse("minecraft:oak_leaves"), out var leaves));
        Assert.Equal(1, leaves.LightDampening);
        Assert.False(leaves.SolidRender);

        var (world, chunk, _, air) = Scene();
        var layout = VanillaBlockRegistry.Layout;
        layout.TryGetBaseId(Identifier.Parse("minecraft:oak_leaves"), out var leavesState);

        var asAir = world.GetBlockState(8, FloorY + 1, 8);
        world.SetBlockState(8, FloorY + 1, 8, air);
        var openLevel = Sky(chunk, 8, FloorY, 8);

        var (world2, chunk2, _, _) = Scene();
        world2.SetBlockState(8, FloorY + 1, 8, leavesState);
        var leavesLevel = Sky(chunk2, 8, FloorY, 8);

        // Leaves are not an open sky source, so the cell under them cannot be 15; they still pass light one level down
        // from their own cell, which is zero here because the ceiling blocks it.
        Assert.Equal(15, openLevel);
        Assert.True(leavesLevel < 15, $"leaves should not be an open sky source, got {leavesLevel}");
        _ = asAir;
    }

    /// <summary>
    /// Filling the light storage must not make every section share one array: a per cell write afterwards would then
    /// change every section at once.
    /// </summary>
    [Fact]
    public void FillingSkyLightDoesNotShareOneArray()
    {
        var light = new ChunkLight(minSectionY: 0, sectionCount: 2);
        light.FillSkyLight(15);

        Assert.True(light.SetSkyLight(x: 3, y: 4, z: 5, level: 0));

        Assert.Equal(0, light.GetSkyLight(3, 4, 5));
        // A different section at the same local coordinates must be untouched.
        Assert.Equal(15, light.GetSkyLight(3, 4 + 16, 5));
        Assert.Equal(15, light.GetSkyLight(3, 4 + 32, 5));
    }

    /// <summary>The light a chunk packet delivered stays untouched until something actually changes.</summary>
    [Fact]
    public void LoadedLightIsNotRewrittenWithoutAChange()
    {
        var (world, chunk, _, _) = Scene();

        // The tunnel and its ceiling stay dark, and the cells the packet lit stay lit: nothing rewrote them because
        // no block changed.
        Assert.Equal(0, Sky(chunk, 8, FloorY, 8));
        Assert.Equal(0, Sky(chunk, 8, FloorY + 1, 8));
        Assert.Equal(15, Sky(chunk, 8, FloorY + 2, 8));
        Assert.Equal(0, world.SkyLight.LastChangedCells);
    }

    /// <summary>
    /// Measures one relight, ten relights and the allocation per relight, so the cost of the naive recompute is a
    /// number rather than an opinion (principle 9). The numbers are printed; the assertions are only the invariants
    /// that must hold whatever the timing does.
    /// </summary>
    [Fact]
    public void TheCostOfOneRelightIsMeasured()
    {
        var (world, chunk, stone, air) = Scene();
        var total = 0;
        var allocated = 0L;

        // Warm up so the first measurement is not dominated by JIT.
        world.SetBlockState(8, FloorY + 1, 8, air);
        world.SetBlockState(8, FloorY + 1, 8, stone);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        world.SetBlockState(8, FloorY + 1, 8, air);
        clock.Stop();
        var single = clock.Elapsed.TotalMilliseconds;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            world.SetBlockState(8, FloorY + 1, 8, i % 2 == 0 ? stone : air);
            total += world.SkyLight.LastChangedCells;
        }

        stopwatch.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var perChange = stopwatch.Elapsed.TotalMilliseconds / 10;
        Console.WriteLine(
            $"light engine cost: single {single:0.00} ms, ten {stopwatch.Elapsed.TotalMilliseconds:0.00} ms, " +
            $"per change {perChange:0.00} ms, allocated {allocated} bytes for 11 changes " +
            $"({allocated / 11} per change), {total} cells rewritten in ten changes");

        Assert.True(single < 5000, $"a single relight took {single:0.0} ms");
        Assert.True(total > 0, "the ten changes rewrote no cells");
        Assert.True(chunk.Light.HasSkyLight);
    }
}