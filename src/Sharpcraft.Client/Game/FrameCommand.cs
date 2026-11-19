// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Collects real chunks from a server, meshes them and renders one offscreen frame to a PNG
/// (docs/decisions/0003-wgpu-binding.md).
/// </summary>
internal static partial class FrameCommand
{
    /// <summary>Runs the whole pipeline and returns the output path.</summary>
    internal static async Task<string> RunAsync(
        string address,
        HttpClient http,
        int width,
        int height,
        int collectSeconds,
        GameIdentity identity,
        CancellationToken cancellationToken)
    {
        if (GameOptions.Get("MC_SUN_TEST") is { Length: > 0 } ||
            GameOptions.Get("MC_MOON_TEST") is { Length: > 0 })
        {
            return RenderSunTest(width, height);
        }

        if (GameOptions.Get("MC_COLOR_TEST") is { Length: > 0 })
        {
            return RenderColorTest(width, height);
        }

        if (GameOptions.Get("MC_SKY_DISC_TEST") is { Length: > 0 })
        {
            return RenderSkyDiscTest(width, height);
        }

        if (GameOptions.Get("MC_FRAME_LOCAL") is { Length: > 0 })
        {
            return RenderLocal(width, height);
        }

        if (GameOptions.Get("MC_FRAME_OCCLUDER") is { Length: > 0 })
        {
            // Deterministic scene for the occlusion test: a solid wall with solid chunks behind it.
            return RenderWorld(BuildOccluderWorld(), width, height, VanillaBlockRegistry.Layout);
        }

        if (GameOptions.Get("MC_FRAME_REPLAY") is { Length: > 0 } replayPath)
        {
            // Real chunk data uses the baseline block layout so state ids resolve to blocks.
            return RenderWorld(LoadReplay(replayPath), width, height, VanillaBlockRegistry.Layout);
        }

        var (outcome, connection) = await GameConnector
            .LoginAsync(address, http, identity, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Success || connection is null)
        {
            throw new InvalidOperationException($"server refused the login: {outcome.Reason}");
        }

        using (connection)
        {
            var probe = await ConfigurationProbe.RunAsync(
                connection!,
                TimeSpan.FromSeconds(20),
                cancellationToken).ConfigureAwait(false);

            if (!probe.ReachedPlay)
            {
                throw new InvalidOperationException($"did not reach play: {probe.Reason}");
            }

            Console.WriteLine("collecting chunks...");
            ClientWorld? world = null;
            var layout = ResolveDimensionLayout(probe.Registries);
            var stats = await PlaySession.RunAsync(
                connection!,
                TimeSpan.FromSeconds(collectSeconds),
                cancellationToken,
                dimensionLayout: joinGame =>
                {
                    var info = layout(joinGame.DimensionTypeId);
                    world ??= new ClientWorld(info.MinSectionY, info.SectionCount);
                    return (info.SectionCount, info.MinSectionY, 0);
                },
                onChunk: parsed =>
                {
                    world ??= new ClientWorld(-4, 24);
                    world.AddChunk(parsed);
                    return true;
                }).ConfigureAwait(false);

            Console.WriteLine($"chunks: {stats.ChunksReceived} ok, {stats.ChunksFailed} failed");
            if (world is null || world.LoadedChunkCount == 0)
            {
                throw new InvalidOperationException("no chunks were received, nothing to render");
            }

            // Apply the clock and weather packets the preload deferred (the join full-clock sync and game events), so the
            // single frame uses the server's real day time and weather.
            if (stats.DeferredPackets is { } deferredPackets)
            {
                foreach (var deferredBody in deferredPackets)
                {
                    var deferredReader = new PayloadReader(deferredBody);
                    if (deferredReader.TryReadVarInt(out var deferredId) != DecodeStatus.Success)
                    {
                        continue;
                    }

                    if (deferredId == PacketIds.PlayClientbound.SetTime && SetTimePacketCodec.TryRead(ref deferredReader, out var setTime) == DecodeStatus.Success)
                    {
                        world.ApplyTime(setTime);
                    }
                    else if (deferredId == PacketIds.PlayClientbound.GameEvent && GameEventPacketCodec.TryRead(ref deferredReader, out var gameEvent) == DecodeStatus.Success)
                    {
                        world.ApplyWeather(gameEvent);
                    }
                }
            }

            Console.WriteLine($"adapter: {OffscreenRenderer.DescribeAdapter()}");
            return RenderWorld(world, width, height, VanillaBlockRegistry.Layout, Sharpcraft.Common.World.BiomeRegistry.From(probe.Registries));
        }
    }

