// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// T-850: a chest on a real server drew as a magenta block because the chunk worker pool never
/// received the block model set, so the background mesher cubed every chest block. This is the
/// minimal reproduction: the same section meshed without a model set must produce cube geometry,
/// and with one must produce none.
/// </summary>
public sealed class Repro_T850Tests
{
    private static (ClientWorld World, BlockModelSet Models) ChestScene()
    {
        var layout = new BlockStateLayout(
        [
            (Identifier.Parse("minecraft:air"), 1),
            (Identifier.Parse("minecraft:chest"), 24)
        ]);

        // Air occupies state zero, so the chest block starts at state one.
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: 0));
        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();

        // A chest has a model definition with no elements: the reference draws nothing for it and the
        // block entity renderer takes over. The fake library has to say the same, or the set has
        // nothing to record and the cube path would paint the chest.
        var library = new BlockModelLibrary(
            _ => Identifier.Parse("minecraft:block/chest"),
            new BlockModelElements(_ => System.Text.Encoding.UTF8.GetBytes("{\"elements\":[]}")),
            (_, _) => null);

        var models = BlockModelSet.Build(library, layout);
        Assert.NotNull(models);
        return (world, models!);
    }

    /// <summary>Without the model set the chest is cubed; with it, nothing is emitted.</summary>
    [Fact]
    public void AChestIsCubedWithoutAModelSetAndSkippedWithOne()
    {
        var (world, models) = ChestScene();

        using var without = new MeshBuffer();
        ChunkMesher.Mesh(world, without, airState: 0, sprites: null, models: null);
        Assert.True(without.IndexCount > 0, "the reproduction needs the chest to be cubed when no model set is given");

        using var with = new MeshBuffer();
        ChunkMesher.Mesh(world, with, airState: 0, sprites: null, models: models);
        Assert.Equal(0, with.IndexCount);
    }

    /// <summary>The set reports the chest state as excluded, which is what the mesher keys on.</summary>
    [Fact]
    public void TheSetExcludesEveryChestState()
    {
        var (_, models) = ChestScene();

        for (var state = 1; state <= 24; state++)
        {
            Assert.True(models.Has(state), $"chest state {state} must be excluded from the cube path");
        }
    }

    /// <summary>
    /// The regression that matters: a pool built without the model set cubes the chest, and one
    /// built with it emits nothing. Dropping the constructor argument breaks every call site at
    /// compile time; passing null where the set belongs is what the second half catches.
    /// </summary>
    [Fact]
    public void ThePoolCubesChestsWithoutAModelSetAndSkipsThemWithOne()
    {
        var (world, models) = ChestScene();

        using (var without = new ChunkWorkerPool(world, sprites: null, models: null, workerCount: 1))
        {
            Assert.True(without.TryEnqueueMesh(ClientWorld.Pack(0, 0)));
            var cubed = WaitForMesh(without);
            Assert.NotNull(cubed);
            Assert.True(cubed!.TriangleCount > 0, "a pool without a model set must cube the chest");
        }

        using (var with = new ChunkWorkerPool(world, sprites: null, models: models, workerCount: 1))
        {
            Assert.True(with.TryEnqueueMesh(ClientWorld.Pack(0, 0)));
            var skipped = WaitForMesh(with);
            Assert.NotNull(skipped);
            Assert.Equal(0, skipped!.TriangleCount);
        }
    }

    private static MeshData? WaitForMesh(ChunkWorkerPool pool, double seconds = 3)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (pool.TryTakeMesh(out _, out var mesh, out _, out _))
            {
                return mesh;
            }

            Thread.Sleep(5);
        }

        return null;
    }
}
