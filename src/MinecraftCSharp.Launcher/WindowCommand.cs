// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Auth;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.Network;
using MinecraftCSharp.Framework.Rendering;

namespace MinecraftCSharp.Launcher;

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

        var request = FrameCommand.BuildRequest(world, width, height, out var mesh);
        Console.WriteLine($"mesh: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var format = WindowedRenderer.Run(request, frames, visibleFor, index =>
        {
            if (index == 0)
            {
                Console.WriteLine("window: first frame presented");
            }
        });

        Console.WriteLine($"window closed after presenting frames; surface format {format}");

        // Swapchain textures are not guaranteed copyable, so the screenshot comes from the
        // offscreen path with the identical camera and geometry.
        return FrameCommand.RenderWorld(world, width, height);
    }

    private static async Task<ClientWorld> ServerWorldAsync(
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
                    world.AddChunk(parsed.Chunk);
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