    /// <summary>Builds a deterministic local world so the render path can be tested without a server.</summary>
    internal static ClientWorld BuildDemoWorld()
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);
        var solid = new Common.World.ChunkSection(0, 0);
        var stone = new Common.World.ChunkSection(0, -1);
        chunk.SetSection(solid);
        chunk.SetSection(stone);

        // The demo world has no network light payload, so light it fully; otherwise every face
        // renders at the minimum brightness and the scene is unreadable.
        chunk.Light.FillSkyLight(15);

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                // A small hill of two different states plus a floor slab beneath it.
                var height = 4 + ((x * 3 + z * 5) % 7);
                for (var y = 0; y < height; y++)
                {
                    solid.Set(x, y, z, y < 2 ? 2 : 1);
                }

                stone.Set(x, 15, z, 3);
            }
        }

        return world;
    }

    /// <summary>
    /// Renders a chunk packet captured earlier, so the render path can be exercised on genuine
    /// server data without a live session (docs/specs/quality-attributes.md).
    /// </summary>
    internal static ClientWorld LoadReplay(string path)
    {
        var body = File.ReadAllBytes(path);
        var status = ChunkPacketParser.TryRead(body, sectionCount: 24, minSectionY: -64, airState: 0, out var parsed);
        if (status != DecodeStatus.Success || parsed is null)
        {
            throw new InvalidDataException($"replay packet could not be parsed: {status}");
        }

        Console.WriteLine(
            $"replay: chunk ({parsed.Chunk.ChunkX},{parsed.Chunk.ChunkZ}) " +
            $"{parsed.BlockEntityCount} block entities, {parsed.Light.ArrayCount} light arrays");

        var world = new ClientWorld(parsed.Chunk.MinSectionY, parsed.Chunk.SectionCount);
        world.AddChunk(parsed);
        return world;
    }

    /// <summary>
    /// The demo world's block state layout. Ids match the states the demo world writes, so the same
    /// texture path a real server would use is exercised here as well.
    /// </summary>
    internal static BlockStateLayout DemoLayout() => new(
    [
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "stone"), 1),
        (new Identifier("minecraft", "dirt"), 1),
        (new Identifier("minecraft", "cobblestone"), 1),
        (new Identifier("minecraft", "torch"), 1),
        (new Identifier("minecraft", "flower_pot"), 1)
    ]);
    /// <summary>Builds the frame request (mesh plus camera) for a world.</summary>
    /// <summary>
    /// Builds a deterministic scene for the translucent test: stone with a layer of water on top.
    /// </summary>
    /// <param name="layout">Layout used to resolve the stone and water state ids.</param>
    /// <summary>
    /// Builds a deterministic scene with leaves, to check the cutout layer against a layout we know.
    /// </summary>
    /// <param name="layout">Layout used to resolve the stone and leaves state ids.</param>
    internal static ClientWorld BuildLeavesWorld(BlockStateLayout layout)
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);

        layout.TryGetBaseId(new Identifier("minecraft", "stone"), out var stone);
        layout.TryGetBaseId(new Identifier("minecraft", "oak_leaves"), out var leaves);

        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: stone, sectionY: -1));
        chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: leaves, sectionY: 0));
        chunk.ApplyLight(Sharpcraft.Common.Network.LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();

        return world;
    }

    internal static ClientWorld BuildWaterWorld(BlockStateLayout layout)
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);

        layout.TryGetBaseId(new Identifier("minecraft", "stone"), out var stone);
        layout.TryGetBaseId(new Identifier("minecraft", "water"), out var water);

        // A three by three patch of stone with a pool in the middle and a chest at the edge, so the presented view has
        // something to judge the water against rather than a single flat slab.
        for (var chunkX = -1; chunkX <= 1; chunkX++)
        {
            for (var chunkZ = -1; chunkZ <= 1; chunkZ++)
            {
                var chunk = world.AddChunk(chunkX, chunkZ);
                chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: stone, sectionY: -1));

                if (chunkX == 0 && chunkZ == 0)
                {
                    // Two water layers in front of the wall behind, so the blended layer has stone to show through.
                    chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: water, sectionY: 0));
                    chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: water, sectionY: 1));
                }

                if (chunkX == 0 && chunkZ == 1)
                {
                    // A stone wall behind the water: without something behind it, transparency cannot be judged.
                    chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: stone, sectionY: 0));
                    chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: stone, sectionY: 1));
                }

                // No lighting data exists for a synthetic scene, so the mesher would shade everything to the minimum;
                // full sky light makes the shapes readable.
                chunk.ApplyLight(Sharpcraft.Common.Network.LightData.FullSky(chunk.Light.LightSectionCount));
                chunk.MarkLoaded();
            }
        }

        world.AddChunk(1, 1).AddBlockEntity(new Sharpcraft.Common.World.BlockEntityRecord(8, 0, 8, 1));


        return world;
    }

    /// <summary>
    /// Builds a deterministic scene for the occlusion test: a solid wall with solid chunks behind it.
    /// </summary>
    /// <remarks>
    /// Whole sections are filled with one state, which is a single call per section and needs no per-block loop. The wall is
    /// wide and tall enough to cover the view at the fixed camera, so the chunks behind it are certainly hidden.
    /// </remarks>
    internal static ClientWorld BuildOccluderWorld()
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);

        void Solid(int chunkX, int chunkZ)
        {
            var chunk = world.AddChunk(chunkX, chunkZ);

            for (var section = -1; section <= 1; section++)
            {
                chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: 1, sectionY: section));
            }

            // A chunk that is not marked loaded is skipped by the mesher, which is why the first attempt drew nothing.
            chunk.MarkLoaded();
        }

        // Wall: seven chunks wide at z = 0, tall and wide enough to cover the view at the fixed camera.
        for (var x = -3; x <= 3; x++)
        {
            Solid(x, 0);
        }

        // A chest stands on the wall face the camera looks at, so the block entity stage has something visible to draw.
        // In the chunk in front of the wall: inside the wall it would be buried and invisible.
        var chestChunk = world.AddChunk(0, -1);
        chestChunk.AddBlockEntity(new Sharpcraft.Common.World.BlockEntityRecord(8, 17, 15, 1));
        // A chunk that is not marked loaded is skipped by the mesher, which is why the first attempt drew nothing.
        chestChunk.MarkLoaded();

        // Hidden: three wide and four deep behind the wall, so the rate has a meaningful population to work with.
        for (var z = 3; z <= 6; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                Solid(x, z);
            }
        }

        return world;
    }

    internal static FrameRequest BuildRequest(
        ClientWorld world,
        int width,
        int height,
        out MeshData mesh,
        out (int FirstIndex, int IndexCount)[]? drawRanges,
        BlockSpriteMap? sprites = null,
        TextureData? atlas = null,
        ChunkMeshCache? cache = null,
        bool meshSynchronously = true,
        BlockStateLayout? layout = null,
        BiomeTint? tint = null,
        float[]? spriteRects = null,
        Sharpcraft.Client.Render.ItemAtlas? chestAtlas = null,
        Sharpcraft.Client.Render.BlockModelSet? models = null,
        Sharpcraft.Client.Render.ItemAtlas? itemAtlas = null)
    {
        // With a cache only the chunks whose blocks changed are re-meshed; without one the whole world is
        // scanned, which is what the offscreen paths still do (docs/specs/network/play-movement.md 7.19).
        // The windowed path passes meshSynchronously false and lets the background worker do the meshing.
        layout ??= Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout;

        // Day/night lightmap parameters from the world clock; the offscreen path re-meshes every frame, so this reaches
        // the vertex colours without re-uploading a cached mesh (docs/specs/render/lighting.md 6).
        var lighting = EffectiveLighting(world);

        float[]? translucentVertices = null;
        uint[]? translucentIndices = null;
        float[]? cutoutVertices = null;
        uint[]? cutoutIndices = null;
        Sharpcraft.Client.Render.BlockEntityGeometry? blockEntityGeometry = null;
        var translucentRanges = new List<(int FirstIndex, int IndexCount, float CentreX, float CentreZ)>();

        if (cache is null)
        {
            // Mesh chunk by chunk so each one gets an index range: without ranges a culled chunk cannot be left out.
            using var buffer = new Sharpcraft.Client.Render.MeshBuffer(Math.Min(2_000_000, world.LoadedChunkCount * 512));

            // With a layout the same pass can also fill a second buffer with the translucent faces.
            // The layers are split whenever the layout is known; leaving a holed sprite in the solid pass draws its holes
            // as opaque black.
            var wantsTranslucent = layout is not null;
            using var translucent = wantsTranslucent ? new Sharpcraft.Client.Render.MeshBuffer(Math.Min(2_000_000, world.LoadedChunkCount * 512)) : null;
            using var cutout = wantsTranslucent ? new Sharpcraft.Client.Render.MeshBuffer(Math.Min(2_000_000, world.LoadedChunkCount * 512)) : null;
            var ranges = new List<(int FirstIndex, int IndexCount)>(world.LoadedChunkCount);
            var blocks = 0;

            foreach (var chunk in world.GetChunks())
            {
                var before = buffer.IndexCount;
                var translucentBefore = translucent?.IndexCount ?? 0;

                blocks += Sharpcraft.Client.Render.ChunkMeshCache.MeshChunkOnce(
                    world, chunk, buffer, sprites, translucent, layout, cutout, tint,
                    GameOptions.Get("MC_MESH_NOMERGE") != "1", maxBlocks: 2_000_000 - blocks, models: models);

                ranges.Add((before, buffer.IndexCount - before));

                if (translucent is not null)
                {
                    // Each chunk's translucent faces occupy their own span of the shared buffer.
                    translucentRanges.Add((
                        translucentBefore,
                        translucent.IndexCount - translucentBefore,
                        (chunk.ChunkX * 16) + 8f,
                        (chunk.ChunkZ * 16) + 8f));
                }
            }

            mesh = buffer.ToMeshData(blocks);
            drawRanges = [.. ranges];

            if (GameOptions.Get("MC_FRAME_BLOCK_ENTITIES") == "1")
            {
                var states = new List<Sharpcraft.Client.Render.BlockEntityRenderState>();

                foreach (var chunk in world.GetChunks())
                {
                    foreach (var record in chunk.BlockEntitiesSpan)
                    {
                        var blockX = (chunk.ChunkX * 16) + record.LocalX;
                        var blockZ = (chunk.ChunkZ * 16) + record.LocalZ;
                        var itemSlots = BlockEntityItemsOf(record, out var shelfAlign);
                        var skullType = SkullTypeOf(world, blockX, record.Y, blockZ, out var skullWall, out var skullFacing, out var skullRotation);
                        states.Add(new Sharpcraft.Client.Render.BlockEntityRenderState(
                            blockX,
                            record.Y,
                            blockZ,
                            record.TypeId,
                Facing: Sharpcraft.Common.World.VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(record.TypeId)
                    ? DirectionOrdinalOf(world, blockX, record.Y, blockZ, Sharpcraft.Common.World.VanillaBlockEntityTypes.DirectionOrdinalFallback(record.TypeId))
                    : FacingOf(world, blockX, record.Y, blockZ),
                            ChestType: ChestTypeOf(world, blockX, record.Y, blockZ),
                            ChestMaterial: BlockEntityMaterialOf(world, blockX, record.Y, blockZ, record.TypeId),
                            HasBook: HasBookOf(world, blockX, record.Y, blockZ),
                            Items: VaultItemsOf(record, BrushableItemsOf(record, itemSlots)),
                            Dusted: DustedOf(world, blockX, record.Y, blockZ),
                            HitDirection: BrushableHitDirectionOf(record),
                            ShelfAlignItemsToBottom: shelfAlign,
                            SkullType: skullType,
                            SkullWall: skullWall,
                            SkullFacing: skullFacing,
                            SkullRotation: skullRotation,
                            Pose: CopperGolemPoseOf(world, blockX, record.Y, blockZ),
                            BannerAttachment: BannerAttachmentOf(world, blockX, record.Y, blockZ),
                            BannerRotation: BannerRotationOf(world, blockX, record.Y, blockZ),
                            BannerColor: BannerColorOf(world, blockX, record.Y, blockZ),
                            BannerPhase: BannerPhaseOf(world, blockX, record.Y, blockZ, partialTick: 0f),
                            ConduitRotation: 0f,
                            ConduitActive: false,
                            Open: world.BlockEntityAnimations.GetOpenness(blockX, record.Y, blockZ)));
                }
            }

                if (states.Count > 0)
                {
                    // Every block entity a registered renderer claims is drawn through the dispatcher;
                    // the rest keep the placeholder merge until their renderer exists.
                    var dispatcher = new Sharpcraft.Client.Render.BlockEntityRenderDispatcher();
                    var modelStates = new List<Sharpcraft.Client.Render.BlockEntityRenderState>(states.Count);
                    var otherStates = new List<Sharpcraft.Client.Render.BlockEntityRenderState>(states.Count);
                    foreach (var state in states)
                    {
                        if (dispatcher.Handles(state.TypeId))
                        {
                            modelStates.Add(state);
                        }
                        else
                        {
                            otherStates.Add(state);
                        }
                    }

                    if (modelStates.Count > 0 && chestAtlas is not null)
                    {
                        blockEntityGeometry = dispatcher.Build(
                            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(modelStates),
                            chestAtlas,
                            itemAtlas);
                    }

                    if (otherStates.Count > 0)
                    {
                        // The placeholder merges into the terrain mesh, so its faces go through the block atlas
                        // table: rect zero is the whole atlas and would paint a shrunken atlas on every box.
                        var mesher = new Sharpcraft.Client.Render.BlockEntityPlaceholderMesher();
                        var placeholderGeometry = mesher.Build(
                            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(otherStates),
                            sprites?.Missing ?? new SpriteRect(0, 0, 16, 16, 1) { RectId = 0, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f });

                        var mergedVertices = new float[mesh.Vertices.Length + placeholderGeometry.VertexFloats];
                        mesh.Vertices.CopyTo(mergedVertices, 0);
                        placeholderGeometry.Vertices[..placeholderGeometry.VertexFloats].CopyTo(mergedVertices.AsSpan(mesh.Vertices.Length));

                        var mergedIndices = new uint[mesh.Indices.Length + placeholderGeometry.IndexCount];
                        mesh.Indices.CopyTo(mergedIndices, 0);
                        var vertexOffset = (uint)(mesh.Vertices.Length / Sharpcraft.Client.Render.ChunkMesher.FloatsPerVertex);

                        for (var index = 0; index < placeholderGeometry.IndexCount; index++)
                        {
                            mergedIndices[mesh.Indices.Length + index] = placeholderGeometry.Indices[index] + vertexOffset;
                        }

                        var placeholderFirstIndex = mesh.Indices.Length;
                        mesh = new Sharpcraft.Client.Render.MeshData(mergedVertices, mergedIndices, mesh.BlockCount);
                        drawRanges = [.. drawRanges, (placeholderFirstIndex, placeholderGeometry.IndexCount)];
                        Console.WriteLine($"block entities: {otherStates.Count} placeholder boxes, {placeholderGeometry.IndexCount / 3} triangles");
                    }
                }
            }

            if (translucent is not null)
            {
                var split = translucent.ToMeshData(0);
                translucentVertices = split.Vertices;
                translucentIndices = split.Indices;
            }

            if (cutout is not null)
            {
                var split = cutout.ToMeshData(0);
                cutoutVertices = split.Vertices;
                cutoutIndices = split.Indices;
            }


            Console.WriteLine(
                $"layers: solid {drawRanges!.Sum(range => range.IndexCount) / 3} tri, " +
                $"cutout {(cutoutIndices?.Length ?? 0) / 3} tri, translucent {(translucentIndices?.Length ?? 0) / 3} tri");
        }
        else
        {
            mesh = meshSynchronously
                ? cache.Build(world, sprites, maxBlocks: 2_000_000, models: models)
                : cache.Snapshot();
            drawRanges = null;


            // The sandbox branch above builds block entity geometry inline; the cached path used by a real
            // session has to build it here, or a chest in a live world has no geometry at all.
            blockEntityGeometry = BuildBlockEntityGeometry(world, chestAtlas, itemAtlas: itemAtlas);
            // The cache is the only mesher (T-793): while it meshes a chunk it also forwards the blended faces into its
            // layer sinks, so the startup frame reads that aggregate instead of meshing the world a second time. Cutout
            // faces are not split out of the solid mesh by design (they are alpha tested inside it), which is why only
            // the translucent layer is taken from here.
            if (cache.Layers?.Translucent is { IndexCount: > 0 } translucentLayer)
            {
                var split = translucentLayer.ToMeshData(0);
                translucentVertices = split.Vertices;
                translucentIndices = split.Indices;
                Console.WriteLine($"layers: translucent {(translucentIndices?.Length ?? 0) / 3} tri (from the cache)");
            }
        }

        var bounds = BoundsOf(mesh);

        // Frame the horizontal footprint: a tall dimension range (y from -64 to 320) would
        // otherwise push the camera so far out that the terrain becomes a sliver.
        // Aim at the top surface and size the sphere by the footprint: a world column can be
        // hundreds of blocks tall, which would otherwise swamp the framing.
        var centre = new Vector3(
            (bounds.Min.X + bounds.Max.X) / 2f,
            bounds.Max.Y,
            (bounds.Min.Z + bounds.Max.Z) / 2f);
        var footprintX = bounds.Max.X - bounds.Min.X;
        var footprintZ = bounds.Max.Z - bounds.Min.Z;
        var radius = MathF.Max(4f, MathF.Sqrt(footprintX * footprintX + footprintZ * footprintZ) / 2f);
        var (eye, target) = CameraMath.DefaultCamera(centre, radius);

        // A fixed camera makes an occlusion test reproducible; six comma separated numbers.
        if (GameOptions.Get("MC_FRAME_CAM") is { Length: > 0 } cameraText)
        {
            var parts = cameraText.Split(',');

            if (parts.Length == 6 &&
                float.TryParse(parts[0], out var camX) && float.TryParse(parts[1], out var camY) && float.TryParse(parts[2], out var camZ) &&
                float.TryParse(parts[3], out var aimX) && float.TryParse(parts[4], out var aimY) && float.TryParse(parts[5], out var aimZ))
            {
                eye = new Vector3(camX, camY, camZ);
                target = new Vector3(aimX, aimY, aimZ);
            }
        }
        // Blending is order dependent, so translucent chunks are drawn far to near.
        var translucentOrdered = translucentRanges
            .Where(range => range.IndexCount > 0)
            .OrderByDescending(range =>
            {
                var offsetX = range.CentreX - eye.X;
                var offsetZ = range.CentreZ - eye.Z;
                return (offsetX * offsetX) + (offsetZ * offsetZ);
            })
            .Select(range => (range.FirstIndex, range.IndexCount))
            .ToArray();

        if (translucentOrdered.Length > 0)
        {
            Console.WriteLine($"translucent ranges: {translucentOrdered.Length}, far to near");
        }

        return new FrameRequest(
            width,
            height,
            mesh.Vertices,
            mesh.Indices,
            new Vector4(0.55f, 0.72f, 0.95f, 1f),
            eye,
            target)
        {
            Diagnostics = GameOptions.ForFrame(),
            Lightmap = ToLightmapUniform(lighting),
            FogColour = EffectiveFogColour(world),
            Atlas = atlas,
            SpriteRects = spriteRects ?? [],
            DrawRanges = drawRanges,
            TranslucentVertices = translucentVertices,
            TranslucentIndices = translucentIndices,
            CutoutVertices = cutoutVertices,
            CutoutIndices = cutoutIndices,
            TranslucentRanges = translucentOrdered.Length > 0 ? translucentOrdered : null,
            // Full rain pulls the atmospheric shell in, so the sandbox can show it closing the view.
            RainLevel = EffectiveRainLevel(world, 1f),
            BlockEntityAtlas = chestAtlas?.Texture,
            BlockEntitySpriteRects = chestAtlas is null ? [] : Sharpcraft.Client.Render.ChestAtlas.BuildRectTable(chestAtlas),
            BlockEntityGeometry = blockEntityGeometry?.Geometry,
            BlockEntitySolidIndexCount = blockEntityGeometry?.SolidIndexCount ?? 0
        };
    }

    /// <summary>Renders the world to an offscreen frame and writes a PNG.</summary>
    internal static string RenderWorld(ClientWorld world, int width, int height, BlockStateLayout? layout = null, Sharpcraft.Common.World.BiomeRegistry? biomes = null)
    {
        // Every frame path reports the same fingerprint for the same scene, so two runs can be proven comparable before
        // their pixels are compared.
        var firstChunk = world.GetChunks().Length > 0 ? world.GetChunks()[0] : null;
        Console.WriteLine($"scene: chunks {world.LoadedChunkCount}, minSectionY {firstChunk?.MinSectionY}, sections {firstChunk?.SectionCount}, " +
            $"fingerprint 0x{Sharpcraft.Client.World.SceneFingerprint.Of(world):X16}");

        if (GameOptions.Get("MC_FRAME_RECORD") is { Length: > 0 } recordPath)
        {
            // Keep the scene so later runs render exactly the same world instead of whatever the server shipped.
            Sharpcraft.Client.World.SceneRecorder.Save(world, recordPath);
            Console.WriteLine($"scene: recorded to {recordPath}");

            // Prove the file reproduces the scene instead of assuming it does.
            var reloaded = Sharpcraft.Client.World.SceneRecorder.Load(recordPath);
            var lossy = Sharpcraft.Client.World.SceneRecorder.TryFindFirstDifference(world, reloaded, out var detail);
            Console.WriteLine(lossy ? $"scene: roundtrip differs: {detail}" : "scene: roundtrip identical");
        }

        var prepared = PrepareSprites(layout);
        layout ??= Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout;

        // Colour maps come from the resource pack; without them the tint falls back to the reference constants. The stack
        // is cheap to build, and this path only builds one frame.
        using var tintResources = CreateResourceManager();
        var tint = Sharpcraft.Client.Render.BiomeTint.Load(tintResources);
        if (biomes is not null)
        {
            // Per-biome block tint: resolve the biome at each position and average over the biome-blend square (radius 2).
            tint = tint.WithBiomes(world.GetBiomeAt, biomes, blendRadius: 2);
        }

        if (GameOptions.Get("MC_BIOME_PROBE") == "1" && biomes is not null)
        {
            Console.WriteLine($"biome probe: registry {biomes.Entries.Count} entries");
            var seen = new HashSet<int>();
            foreach (var chunk in world.GetChunks())
            {
                var section = chunk.GetSection(chunk.MinSectionY);
                if (section is null || !section.HasBiomes)
                {
                    continue;
                }

                var span = section.Biomes;
                for (var i = 0; i < span.Length && seen.Count < 10; i++)
                {
                    var id = span[i];
                    if (!seen.Add(id))
                    {
                        continue;
                    }

                    var entry = biomes.ById(id);
                    var grass = tint.ColourAt(BlockTintKind.Grass, (chunk.ChunkX * 16) + 8, (chunk.MinSectionY * 16) + 8, (chunk.ChunkZ * 16) + 8);
                    Console.WriteLine(entry is null
                        ? $"  id {id}: <no entry> -> grass 0x{grass & 0xFFFFFF:X6}"
                        : $"  id {id} {entry.Id}: temp {entry.Climate.Temperature:F2} downfall {entry.Climate.Downfall:F2} water 0x{entry.Effects.WaterColor & 0xFFFFFF:X6} grassOverride {(entry.Effects.GrassColor is int g ? $"0x{g & 0xFFFFFF:X6}" : "none")} mod {entry.Effects.GrassColorModifier} -> grass 0x{grass & 0xFFFFFF:X6}");
                }

                if (seen.Count >= 10)
                {
                    break;
                }
            }
        }


        var request = BuildRequest(
            world,
            width,
            height,
            out var mesh,
            out var drawRanges,
            prepared?.Map,
            prepared is null ? null : WithMips(prepared.Value.Atlas),
            layout: layout,
            spriteRects: prepared?.Atlas.RectTable,
            chestAtlas: Sharpcraft.Client.Render.ChestAtlas.Load(tintResources),
            models: prepared?.Models);
        Console.WriteLine($"mesh: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");
        Console.WriteLine($"layers: translucent triangles {(request.TranslucentIndices?.Length ?? 0) / 3}, layout {(layout is null ? "null" : "set")}, water->{(layout is null ? "-" : layout.BlockAt(1)?.ToString() ?? "none")}");

        // Sky background and fog use SKY_COLOR (biome sky colour x day/night x weather), like the reference, instead of the
        // static gradient. Clear the target to it and skip the gradient pass.
        if (biomes is not null)
        {
            var eye = request.CameraPosition;
            var sampleX = (int)MathF.Floor(eye.X);
            var sampleY = (int)MathF.Floor(eye.Y);
            var sampleZ = (int)MathF.Floor(eye.Z);
            var chunks = world.GetChunks();
            if (chunks.Length > 0 && world.GetChunk(sampleX >> 4, sampleZ >> 4) is null)
            {
                // The offscreen camera frames the whole world and can sit outside the loaded area; sample a loaded column
                // so the biome sky colour is not zero.
                sampleX = (chunks[0].ChunkX * 16) + 8;
                sampleZ = (chunks[0].ChunkZ * 16) + 8;
            }

            var sky = EffectiveSkyColour(world, biomes, sampleX, sampleY, sampleZ);
            request = request with { SkipGradientSky = true, ClearColour = new Vector4(sky, 1f), FogColour = new Vector4(sky, 1f) };
        }

        // Celestial bodies (the sun for now): drawn into the sky backdrop for the current day time.
        request = AttachCelestial(request, world, tintResources);

        // Per-cell clouds (docs/modules/clouds.md 2); appended after the celestial draws so it is not overwritten and
        // drawn with the same position-colour pass before terrain.
        if (biomes is not null
            && GameOptions.Get("MC_NO_CLOUDS") != "1"
            && Sharpcraft.Client.Assets.CloudTexture.Load(tintResources) is { } cloudTexture
            && BuildCloudDraw(world, cloudTexture, request.CameraPosition) is { } cloudDraw)
        {
            request = request with { CelestialColorDraws = [.. (request.CelestialColorDraws ?? []), cloudDraw] };
        }

        // Opt-in sky disc: replace the gradient backdrop with the SKY_COLOR disc (docs/modules/weather-and-sky-color.md 6.2).
        if (GameOptions.Get("MC_SKY_DISC") == "1" && biomes is not null)
        {
            var eye = request.CameraPosition;
            var sampleX = (int)MathF.Floor(eye.X);
            var sampleY = (int)MathF.Floor(eye.Y);
            var sampleZ = (int)MathF.Floor(eye.Z);

            // The offscreen camera frames the whole world and can sit outside the loaded area; sample a loaded column then.
            var chunks = world.GetChunks();
            if (chunks.Length > 0 && world.GetChunk(sampleX >> 4, sampleZ >> 4) is null)
            {
                sampleX = (chunks[0].ChunkX * 16) + 8;
                sampleZ = (chunks[0].ChunkZ * 16) + 8;
            }

            var baseSky = Sharpcraft.Common.World.SkyColourSource.ColorAt(world.GetBiomeAt, biomes, sampleX, sampleY, sampleZ);
            var sky = ApplySkyWeather(baseSky, world);
            request = AppendSkyDisc(request, sky);
            Console.WriteLine(
                $"sky disc: biomes {biomes.Entries.Count}, sample ({sampleX},{sampleY},{sampleZ}) id {world.GetBiomeAt(sampleX, sampleY, sampleZ)}, " +
                $"colour ({sky.X:F3},{sky.Y:F3},{sky.Z:F3}), gradient skipped");
        }

        var frame = OffscreenRenderer.Render(request);

        Console.WriteLine($"rendered on adapter: {frame.AdapterName}");


        if (GameOptions.Get("MC_FRAME_TRANSLUCENT_TEST") == "1" && request.TranslucentIndices is { Length: > 0 })
        {
            // The same frame without the translucent layer, so the difference is exactly what blending contributed.
            var opaqueOnly = OffscreenRenderer.Render(request with { TranslucentVertices = null, TranslucentIndices = null });
            var difference = Sharpcraft.Framework.Rendering.PixelComparison.Compare(frame.Rgba, opaqueOnly.Rgba, frame.Width, frame.Height, 0);

            Console.WriteLine(
                $"translucent: {request.TranslucentIndices.Length / 3} triangles, pixels changed {difference.Mismatches}" +
                (difference.Mismatches > 0 ? $", first ({difference.FirstX},{difference.FirstY})" : string.Empty));
        }


        if (GameOptions.Get("MC_FRAME_CULLTEST") == "1" && request.DrawRanges is { Length: > 0 } ranges)
        {
            // Two checks in one: with the natural decision the picture must not change at all, and with a forced drop it must
            // change, which proves the ranges are actually honoured rather than silently merged back into one draw.
            var forced = GameOptions.Get("MC_FRAME_CULL_FORCE") == "1";
            var dropped = 0;
            var modified = new (int FirstIndex, int IndexCount)[ranges.Length];

            // Natural mode uses the real decision; forced mode drops the first chunk to prove the ranges are honoured.
            var occluded = new bool[ranges.Length];

            if (!forced && frame.Depth is { } cullDepth)
            {
                var linear = new float[cullDepth.Length];

                for (var index = 0; index < cullDepth.Length; index++)
                {
                    linear[index] = Sharpcraft.Framework.Rendering.DepthLinearisation.ToDistance(cullDepth[index], 0.1f, 2000f);
                }

                var pyramid = Sharpcraft.Framework.Rendering.DepthPyramid.Build(linear, frame.Width, frame.Height);
                var chunks = world.GetChunks();

                for (var index = 0; index < ranges.Length && index < chunks.Length; index++)
                {
                    var chunk = chunks[index];
                    var lowest = chunk.MinSectionY * 16;
                    var highest = (chunk.MinSectionY + chunk.SectionCount) * 16;
                    var centre = new System.Numerics.Vector3(
                        (chunk.ChunkX * 16) + 8f,
                        (lowest + highest) / 2f,
                        (chunk.ChunkZ * 16) + 8f);
                    var half = new System.Numerics.Vector3(8f, (highest - lowest) / 2f, 8f);

                    if (!Sharpcraft.Client.Render.OcclusionCulling.TryProjectBox(centre, half, frame.ViewProjection, out var rectMin, out var rectMax, out var nearest))
                    {
                        continue;
                    }

                    if (!pyramid.TryCoveringDepth(rectMin.X, rectMin.Y, rectMax.X, rectMax.Y, out var farthest))
                    {
                        continue;
                    }

                    var nearestDistance = Sharpcraft.Framework.Rendering.DepthLinearisation.ToDistance(nearest, 0.1f, 2000f);
                    occluded[index] = Sharpcraft.Client.Render.OcclusionCulling.IsOccluded(nearestDistance, farthest);
                }
            }

            for (var index = 0; index < ranges.Length; index++)
            {
                var forcedIndex = GameOptions.Get("MC_FRAME_CULL_FORCE_INDEX") is { Length: > 0 } indexText && int.TryParse(indexText, out var parsedIndex)
                    ? parsedIndex
                    : 0;
                var skip = forced ? index == forcedIndex : occluded[index];

                if (skip)
                {
                    dropped++;
                }

                modified[index] = skip ? (ranges[index].FirstIndex, 0) : ranges[index];
            }

            var second = OffscreenRenderer.Render(request with { DrawRanges = modified });
            var difference = Sharpcraft.Framework.Rendering.PixelComparison.Compare(
                frame.Rgba,
                second.Rgba,
                frame.Width,
                frame.Height,
                tolerance: 0);

            Console.WriteLine(
                $"culltest: mode {(forced ? "forced" : "natural")}, dropped {dropped} of {ranges.Length}, " +
                $"pixel mismatches {difference.Mismatches}" +
                (difference.Mismatches > 0 ? $", first at ({difference.FirstX},{difference.FirstY})" : string.Empty));
        }

        if (GameOptions.Get("MC_FRAME_COMPARE") is { Length: > 0 } baselinePath && File.Exists(baselinePath))
        {
            // Compare against a reference frame: zero mismatches is the only acceptable result for an optimisation that
            // claims not to change the picture, and a blend may round by one, which the tolerance covers.
            var baseline = Sharpcraft.Common.Imaging.PngDecoder.Decode(File.ReadAllBytes(baselinePath));
            var tolerance = int.TryParse(GameOptions.Get("MC_FRAME_TOLERANCE"), out var parsed) ? parsed : 0;
            var difference = Sharpcraft.Framework.Rendering.PixelComparison.Compare(
                baseline.Pixels,
                frame.Rgba,
                frame.Width,
                frame.Height,
                tolerance);

            Console.WriteLine(difference.Mismatches == 0
                ? $"compare: identical ({frame.Width}x{frame.Height}, max channel delta {difference.MaxChannelDelta}, tolerance {tolerance})"
                : $"compare: {difference.Mismatches} pixel(s) differ, first at ({difference.FirstX},{difference.FirstY}), max channel delta {difference.MaxChannelDelta}");
        }
        if (GameOptions.Get("MC_FRAME_PROBE") is { Length: > 0 } probeText)
        {
            // Sample named pixels from the rendered frame, so a claim about a colour is a number taken from the image.
            foreach (var pair in probeText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = pair.Split(',');

                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var probeX) &&
                    int.TryParse(parts[1], out var probeY) &&
                    probeX >= 0 && probeX < frame.Width && probeY >= 0 && probeY < frame.Height)
                {
                    var offset = ((probeY * frame.Width) + probeX) * 4;
                    Console.WriteLine(
                        $"probe ({probeX},{probeY}) = rgba({frame.Rgba[offset]},{frame.Rgba[offset + 1]},{frame.Rgba[offset + 2]},{frame.Rgba[offset + 3]}) " +
                        $"argb 0x{Sharpcraft.Framework.Rendering.PixelComparison.ArgbAt(frame.Rgba, frame.Width, probeX, probeY):X8}");
                }
            }
        }

        if (GameOptions.Get("MC_FRAME_CULL") == "1" && frame.Depth is { } depthPixels)
        {
            // Conservative occlusion decision over the chunk boxes, using the verified pyramid from this frame's depth.
            var linear = new float[depthPixels.Length];

            for (var index = 0; index < depthPixels.Length; index++)
            {
                linear[index] = Sharpcraft.Framework.Rendering.DepthLinearisation.ToDistance(depthPixels[index], 0.1f, 2000f);
            }

            var pyramid = Sharpcraft.Framework.Rendering.DepthPyramid.Build(linear, frame.Width, frame.Height);
            var considered = 0;
            var culled = 0;

            foreach (var chunk in world.GetChunks())
            {
                var lowest = chunk.MinSectionY * 16;
                var highest = (chunk.MinSectionY + chunk.SectionCount) * 16;
                var centre = new System.Numerics.Vector3(
                    (chunk.ChunkX * 16) + 8f,
                    (lowest + highest) / 2f,
                    (chunk.ChunkZ * 16) + 8f);
                var half = new System.Numerics.Vector3(8f, (highest - lowest) / 2f, 8f);

                if (!Sharpcraft.Client.Render.OcclusionCulling.TryProjectBox(centre, half, frame.ViewProjection, out var rectMin, out var rectMax, out var nearest))
                {
                    continue;
                }

                considered++;

                if (!pyramid.TryCoveringDepth(rectMin.X, rectMin.Y, rectMax.X, rectMax.Y, out var farthest))
                {
                    continue;
                }

                var nearestDistance = Sharpcraft.Framework.Rendering.DepthLinearisation.ToDistance(nearest, 0.1f, 2000f);

                if (Sharpcraft.Client.Render.OcclusionCulling.IsOccluded(nearestDistance, farthest))
                {
                    culled++;
                }
            }

            Console.WriteLine(
                $"cull: chunks {considered}, culled {culled} ({(considered == 0 ? 0f : 100f * culled / considered):F1}%), visible {considered - culled}");
        }

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "sharpcraft-frame.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    private static string RenderLocal(int width, int height)
    {
        var world = BuildDemoWorld();

        if (GameOptions.Get("MC_DEMO_STATE") is { Length: > 0 } forced &&
            int.TryParse(forced, out var forcedState))
        {
            // Diagnostic: paint every block with one state so a wrong sprite is unmistakable.
            var forcedChunk = world.GetChunks().First();
            for (var y = 0; y < 11; y++)
            {
                var section = forcedChunk.GetSection(y >> 4);
                if (section is null)
                {
                    continue;
                }

                for (var x = 0; x < 16; x++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        if (section.Get(x, y & 15, z) != 0)
                        {
                            section.Set(x, y & 15, z, forcedState);
                        }
                    }
                }
            }
        }

        // The demo world's states are laid out explicitly, so the same sprite path a real server
        // would exercise is used here as well.
        // Local verification of the element model path: the demo world carries a torch, whose model
        // is a thin post rather than a cube, so the same resolver the server path uses runs here.
        if (GameOptions.Get("MC_FRAME_MODELS") is "1" or "2" && world.GetChunk(0, 0) is { } modelChunk)
        {
            var modelState = GameOptions.Get("MC_FRAME_MODELS") == "2" ? 5 : 4;
            modelChunk.GetSection(10 >> 4)?.Set(8, 10 & 15, 8, modelState);
        }

        var prepared = PrepareSprites(DemoLayout());

        if (GameOptions.Get("MC_DEBUG_SPRITES") is "1")
        {
            for (var y = 0; y < 12; y++)
            {
                Console.WriteLine($"  demo block (0,{y},0) state {world.GetBlockState(0, y, 0)}");
            }
        }
        using var resources = CreateResourceManager();

        // The demo world has no biome data, so this entry passes neither layout nor tint and the diagnostic frame keeps
        // its baseline geometry. That is a deliberate local-scene hole, not production: the live session meshes through
        // ChunkMeshCache/ChunkWorkerPool with both, and both entries accept them (issue 227). Passing the layout here
        // changes this scene's culling (2392 -> 4176 triangles), which is a separate question and not this issue's.
        var mesh = Sharpcraft.Client.Render.ChunkMeshCache.MeshWorldOnce(
            world, prepared?.Map, GameOptions.Get("MC_MESH_NOMERGE") != "1", prepared?.Models);
        Console.WriteLine($"local demo: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var bounds = (Min: new Vector3(0, -16, 0), Max: new Vector3(16, 16, 16));
        var centre = (bounds.Min + bounds.Max) / 2f;
        var (eye, target) = CameraMath.DefaultCamera(centre, 16f);

        var request = new FrameRequest(
            width,
            height,
            mesh.Vertices,
            mesh.Indices,
            new Vector4(0.55f, 0.72f, 0.95f, 1f),
            eye,
            target)
        {
            Lightmap = ToLightmapUniform(EffectiveLighting(world)),
            Atlas = prepared is null
                ? null
                : WithMips(prepared.Value.Atlas),

            // The vertex carries a sprite id, not the atlas bounds, so the frame is only textured when its table
            // travels with it. Without this the scene sampled the whole atlas and every face was a shrunken copy of
            // it, which is what the diagnostic frame showed (issue 226).
            SpriteRects = prepared?.Atlas.RectTable ?? [],

            // The local scene has no weather of its own, so it takes the forced values. Without these the
            // sandbox always renders clear weather and cannot show what rain does to the fog (T-842).
            RainLevel = EffectiveRainLevel(world, 1f),
            FogColour = EffectiveFogColour(world)
        };
        request = AttachCelestial(request, world, resources);

        // Local verification of the block entity segment: the demo world carries a chest, so the same
        // chest mesher and atlas the server path uses can be exercised here without a running server.
        if (GameOptions.Get("MC_FRAME_BLOCK_ENTITIES") == "1")
        {
            var chestAtlas = Sharpcraft.Client.Render.ChestAtlas.Load(resources);

            // The demo world is bare, so stand one chest on the platform the camera frames.
            if (world.GetChunk(0, 0) is { } demoChunk)
            {
                // State 5562 is a single chest facing east, so the facing path is exercised here.
                demoChunk.GetSection(10 >> 4)?.Set(8, 10 & 15, 8, 5562);
                demoChunk.AddBlockEntity(new Sharpcraft.Common.World.BlockEntityRecord(8, 10, 8, 1));
            }
            var blockEntityStates = new List<Sharpcraft.Client.Render.BlockEntityRenderState>();
            foreach (var chunk in world.GetChunks())
            {
                foreach (var record in chunk.BlockEntitiesSpan)
                {
                    var blockX = (chunk.ChunkX * 16) + record.LocalX;
                    var blockZ = (chunk.ChunkZ * 16) + record.LocalZ;
                    var itemSlots = BlockEntityItemsOf(record, out var shelfAlign);
                    var skullType = SkullTypeOf(world, blockX, record.Y, blockZ, out var skullWall, out var skullFacing, out var skullRotation);
                    blockEntityStates.Add(new Sharpcraft.Client.Render.BlockEntityRenderState(
                        blockX,
                        record.Y,
                        blockZ,
                        record.TypeId,
                    Facing: Sharpcraft.Common.World.VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(record.TypeId)
                        ? DirectionOrdinalOf(world, blockX, record.Y, blockZ, Sharpcraft.Common.World.VanillaBlockEntityTypes.DirectionOrdinalFallback(record.TypeId))
                        : FacingOf(world, blockX, record.Y, blockZ),
                        ChestType: ChestTypeOf(world, blockX, record.Y, blockZ),
                        ChestMaterial: BlockEntityMaterialOf(world, blockX, record.Y, blockZ, record.TypeId),
                        HasBook: HasBookOf(world, blockX, record.Y, blockZ),
                        Items: VaultItemsOf(record, BrushableItemsOf(record, itemSlots)),
                        Dusted: DustedOf(world, blockX, record.Y, blockZ),
                        HitDirection: BrushableHitDirectionOf(record),
                        ShelfAlignItemsToBottom: shelfAlign,
                        SkullType: skullType,
                        SkullWall: skullWall,
                        SkullFacing: skullFacing,
                        SkullRotation: skullRotation,
                        Pose: CopperGolemPoseOf(world, blockX, record.Y, blockZ),
                        BannerAttachment: BannerAttachmentOf(world, blockX, record.Y, blockZ),
                        BannerRotation: BannerRotationOf(world, blockX, record.Y, blockZ),
                        BannerColor: BannerColorOf(world, blockX, record.Y, blockZ),
                        BannerPhase: BannerPhaseOf(world, blockX, record.Y, blockZ, partialTick: 0f),
                        ConduitRotation: 0f,
                        ConduitActive: false,
                        Open: world.BlockEntityAnimations.GetOpenness(blockX, record.Y, blockZ)));
            }
            }

            if (blockEntityStates.Count > 0)
            {
                var dispatcher = new Sharpcraft.Client.Render.BlockEntityRenderDispatcher();
                var geometry = dispatcher.Build(
                    System.Runtime.InteropServices.CollectionsMarshal.AsSpan(blockEntityStates),
                    chestAtlas,
                    itemAtlas: null);
                request = request with
                {
                    BlockEntityAtlas = chestAtlas.Texture,
                    BlockEntitySpriteRects = Sharpcraft.Client.Render.ChestAtlas.BuildRectTable(chestAtlas),
                    BlockEntityGeometry = geometry?.Geometry,
                    BlockEntitySolidIndexCount = geometry?.SolidIndexCount ?? 0
                };
                Console.WriteLine($"block entities(local): {geometry?.Geometry.IndexCount / 3 ?? 0} tri");
            }
        }

        // Opt-in sky disc for local verification: synthetic temperature, gradient replaced by the SKY_COLOR disc.
        if (GameOptions.Get("MC_SKY_DISC") == "1")
        {
            var temperature = GameOptions.Get("MC_BIOME_TEMP") is { Length: > 0 } text && float.TryParse(text, out var parsed)
                ? parsed
                : 0.5f;
            var packed = SkyTintColour(temperature);
            var sky = ApplySkyWeather(packed, world);
            request = AppendSkyDisc(request, sky);
            Console.WriteLine($"sky disc(local): temp {temperature:F2} colour ({sky.X:F3},{sky.Y:F3},{sky.Z:F3}), gradient skipped");
        }

        var frame = OffscreenRenderer.Render(request);

        Console.WriteLine($"rendered on adapter: {frame.AdapterName}");

        if (GameOptions.Get("MC_DEBUG_SPRITES") is "1")
        {
            // Sample a few pixels so a wrong sprite shows up as a number, not as an impression.
            foreach (var (label, x, y) in new[]
                     {
                         ("top-centre", frame.Width / 2, (frame.Height / 2) - 60),
                         ("top-mid", frame.Width / 2, frame.Height / 2),
                         ("left-side", (frame.Width / 2) - 120, (frame.Height / 2) + 60),
                         ("sky", 10, 10)
                     })
            {
                var offset = (((y * frame.Width) + x) * 4);
                Console.WriteLine(
                    $"  pixel {label,-12} ({x},{y}) rgba({frame.Rgba[offset]},{frame.Rgba[offset + 1]},{frame.Rgba[offset + 2]})");
            }
        }

        if (GameOptions.Get("MC_FRAME_COMPARE") is { Length: > 0 } baselinePath && File.Exists(baselinePath))
        {
            // Compare against a reference frame: zero mismatches is the only acceptable result for an optimisation that
            // claims not to change the picture, and a blend may round by one, which the tolerance covers.
            var baseline = Sharpcraft.Common.Imaging.PngDecoder.Decode(File.ReadAllBytes(baselinePath));
            var tolerance = int.TryParse(GameOptions.Get("MC_FRAME_TOLERANCE"), out var parsed) ? parsed : 0;
            var difference = Sharpcraft.Framework.Rendering.PixelComparison.Compare(
                baseline.Pixels,
                frame.Rgba,
                frame.Width,
                frame.Height,
                tolerance);

            Console.WriteLine(difference.Mismatches == 0
                ? $"compare: identical ({frame.Width}x{frame.Height}, max channel delta {difference.MaxChannelDelta}, tolerance {tolerance})"
                : $"compare: {difference.Mismatches} pixel(s) differ, first at ({difference.FirstX},{difference.FirstY}), max channel delta {difference.MaxChannelDelta}");
        }
        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "sharpcraft-frame.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>Exact extent of the emitted geometry, so the camera frames what is actually drawn.</summary>
    private static (Vector3 Min, Vector3 Max) BoundsOf(MeshData mesh)
    {
        if (mesh.VertexCount == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (var i = 0; i < mesh.Vertices.Length; i += ChunkMesher.FloatsPerVertex)
        {
            var position = new Vector3(mesh.Vertices[i], mesh.Vertices[i + 1], mesh.Vertices[i + 2]);
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        return (min, max);
    }

    internal static Func<int, (int SectionCount, int MinSectionY)> ResolveDimensionLayout(DynamicRegistries registries)
    {
        var registryKey = Identifier.Parse("minecraft:dimension_type");

        return dimensionTypeId =>
        {
            var entry = registries.ById(registryKey, dimensionTypeId);
            var info = DimensionTypeInfo.TryRead(entry?.Data);
            return info is null ? (24, -4) : (info.SectionCount, info.MinSectionY);
        };
    }

    /// <summary>
    /// The direction a block entity faces as the reference's Direction ordinal (down, up, north,
    /// south, west, east). Shulker boxes and decorated pots use this encoding; chests use the
    /// face-step encoding from <see cref="FacingOf"/> instead.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java:33-38,66-77,311-312</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/DecoratedPotBlockEntity.java:75-76</c>.
    /// </summary>
    /// <param name="fallback">Ordinal used when the block state has no facing property.</param>
    private static byte DirectionOrdinalOf(ClientWorld world, int x, int y, int z, byte fallback)
    {
        if (!Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                world.GetBlockState(x, y, z),
                Sharpcraft.Common.Registries.BlockStatePropertyNames.Facing,
                out var facing))
        {
            return fallback;
        }

        return facing switch
        {
            "down" => 0,
            "up" => 1,
            "north" => 2,
            "south" => 3,
            "west" => 4,
            "east" => 5,
            _ => fallback
        };
    }

    /// <summary>
    /// Reads the fixed item snapshot from a block entity's NBT. Missing or malformed item data falls
    /// back to empty slots; the shelf bottom-alignment flag is read independently.
    /// </summary>
    private static Sharpcraft.Client.World.BlockEntityItemSlots BlockEntityItemsOf(
        Sharpcraft.Common.World.BlockEntityRecord record,
        out bool shelfAlignItemsToBottom)
    {
        shelfAlignItemsToBottom = Sharpcraft.Client.World.BlockEntityData.TryReadShelfAlign(record, out var align) && align;
        if (Sharpcraft.Client.Render.EntityModelResolver.Items is not { } registry)
        {
            return default;
        }

        Sharpcraft.Client.World.BlockEntityData.TryReadItems(record, registry, out var slots);
        return slots;
    }

    /// <summary>
    /// Reads a brushable block's single item and maps it into the fixed slot snapshot used by the
    /// shared item mesher. Non-brushable or malformed records keep the normal shelf/campfire slots.
    /// </summary>
    private static Sharpcraft.Client.World.BlockEntityItemSlots BrushableItemsOf(
        Sharpcraft.Common.World.BlockEntityRecord record,
        Sharpcraft.Client.World.BlockEntityItemSlots fallback)
    {
        if (Sharpcraft.Client.Render.EntityModelResolver.Items is not { } registry)
        {
            return fallback;
        }

        return Sharpcraft.Client.World.BlockEntityData.TryReadBrushable(record, registry, out var data) && !data.Item.IsEmpty
            ? new Sharpcraft.Client.World.BlockEntityItemSlots(Slot0: data.Item)
            : fallback;
    }

    /// <summary>Reads a vault's single display item into the fixed slot used by the item mesher.</summary>
    private static Sharpcraft.Client.World.BlockEntityItemSlots VaultItemsOf(
        Sharpcraft.Common.World.BlockEntityRecord record,
        Sharpcraft.Client.World.BlockEntityItemSlots fallback)
    {
        if (Sharpcraft.Client.Render.EntityModelResolver.Items is not { } registry)
        {
            return fallback;
        }

        return Sharpcraft.Client.World.BlockEntityData.TryReadVaultDisplayItem(record, registry, out var item)
            && !item.IsEmpty
            ? new Sharpcraft.Client.World.BlockEntityItemSlots(Slot0: item)
            : fallback;
    }

    /// <summary>Reads a brushable block's hit direction, or None when absent.</summary>
    private static Sharpcraft.Client.World.HitDirection BrushableHitDirectionOf(
        Sharpcraft.Common.World.BlockEntityRecord record)
    {
        if (Sharpcraft.Client.Render.EntityModelResolver.Items is not { } registry)
        {
            return Sharpcraft.Client.World.HitDirection.None;
        }

        Sharpcraft.Client.World.BlockEntityData.TryReadBrushable(record, registry, out var data);
        return data.HitDirection;
    }

    /// <summary>The brushable block's <c>dusted</c> block-state property, zero when absent or malformed.</summary>
    private static byte DustedOf(ClientWorld world, int x, int y, int z)
    {
        if (!Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                world.GetBlockState(x, y, z),
                Sharpcraft.Common.Registries.BlockStatePropertyNames.Dusted,
                out var dusted)
            || !byte.TryParse(dusted, out var value))
        {
            return 0;
        }

        return value;
    }

    /// <summary>
    /// Reads a skull's static block variant and placement. Wall skulls use the block's facing and
    /// ground skulls use its rotation segment; unsupported skull variants map to None.
    /// </summary>
    private static Sharpcraft.Client.Render.SkullType SkullTypeOf(
        ClientWorld world,
        int x,
        int y,
        int z,
        out bool wall,
        out byte facing,
        out byte rotation)
    {
        var block = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z));
        var path = block?.Path;
        wall = path is not null && path.Contains("wall", StringComparison.Ordinal);
        var type = Sharpcraft.Client.Render.SkullModelDefinition.TypeOfPath(path);
        if (wall)
        {
            facing = DirectionOrdinalOf(world, x, y, z, fallback: 2);
            rotation = 0;
        }
        else
        {
            facing = 5;
            rotation = 0;
            if (Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                    world.GetBlockState(x, y, z),
                    Sharpcraft.Common.Registries.BlockStatePropertyNames.Rotation,
                    out var rotationText))
            {
                byte.TryParse(rotationText, out rotation);
            }
        }

        return type;
    }

    /// <summary>
    /// The lectern block's <c>has_book</c> property, false when the state has no such property.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/LecternBlock.java:44-60</c>.
    /// </summary>
    private static bool HasBookOf(ClientWorld world, int x, int y, int z)
    {
        return Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
            world.GetBlockState(x, y, z),
            Sharpcraft.Common.Registries.BlockStatePropertyNames.HasBook,
            out var hasBook)
            && hasBook == "true";
    }

    /// <summary>
    /// The facing a chest's block carries, or south when it has none. Chests take their direction
    /// from the block state rather than from their block entity data.
    /// </summary>
    private static byte FacingOf(ClientWorld world, int x, int y, int z)    {
        if (!Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                world.GetBlockState(x, y, z),
                Sharpcraft.Common.Registries.BlockStatePropertyNames.Facing,
                out var facing))
        {
            return 5;
        }

        return facing switch
        {
            "west" => 0,
            "east" => 1,
            "north" => 4,
            _ => 5
        };
    }


    /// <summary>
    /// Which half of a double chest the block is, or single. Values follow the reference's
    /// <c>ChestType</c> ordinal: single zero, left one, right two.
    /// </summary>
    private static byte ChestTypeOf(ClientWorld world, int x, int y, int z)
    {
        if (!Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                world.GetBlockState(x, y, z),
                Sharpcraft.Common.Registries.BlockStatePropertyNames.Type,
                out var type))
        {
            return 0;
        }

        return type switch
        {
            "left" => 1,
            "right" => 2,
            _ => 0
        };
    }

    /// <summary>
    /// Material of the chest block at a position, from the block identifier. Waxed copper shares the
    /// appearance of the unwaxed stage it was made from, so both map to the same material.
    /// </summary>
    private static byte ChestMaterialOf(ClientWorld world, int x, int y, int z)
    {
        var block = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z));
        if (block is null)
        {
            return 0;
        }

        return block.Value.Path switch
        {
            "trapped_chest" => Sharpcraft.Client.Render.ChestAtlas.Trapped,
            "ender_chest" => Sharpcraft.Client.Render.ChestAtlas.Ender,
            "copper_chest" or "waxed_copper_chest" => Sharpcraft.Client.Render.ChestAtlas.Copper,
            "exposed_copper_chest" or "waxed_exposed_copper_chest" => Sharpcraft.Client.Render.ChestAtlas.CopperExposed,
            "weathered_copper_chest" or "waxed_weathered_copper_chest" => Sharpcraft.Client.Render.ChestAtlas.CopperWeathered,
            "oxidized_copper_chest" or "waxed_oxidized_copper_chest" => Sharpcraft.Client.Render.ChestAtlas.CopperOxidized,
            _ => Sharpcraft.Client.Render.ChestAtlas.Normal
        };
    }

    /// <summary>
    /// Selects the material-like byte passed to a block entity renderer: shulker dye colour,
    /// copper golem statue weathering stage, or chest material.
    /// </summary>
    private static byte BlockEntityMaterialOf(ClientWorld world, int x, int y, int z, int typeId)
    {
        if (Sharpcraft.Common.World.VanillaBlockEntityTypes.IsShulkerBox(typeId))
        {
            return ShulkerColorOf(world, x, y, z);
        }

        if (Sharpcraft.Common.World.VanillaBlockEntityTypes.IsCopperGolemStatue(typeId))
        {
            return CopperGolemStatueOxidationOf(world, x, y, z);
        }

        return ChestMaterialOf(world, x, y, z);
    }

    /// <summary>
    /// Copper golem statue pose from the block's <c>pose</c> property: standing 0, sitting 1,
    /// running 2, star 3. Missing or unknown states fall back to standing, matching the block default.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/CopperGolemStatueBlock.java:174-196</c>.
    /// </summary>
    private static Sharpcraft.Client.Render.CopperGolemPose CopperGolemPoseOf(ClientWorld world, int x, int y, int z)
    {
        if (!Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                world.GetBlockState(x, y, z),
                Sharpcraft.Common.Registries.BlockStatePropertyNames.CopperGolemPose,
                out var pose))
        {
            return Sharpcraft.Client.Render.CopperGolemPose.Standing;
        }

        return pose switch
        {
            "sitting" => Sharpcraft.Client.Render.CopperGolemPose.Sitting,
            "running" => Sharpcraft.Client.Render.CopperGolemPose.Running,
            "star" => Sharpcraft.Client.Render.CopperGolemPose.Star,
            _ => Sharpcraft.Client.Render.CopperGolemPose.Standing
        };
    }

    /// <summary>Banner attachment type: ground for a standing banner, wall for a wall banner.</summary>
    private static Sharpcraft.Client.Render.BannerAttachment BannerAttachmentOf(ClientWorld world, int x, int y, int z)
    {
        var path = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z))?.Path;
        return path is not null && path.Contains("wall_banner", StringComparison.Ordinal)
            ? Sharpcraft.Client.Render.BannerAttachment.Wall
            : Sharpcraft.Client.Render.BannerAttachment.Ground;
    }

    /// <summary>
    /// Ground banners use their 16-segment <c>rotation</c>; wall banners use their horizontal
    /// <c>facing</c> Direction ordinal. Unknown/missing states use the block defaults (8 and north).
    /// </summary>
    private static byte BannerRotationOf(ClientWorld world, int x, int y, int z)
    {
        var path = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z))?.Path;
        if (path is not null && path.Contains("wall_banner", StringComparison.Ordinal))
        {
            return DirectionOrdinalOf(world, x, y, z, fallback: 2);
        }

        if (Sharpcraft.Common.Registries.BlockStateDefinitions.Baseline.TryGetProperty(
                world.GetBlockState(x, y, z),
                "rotation",
                out var rotation)
            && byte.TryParse(rotation, out var segment))
        {
            return segment;
        }

        return 8;
    }

    /// <summary>
    /// Source banner phase: <c>(floorMod(x*7 + y*9 + z*13 + gameTime, 100) + partialTick) / 100</c>.
    /// Exposed with an explicit game time so the formula can be tested without a network world.
    /// </summary>
    internal static float BannerPhase(int x, int y, int z, long gameTime, float partialTick)
    {
        var sum = (x * 7L) + (y * 9L) + (z * 13L) + gameTime;
        var mod = sum % 100L;
        if (mod < 0L)
        {
            mod += 100L;
        }

        return (mod + partialTick) / 100f;
    }

    private static float BannerPhaseOf(ClientWorld world, int x, int y, int z, float partialTick) =>
        BannerPhase(x, y, z, world.GameTime, partialTick);

    /// <summary>Dye colour index for a banner block path, in DyeColor id order.</summary>
    private static byte BannerColorOf(ClientWorld world, int x, int y, int z)
    {
        var path = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z))?.Path;
        return path switch
        {
            "white_banner" or "white_wall_banner" => 0,
            "orange_banner" or "orange_wall_banner" => 1,
            "magenta_banner" or "magenta_wall_banner" => 2,
            "light_blue_banner" or "light_blue_wall_banner" => 3,
            "yellow_banner" or "yellow_wall_banner" => 4,
            "lime_banner" or "lime_wall_banner" => 5,
            "pink_banner" or "pink_wall_banner" => 6,
            "gray_banner" or "gray_wall_banner" => 7,
            "light_gray_banner" or "light_gray_wall_banner" => 8,
            "cyan_banner" or "cyan_wall_banner" => 9,
            "purple_banner" or "purple_wall_banner" => 10,
            "blue_banner" or "blue_wall_banner" => 11,
            "brown_banner" or "brown_wall_banner" => 12,
            "green_banner" or "green_wall_banner" => 13,
            "red_banner" or "red_wall_banner" => 14,
            "black_banner" or "black_wall_banner" => 15,
            _ => 0
        };
    }

    /// <summary>
    /// Weathering stage of a copper golem statue block: unaffected 0, exposed 1, weathered 2,
    /// oxidized 3. Waxed variants keep the appearance of the unwaxed stage, as in the reference
    /// <c>CopperGolemOxidationLevels</c> mapping.
    /// Ref: <c>ref://mc-26.3-analysis/src/net/minecraft/world/entity/animal/golem/CopperGolemOxidationLevels.java</c>.
    /// </summary>
    private static byte CopperGolemStatueOxidationOf(ClientWorld world, int x, int y, int z)
    {
        var block = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z));
        if (block is null)
        {
            return 0;
        }

        return block.Value.Path switch
        {
            "exposed_copper_golem_statue" or "waxed_exposed_copper_golem_statue" => 1,
            "weathered_copper_golem_statue" or "waxed_weathered_copper_golem_statue" => 2,
            "oxidized_copper_golem_statue" or "waxed_oxidized_copper_golem_statue" => 3,
            _ => 0
        };
    }

    /// <summary>Dye index of a shulker box, or 255 for the undyed default. The reference orders the sixteen colours the way its dye enum does.</summary>
    private static byte ShulkerColorOf(ClientWorld world, int x, int y, int z)
    {
        var block = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(x, y, z));
        if (block is null)
        {
            return 255;
        }

        return block.Value.Path switch
        {
            "white_shulker_box" => 0,
            "orange_shulker_box" => 1,
            "magenta_shulker_box" => 2,
            "light_blue_shulker_box" => 3,
            "yellow_shulker_box" => 4,
            "lime_shulker_box" => 5,
            "pink_shulker_box" => 6,
            "gray_shulker_box" => 7,
            "light_gray_shulker_box" => 8,
            "cyan_shulker_box" => 9,
            "purple_shulker_box" => 10,
            "blue_shulker_box" => 11,
            "brown_shulker_box" => 12,
            "green_shulker_box" => 13,
            "red_shulker_box" => 14,
            "black_shulker_box" => 15,
            _ => 255
        };
    }

    /// <summary>Packs a block position the way the reference does.</summary>
    private static long PositionKey(int x, int y, int z) =>
        ((long)x & 0x3FFFFFF) << 38 | ((long)z & 0x3FFFFFF) << 12 | ((long)y & 0xFFF);

    /// <summary>Builds the chest segment for a live session from the block entities in the loaded chunks.</summary>
    internal static Sharpcraft.Client.Render.BlockEntityGeometry? BuildBlockEntityGeometry(
        ClientWorld world,
        Sharpcraft.Client.Render.ItemAtlas? chestAtlas,
        float partialTick = 0f,
        Sharpcraft.Client.Render.BlockEntityFrameState? frameState = null,
        Sharpcraft.Client.Render.ItemAtlas? itemAtlas = null)
    {
        var frame = frameState ?? new Sharpcraft.Client.Render.BlockEntityFrameState();
        frame.Dispatcher.Trace = Sharpcraft.Client.Game.GameOptions.Get("MC_DEBUG_MODELS") is { Length: > 0 };
        if (chestAtlas is null)
        {
            return null;
        }

        if (Sharpcraft.Client.Game.GameOptions.Get("MC_DEBUG_MODELS") is { Length: > 0 })
        {
            var allTypes = new List<int>();
            foreach (var c in world.GetChunks()) { foreach (var r in c.BlockEntities) { allTypes.Add(r.TypeId); } }
            Console.WriteLine($"block entity records: {allTypes.Count} total, types=[" + string.Join(",", allTypes) + "]");
        }



        var chestStates = frame.States;
        chestStates.Clear();
        foreach (var chunk in world.GetChunks())
        {
            foreach (var record in chunk.BlockEntitiesSpan)
            {
                if (!frame.Dispatcher.Handles(record.TypeId))
                {
                    continue;
                }

                var blockX = (chunk.ChunkX * 16) + record.LocalX;
                var blockZ = (chunk.ChunkZ * 16) + record.LocalZ;
                var itemSlots = BlockEntityItemsOf(record, out var shelfAlign);
                var skullType = SkullTypeOf(world, blockX, record.Y, blockZ, out var skullWall, out var skullFacing, out var skullRotation);
                chestStates.Add(new Sharpcraft.Client.Render.BlockEntityRenderState(
                    blockX,
                    record.Y,
                    blockZ,
                    record.TypeId,
                Facing: Sharpcraft.Common.World.VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(record.TypeId)
                    ? DirectionOrdinalOf(world, blockX, record.Y, blockZ, Sharpcraft.Common.World.VanillaBlockEntityTypes.DirectionOrdinalFallback(record.TypeId))
                    : FacingOf(world, blockX, record.Y, blockZ),
                    ChestType: ChestTypeOf(world, blockX, record.Y, blockZ),
                    ChestMaterial: BlockEntityMaterialOf(world, blockX, record.Y, blockZ, record.TypeId),
                    HasBook: HasBookOf(world, blockX, record.Y, blockZ),
                    Items: VaultItemsOf(record, BrushableItemsOf(record, itemSlots)),
                    Dusted: DustedOf(world, blockX, record.Y, blockZ),
                    HitDirection: BrushableHitDirectionOf(record),
                    ShelfAlignItemsToBottom: shelfAlign,
                    SkullType: skullType,
                    SkullWall: skullWall,
                    SkullFacing: skullFacing,
                    SkullRotation: skullRotation,
                    Pose: CopperGolemPoseOf(world, blockX, record.Y, blockZ),
                    BannerAttachment: BannerAttachmentOf(world, blockX, record.Y, blockZ),
                    BannerRotation: BannerRotationOf(world, blockX, record.Y, blockZ),
                    BannerColor: BannerColorOf(world, blockX, record.Y, blockZ),
                    BannerPhase: BannerPhaseOf(world, blockX, record.Y, blockZ, partialTick),
                    ConduitRotation: 0f,
                    ConduitActive: false,
                    Open: world.BlockEntityAnimations.GetOpenness(blockX, record.Y, blockZ, partialTick)));
            }
        }

        if (Sharpcraft.Client.Game.GameOptions.Get("MC_DEBUG_MODELS") is { Length: > 0 })
        {
            Console.WriteLine($"block entity geometry: {chestStates.Count} chest entities");
        }

        // A double chest opens as far as its more open half. The reference walks to one side only:
        // the clockwise block for a left half, the counter-clockwise one for a right half, and it
        // accepts that neighbour only when it is the same block, carries the complementary type and
        // faces the same way (issues 27 and 28).
        var anyDouble = false;
        for (var scan = 0; scan < chestStates.Count; scan++)
        {
            if (chestStates[scan].ChestType != 0)
            {
                anyDouble = true;
                break;
            }
        }

        if (anyDouble)
        {
            var byPosition = frame.ByPosition;
            byPosition.Clear();
            foreach (var state in chestStates)
            {
                byPosition[PositionKey((int)state.X, (int)state.Y, (int)state.Z)] = state;
            }

            for (var i = 0; i < chestStates.Count; i++)
            {
                var state = chestStates[i];
                if (state.ChestType == 0)
                {
                    continue;
                }

                var (dx, dz) = Sharpcraft.Client.Render.ChestPairing.ConnectedOffset(state.Facing, state.ChestType);
                var nx = (int)state.X + dx;
                var nz = (int)state.Z + dz;
                if (!byPosition.TryGetValue(PositionKey(nx, (int)state.Y, nz), out var other))
                {
                    continue;
                }

                if (!Sharpcraft.Client.Render.ChestPairing.IsPartner(state.Facing, state.ChestType, other.Facing, other.ChestType))
                {
                    continue;
                }

                // Same block: a chest must never pair with a trapped or a different copper chest that
                // merely happens to be adjacent.
                var selfBlock = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState((int)state.X, (int)state.Y, (int)state.Z));
                var otherBlock = Sharpcraft.Common.Registries.VanillaBlockRegistry.Layout.BlockAt(world.GetBlockState(nx, (int)state.Y, nz));
                if (selfBlock?.Path != otherBlock?.Path)
                {
                    continue;
                }

                if (other.Open > state.Open)
                {
                    chestStates[i] = state with { Open = other.Open };
                }
            }
        }

        if (chestStates.Count == 0)
        {
            return null;
        }

        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chestStates);
        var geometry = frame.Dispatcher.Build(span, chestAtlas, itemAtlas);
        if (Sharpcraft.Client.Game.GameOptions.Get("MC_DEBUG_MODELS") is { Length: > 0 })
        {
            Console.WriteLine($"block entity geometry: {(geometry is null ? 0 : geometry.Geometry.IndexCount / 3)} tri");
        }

        return geometry;
    }


}
