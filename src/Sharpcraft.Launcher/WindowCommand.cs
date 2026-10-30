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
    /// <summary>The block layout this run resolves against, which differs only for the demo layout.</summary>
    private static BlockStateLayout? CurrentLayout() =>
        Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 }
            ? FrameCommand.DemoLayout()
            : VanillaBlockRegistry.Layout;

    /// <summary>True when two atlases differ in size or in any sprite's rectangle.</summary>
    /// <param name="before">Previous atlas, or null.</param>
    /// <param name="after">New atlas.</param>
    private static bool LayoutChanged(SpriteAtlas? before, SpriteAtlas after)
    {
        if (before is null || before.Width != after.Width || before.Height != after.Height ||
            before.Sprites.Count != after.Sprites.Count)
        {
            return true;
        }

        foreach (var (location, rect) in after.Sprites)
        {
            if (!before.Sprites.TryGetValue(location, out var previous) || previous != rect)
            {
                return true;
            }
        }

        return false;
    }

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
        // One resource manager for the session, created before anything reads resources so the atlas, models and
        // blockstates see the same packs as the font and the language table.
        using var resources = FrameCommand.CreateResourceManager();

        (SpriteAtlas Atlas, BlockSpriteMap Map)? prepared = FrameCommand.PrepareSprites(
            Environment.GetEnvironmentVariable("MC_FRAME_LOCAL") is { Length: > 0 }
                ? FrameCommand.DemoLayout()
                : VanillaBlockRegistry.Layout,
            resources);

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
        var meshOrderSeen = 0;
        var meshOrderDistanceSum = 0f;
        var geometryEpoch = 0;
        Queue<(long Key, Sharpcraft.Client.Render.MeshData Mesh)>? replayQueue = null;

        // The block atlas the terrain samples. A reload replaces it; whether the geometry must be rebuilt depends on
        // whether the layout changed, so that comparison lives here where both atlases are known.
        var atlasSource = new Sharpcraft.Framework.Rendering.AtlasTextureSource();

        resources.AddListener("atlas", 30, manager =>
        {
            var atlasClock = System.Diagnostics.Stopwatch.StartNew();
            var candidate = FrameCommand.PrepareAtlas(manager);
            atlasClock.Stop();

            if (candidate is null)
            {
                Console.WriteLine("resource reload: the atlas could not be rebuilt; keeping the previous one");
                return;
            }

            var layoutChanged = LayoutChanged(prepared?.Atlas, candidate);

            // Only a layout change needs the models resolved again; a pure recolour keeps the previous mapping.
            var nextMap = layoutChanged
                ? FrameCommand.PrepareMap(candidate, CurrentLayout(), manager)
                : prepared?.Map;

            prepared = (candidate, nextMap!);
            workerPool.SetSprites(nextMap);

            if (layoutChanged)
            {
                meshCache.Clear();
                var passes = 0;

                while (passes < 64 && (meshCache.LastTotalChunks < world.LoadedChunkCount || meshCache.PendingChunks > 0))
                {
                    meshCache.QueueMissing(world);
                    meshCache.Build(world, nextMap!, Sharpcraft.Client.Render.MeshBuildPolicy.CatchUp);
                    passes++;

                    if (meshCache.PendingChunks > 0)
                    {
                        Thread.Sleep(2);
                    }
                }

                geometryEpoch++;
                replayQueue = new Queue<(long Key, Sharpcraft.Client.Render.MeshData Mesh)>(meshCache.SnapshotAll());
                Console.WriteLine(
                    $"resource reload: layout changed; meshes {meshCache.LastTotalChunks}/{world.LoadedChunkCount} " +
                    $"after {passes} pass(es), atlas {atlasClock.Elapsed.TotalMilliseconds:F1} ms");
            }
            else
            {
                Console.WriteLine($"resource reload: atlas refreshed in {atlasClock.Elapsed.TotalMilliseconds:F1} ms (models kept)");
            }

            atlasSource.Update(new TextureData(candidate.Width, candidate.Height, candidate.Pixels));
        });

        // A short synchronous warm-up gives the first frame something to show.
        meshCache.Build(world, prepared?.Map, Sharpcraft.Client.Render.MeshBuildPolicy.Interactive);
        meshCache.QueueMissing(world);

        var request = FrameCommand.BuildRequest(
            world,
            width,
            height,
            out var mesh,
            out var drawRanges,
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
        var language = FrameCommand.PrepareLanguage(resources);

        // Chat types decide how a player message is written, so they come from the same connection that carries the
        // messages (docs/decisions/0012-chat-type-registry.md). Without them the reference's own default shape is used.
        var chatTypes = new Sharpcraft.Common.Network.ChatTypeTable();

        if (live?.Registries is { } registries)
        {
            chatTypes.Load(registries);
            Console.WriteLine($"chat types: {chatTypes.Count} from the server");

            if (Environment.GetEnvironmentVariable("MC_DIAG") == "1")
            {
                Console.WriteLine("chat type table: " + string.Join(", ", chatTypes.Describe()));
            }
        }

        var session = InteractiveSession.Create(world, request, live, workerPool, language, chatTypes);

        // A reload rebuilds the stack and then tells the listeners, in order. Language first: everything that shows text
        // reads it, so it must be in place before anything else rebuilds.
        resources.AddListener("language", 10, manager => session.Language = FrameCommand.PrepareLanguage(manager));

        // Watching is an extension the reference does not have, so it only exists when the switch is on
        // (docs/decisions/0016-resource-pack-file-watch.md).
        using var packWatcher = Sharpcraft.Client.Assets.PackWatcher.ForEnvironment(
            FrameCommand.ResourcePackFolders(),
            () =>
            {
                if (resources.Reload())
                {
                    Console.WriteLine("resource reload: applied after a file change");
                }
            });

        if (Sharpcraft.Client.Assets.PackWatcher.IsEnabled)
        {
            Console.WriteLine($"resource watch: {packWatcher?.FolderCount ?? 0} folder(s)");
        }

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
        var uiFont = FrameCommand.PrepareFont(resources);
        session.UiFont = uiFont;
        // The tooltip's frame is two nine slice sprites; without them it falls back to a rectangle.
        var guiSprites = FrameCommand.PrepareGuiSprites(resources);

        var glyphTexture = uiFont is null
            ? null
            : new TextureData(uiFont.Sheet.Width, uiFont.Sheet.Height, uiFont.Sheet.Pixels);

        // The renderer uploads these once; a reload replaces the images, so it watches this source's version instead of
        // being rebuilt (docs/decisions/0016-resource-pack-file-watch.md).
        var uiTextures = new Sharpcraft.Framework.Rendering.UiTextureSource();

        // Font atlas and tooltip sprites reload together: they come from the same packs and a half updated pair would draw
        // text measured for one sheet against another.
        resources.AddListener("font", 20, manager =>
        {
            uiFont = FrameCommand.PrepareFont(manager);
            session.UiFont = uiFont;
            guiSprites = FrameCommand.PrepareGuiSprites(manager);

            var glyph = uiFont is null
                ? null
                : new TextureData(uiFont.Sheet.Width, uiFont.Sheet.Height, uiFont.Sheet.Pixels);

            uiTextures.Update(glyph, guiSprites?.Textures ?? []);
            Console.WriteLine("resource reload: interface font and sprites replaced");
        });

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

                // After a reset the renderer holds nothing, so every mesh is replayed once; the incremental queue would
                // only carry what changed since the last frame and the world would stay empty.
                // A full replay is spread over frames: sending every mesh at once is what produced the 160 ms spike, and a
                // partly refilled world is correct because the old geometry was already dropped.
                List<(long Key, Sharpcraft.Client.Render.MeshData Mesh)> drained;

                if (replayQueue is { Count: > 0 } pendingReplay)
                {
                    drained = new List<(long, Sharpcraft.Client.Render.MeshData)>(64);

                    while (drained.Count < 64 && pendingReplay.Count > 0)
                    {
                        drained.Add(pendingReplay.Dequeue());
                    }

                    if (pendingReplay.Count == 0)
                    {
                        replayQueue = null;
                    }
                }
                else
                {
                    drained = [.. meshCache.DrainUpdates()];
                }
                if (drained.Count == 0)
                {
                    return null;
                }

                if (meshOrderSeen < 64)
                {
                    var eye = session.Player.Position;

                    foreach (var (key, _) in drained)
                    {
                        if (meshOrderSeen >= 64)
                        {
                            break;
                        }

                        var (chunkX, chunkZ) = Sharpcraft.Client.World.ClientWorld.Unpack(key);
                        var dx = chunkX + 0.5f - (eye.X / 16f);
                        var dz = chunkZ + 0.5f - (eye.Z / 16f);
                        meshOrderDistanceSum += MathF.Sqrt((dx * dx) + (dz * dz));
                        meshOrderSeen++;
                    }

                    if (meshOrderSeen >= 64)
                    {
                        Console.WriteLine($"mesh order: first 64 average distance {meshOrderDistanceSum / 64f:F2} chunks");
                    }
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
            spriteTextures: guiSprites?.Textures,
            textureSource: uiTextures,
            atlasSource: atlasSource,
            geometryEpoch: () => geometryEpoch,
            uiGeometry: frame =>
            {
                // Mesh work is ordered by distance, so the pool needs to know where the player is each frame.
                var player = session.Player.Position;
                workerPool.SetViewOrigin(player.X / 16f, player.Z / 16f);

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
                    session.Title.Draw(uiGeometry, uiFont, canvasWidth, canvasHeight);

                    // The session needs the drawn height to know where the history sits, and the window layer owns the
                    // clipboard, so the two are handed over here rather than reached for from inside the session.
                    session.CanvasHeight = canvasHeight;

                    // An unattended run cannot move the pointer, so a fixed probe point can report what it resolves to.
                    var probe = Environment.GetEnvironmentVariable("MC_CHAT_PROBE");

                    if (probe is { Length: > 0 } && session.ChatTicks % 100 == 0)
                    {
                        var parts = probe.Split(',');

                        if (parts.Length == 2 &&
                            float.TryParse(parts[0], out var probeX) &&
                            float.TryParse(parts[1], out var probeY))
                        {
                            session.ProbeChat(probeX, probeY);
                        }
                    }

                    if (session.Chat.IsOpen || !frame.Input.MouseCaptured)
                    {
                        var pointer = frame.Input.CursorInCanvas;
                        var hover = session.ChatHoverAt(pointer.X, pointer.Y);

                        if (hover is not null)
                        {
                            Sharpcraft.Client.Ui.Tooltip.Draw(
                                uiGeometry, uiFont, hover, pointer.X, pointer.Y, canvasWidth, canvasHeight, guiSprites);
                        }
                        else if (session.ChatTagTipAt(pointer.X, pointer.Y) is { } tagTip)
                        {
                            // The indicator bar explains itself when hovered.
                            Sharpcraft.Client.Ui.Tooltip.Draw(
                                uiGeometry, uiFont, tagTip, pointer.X, pointer.Y, canvasWidth, canvasHeight, guiSprites);
                        }
                    }

                    // A fixed preview point, because an unattended run cannot move the pointer onto a hover target.
                    var preview = Environment.GetEnvironmentVariable("MC_TOOLTIP_PREVIEW");

                    if (preview is { Length: > 0 })
                    {
                        var previewParts = preview.Split(',');

                        if (previewParts.Length == 2 &&
                            float.TryParse(previewParts[0], out var previewX) &&
                            float.TryParse(previewParts[1], out var previewY))
                        {
                            Sharpcraft.Client.Ui.Tooltip.Draw(
                                uiGeometry,
                                uiFont,
                                [new Sharpcraft.Common.Network.TextRun(
                                    "Server message. Cannot be reported.",
                                    Sharpcraft.Common.Network.ComponentRuns.DefaultColour,
                                    false)],
                                previewX,
                                previewY,
                                canvasWidth,
                                canvasHeight,
                                guiSprites);
                        }
                    }

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
