// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Auth;
using Sharpcraft.Common.Registries;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Launcher;

/// <summary>
/// Opens a window and presents the collected world for a few seconds, then writes the same frame
/// as a PNG (docs/decisions/0004-window-layer.md).
/// </summary>
internal static class WindowCommand
{
    /// <summary>Runs the windowed presentation and returns the screenshot path.</summary>
    internal static async Task<string> RunAsync(
        string? address,
        HttpClient http,
        int width,
        int height,
        int collectSeconds,
        int frames,
        TimeSpan visibleFor,
        CancellationToken cancellationToken)
    {
        ClientWorld world;

        if (Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 })
        {
            world = FrameCommand.BuildDemoWorld();
        }
        else if (Environment.GetEnvironmentVariable("MC_FRAME_REPLAY") is { Length: > 0 } replayPath)
        {
            world = FrameCommand.LoadReplay(replayPath);
        }
        else if (address is not null)
        {
            world = await ServerWorldAsync(address, http, collectSeconds, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException("window needs an address, MC_FRAME_LOCAL=1 or MC_FRAME_REPLAY=<file>");
        }

        // The windowed path uses the same atlas and block layout as the offscreen path, otherwise the
        // window would show colour-only faces while the saved PNG is textured (docs/modules/render.md 6.1).
        var prepared = FrameCommand.PrepareSprites(Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 }
            ? FrameCommand.DemoLayout()
            : VanillaBlockRegistry.Layout);

        // One pool serves both decoding and meshing, sized from the machine
        // (docs/specs/network/play-movement.md 7.24).
        var workerPool = new ChunkWorkerPool(world, prepared?.Map);
        Console.WriteLine($"chunk workers: {workerPool.WorkerCount} (of {Environment.ProcessorCount} logical processors)");

        var meshCache = new ChunkMeshCache(workerPool);

        // A short synchronous warm-up gives the first frame something to show.
        meshCache.Build(world, prepared?.Map, maxChunksPerBuild: 8, maxMillisecondsPerBuild: 250);
        meshCache.QueueMissing(world);

        var request = FrameCommand.BuildRequest(
            world,
            width,
            height,
            out var mesh,
            prepared?.Map,
            prepared is null
                ? null
                : new TextureData(prepared.Value.Atlas.Width, prepared.Value.Atlas.Height, prepared.Value.Atlas.Pixels),
            cache: meshCache,
            meshSynchronously: false);
        Console.WriteLine($"mesh: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var selfResize = ParseSelfResize();

        // Interactive session: the player entity is advanced on a fixed tick while the camera follows its
        // eye, so the window shows a body that can actually be walked (docs/modules/input.md 6).
        // A real server keeps the session live while the window runs, so movement is actually sent and the
        // server's corrections come back through the same code path the walk command verified
        // (docs/specs/network/play-movement.md 6.5).
        LivePlaySession? live = null;
        if (address is not null &&
            Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is not { Length: > 0 } &&
            Environment.GetEnvironmentVariable("MC_FRAME_REPLAY") is not { Length: > 0 })
        {
            try
            {
                live = await LivePlaySession.ConnectAsync(address, http, cancellationToken).ConfigureAwait(false);
                Console.WriteLine("live: connected, movement will be sent while the window runs");
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                Console.WriteLine($"live: connection failed ({exception.Message}); running without movement");
            }
        }

        using var liveScope = live;
        var session = InteractiveSession.Create(world, request, live, workerPool);

        var format = WindowedRenderer.Run(
            request,
            frames,
            visibleFor,
            (index, window) =>
        {
            if (index == 0)
            {
                Console.WriteLine("window: first frame presented");
            }

            // Deterministic resize exercise (MC_WINDOW_SELFRESIZE=640x360): the renderer has to
            // rebuild the swapchain, the depth texture and the aspect-dependent vertices.
            if (selfResize is { } target && index == 30)
            {
                Console.WriteLine($"window: self-resize to {target.Width}x{target.Height}");
                window.Resize(target.Width, target.Height);
            }

            session.CaptureMouse(window, index == 0);
        },
            (window) => session.UpdateCamera(window),
            Environment.GetEnvironmentVariable("MC_NO_REBUILD") == "1" ? null : () =>
            {
                // Hand fresh work to the worker, then take whatever it finished. Both steps are cheap; the
                // meshing itself never happens on this thread.
                var changedBlocks = false;
                foreach (var key in world.TakeDirtyChunks())
                {
                    meshCache.Invalidate(key);
                    changedBlocks = true;
                }

                if (session.TakeGeometryDirty())
                {
                    meshCache.QueueMissing(world);
                }

                if (!meshCache.Drain() && !changedBlocks)
                {
                    return null;
                }

                return FrameCommand.BuildRequest(
                    world, width, height, out _, prepared?.Map, cache: meshCache, meshSynchronously: false);
            });

        Console.WriteLine($"window closed after presenting frames; surface format {format}");

        // Swapchain textures are not guaranteed copyable, so the screenshot comes from the
        // offscreen path with the identical camera and geometry.
        return FrameCommand.RenderWorld(
            world,
            width,
            height,
            Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 }
                ? FrameCommand.DemoLayout()
                : VanillaBlockRegistry.Layout);
    }

    private static (int Width, int Height)? ParseSelfResize()
    {
        if (Environment.GetEnvironmentVariable("MC_WINDOW_SELFRESIZE") is not { Length: > 0 } raw)
        {
            return null;
        }

        var parts = raw.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
        {
            return (width, height);
        }

        throw new InvalidOperationException($"MC_WINDOW_SELFRESIZE must look like 640x360, got '{raw}'");
    }

    internal static async Task<ClientWorld> ServerWorldAsync(
        string address,
        HttpClient http,
        int collectSeconds,
        CancellationToken cancellationToken)
    {
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

        if (!outcome.Success || connection is null)
        {
            throw new InvalidOperationException($"server refused the login: {outcome.Reason}");
        }

        using (connection)
        {
            var probe = await ConfigurationProbe.RunAsync(
                connection,
                TimeSpan.FromSeconds(20),
                cancellationToken).ConfigureAwait(false);

            if (!probe.ReachedPlay)
            {
                throw new InvalidOperationException($"did not reach play: {probe.Reason}");
            }

            Console.WriteLine("collecting chunks...");
            ClientWorld? world = null;
            var layout = FrameCommand.ResolveDimensionLayout(probe.Registries);

            var stats = await PlaySession.RunAsync(
                connection,
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
                throw new InvalidOperationException("no chunks were received, nothing to show");
            }

            return world;
        }
    }
}
