// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Auth;
using Sharpcraft.Launcher.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher;

/// <summary>
/// Collects real chunks from a server, meshes them and renders one offscreen frame to a PNG
/// (docs/decisions/0003-wgpu-binding.md).
/// </summary>
internal static class FrameCommand
{
    /// <summary>Runs the whole pipeline and returns the output path.</summary>
    internal static async Task<string> RunAsync(
        string address,
        HttpClient http,
        int width,
        int height,
        int collectSeconds,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 })
        {
            return RenderLocal(width, height);
        }

        if (Environment.GetEnvironmentVariable("MC_FRAME_REPLAY") is { Length: > 0 } replayPath)
        {
            // Real chunk data uses the baseline block layout so state ids resolve to blocks.
            return RenderWorld(LoadReplay(replayPath), width, height, VanillaBlockRegistry.Layout);
        }

        var signedIn = await AuthenticationFlow.EnsureSignedInAsync(
            http,
            TokenStore.CreateDefault(),
            forceInteractive: false,
            onChallenge: (challenge, _) =>
            {
                Console.WriteLine($"Open {challenge.VerificationUri} and enter the code: {challenge.UserCode}");
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Signed in as {signedIn.Profile.Name}");

        var join = new SessionServerClient(http);
        var (outcome, connection) = await MinecraftLoginClient.LoginAsync(
            address,
            (signedIn.Profile.Name, signedIn.Profile.Id),
            signedIn.Session.AccessToken,
            (token, hash, ct) => join.JoinAsync(signedIn.Session, signedIn.Profile.Id, hash, ct),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!outcome.Success)
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

            Console.WriteLine($"adapter: {OffscreenRenderer.DescribeAdapter()}");
            return RenderWorld(world, width, height, VanillaBlockRegistry.Layout);
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
    /// Builds the sprite atlas and state mapping for a frame. The client owns stitching (ADR-0007);
    /// the launcher only supplies the pack path it already manages.
    /// </summary>
    /// <param name="layout">Block state layout, or null when it is not known yet.</param>
    /// <returns>Atlas plus mapping, or null when no pack is installed.</returns>
    internal static (SpriteAtlas Atlas, BlockSpriteMap Map)? PrepareSprites(BlockStateLayout? layout)
    {
        if (layout is null)
        {
            // Without a block state layout every state would resolve to the missing sprite, which
            // paints the whole world purple. Colour-only faces are the honest fallback until the
            // layout arrives (T-039).
            Console.WriteLine(
                "sprites: block state layout not assembled yet (T-039); rendering colour-only faces " +
                "instead of an all-missing-sprite world");
            return null;
        }

        var packPath = FindPack();
        if (packPath is null)
        {
            Console.WriteLine("sprites: no asset pack found; rendering colour-only faces");
            return null;
        }

        // The stack stays alive until the atlas and mapping are built, then it is released.
        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new BuiltInFallbackSource())
            .Add(new ZipAssetSource(packPath, "base-pack"));

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var atlas = SpriteAtlasBuilder.Build(stack);
        timer.Stop();

        var models = new BlockModelResolver(stack);
        var map = new BlockSpriteMap(atlas, layout, models);

        if (Environment.GetEnvironmentVariable("MC_DEBUG_SPRITES") is "1" && layout is not null)
        {
            // Which blocks still have no texture, so the gap is measured instead of guessed.
            var missing = atlas.Sprites[SpriteAtlas.MissingSprite];
            var unresolved = new List<string>();

            for (var block = 0; block < layout.Blocks.Count; block++)
            {
                var entry = layout.Blocks[block];
                if (map.ForState(entry.BaseId) == missing && entry.Id.Path != "air")
                {
                    unresolved.Add(entry.Id.ToString());
                }
            }

            Console.WriteLine(
                $"  sprite coverage: {layout.Blocks.Count - unresolved.Count}/{layout.Blocks.Count} blocks textured, " +
                $"{unresolved.Count} missing");

            foreach (var blockId in unresolved.Take(8))
            {
                Console.WriteLine($"    no texture: {blockId}");
            }
        }
        Console.WriteLine(
            $"sprites: {atlas.Width}x{atlas.Height} atlas, {atlas.Sprites.Count} sprites, " +
            $"layout {(map.HasLayout ? "present" : "missing")}, {timer.Elapsed.TotalMilliseconds:F0} ms");

        return (atlas, map);
    }

    /// <summary>Finds the newest asset pack the launcher produced.</summary>
    internal static string? FindPack()
    {
        var configured = Environment.GetEnvironmentVariable("MC_ASSETS_PACK");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var directory = Environment.GetEnvironmentVariable("MC_ASSETS_OUT") is { Length: > 0 } output
            ? output
            : HostPaths.DefaultPacksDirectory();

        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.pack.zip")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
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
        (new Identifier("minecraft", "cobblestone"), 1)
    ]);

    /// <summary>Builds the frame request (mesh plus camera) for a world.</summary>
    internal static FrameRequest BuildRequest(
        ClientWorld world,
        int width,
        int height,
        out MeshData mesh,
        BlockSpriteMap? sprites = null,
        TextureData? atlas = null)
    {
        mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 2_000_000, sprites: sprites);

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
        return new FrameRequest(
            width,
            height,
            mesh.Vertices,
            mesh.Indices,
            new Vector4(0.55f, 0.72f, 0.95f, 1f),
            eye,
            target)
        {
            Atlas = atlas
        };
    }

    /// <summary>Renders the world to an offscreen frame and writes a PNG.</summary>
    internal static string RenderWorld(ClientWorld world, int width, int height, BlockStateLayout? layout = null)
    {
        var prepared = PrepareSprites(layout);
        var request = BuildRequest(
            world,
            width,
            height,
            out var mesh,
            prepared?.Map,
            prepared is null ? null : new TextureData(prepared.Value.Atlas.Width, prepared.Value.Atlas.Height, prepared.Value.Atlas.Pixels));
        Console.WriteLine($"mesh: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var frame = OffscreenRenderer.Render(request);

        Console.WriteLine($"rendered on adapter: {frame.AdapterName}");
        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "minecraftcsharp-frame.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    private static string RenderLocal(int width, int height)
    {
        var world = BuildDemoWorld();

        if (Environment.GetEnvironmentVariable("MC_DEMO_STATE") is { Length: > 0 } forced &&
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
        var prepared = PrepareSprites(DemoLayout());

        if (Environment.GetEnvironmentVariable("MC_DEBUG_SPRITES") is "1")
        {
            for (var y = 0; y < 12; y++)
            {
                Console.WriteLine($"  demo block (0,{y},0) state {world.GetBlockState(0, y, 0)}");
            }
        }
        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 2_000_000, sprites: prepared?.Map);
        Console.WriteLine($"local demo: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var bounds = (Min: new Vector3(0, -16, 0), Max: new Vector3(16, 16, 16));
        var centre = (bounds.Min + bounds.Max) / 2f;
        var (eye, target) = CameraMath.DefaultCamera(centre, 16f);

        var frame = OffscreenRenderer.Render(new FrameRequest(
            width,
            height,
            mesh.Vertices,
            mesh.Indices,
            new Vector4(0.55f, 0.72f, 0.95f, 1f),
            eye,
            target)
        {
            Atlas = prepared is null
                ? null
                : new TextureData(prepared.Value.Atlas.Width, prepared.Value.Atlas.Height, prepared.Value.Atlas.Pixels)
        });

        Console.WriteLine($"rendered on adapter: {frame.AdapterName}");

        if (Environment.GetEnvironmentVariable("MC_DEBUG_SPRITES") is "1")
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

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "minecraftcsharp-frame.png");
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
}
