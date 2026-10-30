// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Auth;
using Sharpcraft.Common.Registries;
using Sharpcraft.Launcher.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.Ui;
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
        LivePlaySession? joinedEarly = null;

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
            var preparedWorld = await ServerWorldAsync(address, http, collectSeconds, cancellationToken).ConfigureAwait(false);
            world = preparedWorld.World;
            joinedEarly = preparedWorld.Live;
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
        var entityMesher = new EntityPlaceholderMesher();

        // Placeholder boxes sample one atlas cell and tint it, so no per entity texture lookup is needed.
        SpriteRect? entitySprite = prepared is null
            ? null
            : prepared.Value.Map.ForBlock(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "stone"));


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
        LivePlaySession? live = joinedEarly;
        if (live is null &&
            address is not null &&
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
        // Loaded before the session because chat arrives as translation keys and the session resolves them.
        var language = FrameCommand.PrepareLanguage();

        // Chat types decide how a player message is written, so they come from the same connection that carries the
        // messages (docs/decisions/0012-chat-type-registry.md). Without them the reference's own default shape is used.
        var chatTypes = new Sharpcraft.Common.Network.ChatTypeTable();

        if (live?.Registries is { } registries)
        {
            chatTypes.Load(registries);
            Console.WriteLine($"chat types: {chatTypes.Count} from the server");
        }

        var session = InteractiveSession.Create(world, request, live, workerPool, language, chatTypes);

        // The document comes from one managed location and nowhere else: no environment variable and no command
        // line option can point the client at an arbitrary file (docs/decisions/0010). Editing that file reloads
        // the interface while the game runs; a broken edit keeps the previous screen.
        var uiPath = UiDocumentFile.EnsureExists(HostPaths.DefaultUiDirectory(), UiScreenLayout.DefaultDocument);
        using var uiLoader = new UiDocumentLoader(uiPath);
        uiLoader.Start();

        // A document that is already broken at startup must not leave the screen blank: the built in screen is
        // parsed as the fallback, so the player sees something usable and the reason in the log.
        CompiledScreen? fallback = null;

        if (uiLoader.Current is null)
        {
            var root = UiMarkup.Parse(UiScreenLayout.DefaultDocument);
            fallback = new CompiledScreen(root, UiStyles.Compile(root));
            Console.WriteLine("ui: managed document unusable at startup; showing the built in screen");
        }

        var uiGeometry = new UiGeometry();
        var uiFont = FrameCommand.PrepareFont();
        session.UiFont = uiFont;
        var glyphTexture = uiFont is null
            ? null
            : new TextureData(uiFont.Sheet.Width, uiFont.Sheet.Height, uiFont.Sheet.Pixels);

        // Without an atlas there is no cell to sample, so entity boxes are simply not drawn.
        var entitySource = entitySprite is { } sprite
            ? new Func<DynamicGeometry>(() => entityMesher.Build(session.EntityStates(session.PartialTick), sprite))
            : null;

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
            geometryUpdates: () =>
            {
                // Hand fresh work to the pool, then return only the chunks that finished. The renderer keeps a
                // slice per chunk, so each arrival costs one small upload rather than a recomposition of the
                // whole world (docs/specs/network/play-movement.md 7.25).
                foreach (var key in world.TakeDirtyChunks())
                {
                    meshCache.Invalidate(key);
                }

                if (session.TakeGeometryDirty())
                {
                    meshCache.QueueMissing(world);
                }

                var drained = meshCache.DrainUpdates();
                if (drained.Count == 0)
                {
                    return null;
                }

                var updates = new List<(long Key, GeometryData Mesh)>(drained.Count);
                foreach (var (key, mesh) in drained)
                {
                    updates.Add((key, new GeometryData(mesh.Vertices, mesh.Indices)));
                }

                return updates;
            },
            expectedChunks: InteractiveSession.ExpectedChunkCount(),
            dynamicGeometry: entitySource,
            glyphTexture: glyphTexture,
            uiGeometry: frame =>
            {
                var screen = uiLoader.Current ?? fallback;

                if (screen is null)
                {
                    uiGeometry.Clear();
                    return uiGeometry;
                }

                var canvasWidth = frame.Width;
                var canvasHeight = frame.Height;
                UiScreenLayout.Build(screen.Root, screen.Styles, canvasWidth, canvasHeight, uiGeometry, uiFont);

                // The reference shows the list only while the key is held, so it is drawn from the current input
                // rather than unconditionally. It joins the same batch as the document, so the submission budget
                // does not grow with it.
                // Chat history sits above the hotbar and fades with age; opening the prompt pins it fully visible,
                // which is what the reference does so nothing disappears while it is being read.
                if (uiFont is not null)
                {
                    session.ChatLog.Draw(uiGeometry, uiFont, canvasHeight, session.ChatTicks, session.Chat.IsOpen);
                    session.ActionBar.Draw(uiGeometry, uiFont, canvasWidth, canvasHeight);

                    // The bar sits just right of the column, over the same rows the history occupies.
                    var chatBottom = canvasHeight - Sharpcraft.Client.Ui.ChatLog.BottomMargin;
                    var chatTop = chatBottom - (Sharpcraft.Client.Ui.ChatLog.LinesPerPage * Sharpcraft.Client.Ui.ChatLog.EntryHeight);

                    Sharpcraft.Client.Ui.ScrollBar.Draw(
                        uiGeometry,
                        Sharpcraft.Client.Ui.ChatLog.LeftInset + Sharpcraft.Client.Ui.ChatLog.ColumnWidth + 4f,
                        chatTop,
                        chatBottom,
                        session.ChatLog.Count,
                        Sharpcraft.Client.Ui.ChatLog.LinesPerPage,
                        session.ChatLog.Scroll.Offset,
                        pending: false);
                }

                if (session.Chat.IsOpen)
                {
                    ChatPrompt.Draw(uiGeometry, uiFont, canvasWidth, canvasHeight, session.Chat.Model, session.ChatElapsedMs);
                }

                if (KeyBindings.Default.IsDown(frame.Input, InputAction.PlayerList))
                {
                    PlayerListPanel.Draw(
                        session.PlayerList.Entries,
                        canvasWidth,
                        top: 10f,
                        slotWidth: 140f,
                        uiGeometry,
                        uiFont,
                        textScale: 1f);
                }

                return uiGeometry;
            });

        Console.WriteLine(
            $"pool: parsed {workerPool.ParsedOk} ok / {workerPool.ParsedFailed} failed, "
            + $"meshed {workerPool.MeshesBuilt} (empty {workerPool.MeshesEmpty}, missing chunk {workerPool.MeshesMissingChunk})");

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

    /// <summary>A collected world together with the connection it was collected over.</summary>
    /// <param name="World">The world holding the received chunks.</param>
    /// <param name="Live">Session that owns the already joined connection.</param>
    internal readonly record struct PreparedWorld(ClientWorld World, LivePlaySession Live);

    internal static async Task<PreparedWorld> ServerWorldAsync(
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

        {
            var probe = await ConfigurationProbe.RunAsync(
                connection,
                TimeSpan.FromSeconds(20),
                cancellationToken).ConfigureAwait(false);

            if (!probe.ReachedPlay)
            {
                throw new InvalidOperationException($"did not reach play: {probe.Reason}");
            }

            // Vanilla does not fill the whole view distance before showing the world, it waits for the ground
            // near the player and renders while the rest streams in. Nine chunks is a three by three patch, which
            // arrives in a fraction of a second and is enough for a first frame.
            var startChunks = Environment.GetEnvironmentVariable("MC_START_CHUNKS") is { } rawStart && int.TryParse(rawStart, out var parsedStart)
                ? parsedStart
                : 9;

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

                    // Keep going until the ground around the player exists, then let the window open.
                    return world.LoadedChunkCount < startChunks;
                }).ConfigureAwait(false);

            Console.WriteLine($"chunks: {stats.ChunksReceived} ok, {stats.ChunksFailed} failed");
            if (world is null || world.LoadedChunkCount == 0)
            {
                connection.Dispose();
                throw new InvalidOperationException("no chunks were received, nothing to show");
            }

            // Hand the joined connection to the session instead of dropping it and logging in again. Joining
            // twice made startup slower and showed a spurious join and leave to everyone else on the server.
            // The registries are the ones this very connection received during configuration, which is where the
            // chat type decorations live.
            return new PreparedWorld(world, LivePlaySession.Adopt(connection, cancellationToken, probe.Registries));
        }
    }
}
