// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;
using MinecraftCSharp.Auth;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.Imaging;
using MinecraftCSharp.Common.Identifiers;
using MinecraftCSharp.Common.Network;
using MinecraftCSharp.Framework.Rendering;

namespace MinecraftCSharp.Launcher;

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
                    world.AddChunk(parsed.Chunk);
                    return true;
                }).ConfigureAwait(false);

            Console.WriteLine($"chunks: {stats.ChunksReceived} ok, {stats.ChunksFailed} failed");
            if (world is null || world.LoadedChunkCount == 0)
            {
                throw new InvalidOperationException("no chunks were received, nothing to render");
            }

            Console.WriteLine($"adapter: {OffscreenRenderer.DescribeAdapter()}");

            var mesh = ChunkMesher.Mesh(world);

            Console.WriteLine($"mesh: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

            var bounds = WorldBounds(world);
            var centre = (bounds.Min + bounds.Max) / 2f;
            var radius = Vector3.Distance(bounds.Min, bounds.Max) / 2f;
            var (eye, target) = CameraMath.DefaultCamera(centre, radius);

            var request = new FrameRequest(
                width,
                height,
                mesh.Vertices,
                mesh.Indices,
                new Vector4(0.55f, 0.72f, 0.95f, 1f),
                eye,
                target);

            var frame = OffscreenRenderer.Render(request);
            Console.WriteLine($"rendered on adapter: {frame.AdapterName}");

            var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
            var path = Path.Combine(Path.GetTempPath(), "minecraftcsharp-frame.png");
            File.WriteAllBytes(path, png);
            return path;
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

    private static string RenderLocal(int width, int height)
    {
        var world = BuildDemoWorld();
        var mesh = ChunkMesher.Mesh(world);
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
            target));

        Console.WriteLine($"rendered on adapter: {frame.AdapterName}");
        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "minecraftcsharp-frame.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    private static (Vector3 Min, Vector3 Max) WorldBounds(ClientWorld world)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        // Sample the loaded chunks' extents; block-level bounds come with the renderer later.
        for (var chunkX = -64; chunkX <= 64; chunkX++)
        {
            for (var chunkZ = -64; chunkZ <= 64; chunkZ++)
            {
                var chunk = world.GetChunk(chunkX, chunkZ);
                if (chunk is null)
                {
                    continue;
                }

                var low = new Vector3(chunk.ChunkX * 16, chunk.MinSectionY * 16, chunk.ChunkZ * 16);
                var high = new Vector3(low.X + 16, (chunk.MaxSectionY + 1) * 16, low.Z + 16);
                min = Vector3.Min(min, low);
                max = Vector3.Max(max, high);
            }
        }

        return (min, max);
    }

    private static Func<int, (int SectionCount, int MinSectionY)> ResolveDimensionLayout(DynamicRegistries registries)
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
