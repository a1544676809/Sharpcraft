// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.AuthLib;
using Sharpcraft.Common.Registries;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.Input;
using Sharpcraft.Client.Ui;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Opens a window and presents the collected world for a few seconds, then writes the same frame
/// as a PNG (docs/decisions/0004-window-layer.md).
/// </summary>
internal static class WindowCommand
{
    /// <summary>Runs the windowed presentation and returns the screenshot path.</summary>
    /// <summary>The block layout this run resolves against, which differs only for the demo layout.</summary>
    private static BlockStateLayout? CurrentLayout() =>
        GameOptions.Get("MC_FRAME_LOCAL") is { Length: > 0 }
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
        TimeSpan? visibleFor,
        GameIdentity identity,
        string? gameDirectory,
        CancellationToken cancellationToken)
    {
        ClientWorld world;
        LivePlaySession? joinedEarly = null;

        if (GameOptions.Get("MC_FRAME_LOCAL") is { Length: > 0 })
        {
            world = FrameCommand.BuildDemoWorld();
        }
        else if (GameOptions.Get("MC_FRAME_WATER") == "1")
        {
            world = FrameCommand.BuildWaterWorld(VanillaBlockRegistry.Layout);
            Console.WriteLine("window: water demo scene selected");
        }
        else if (GameOptions.Get("MC_FRAME_REPLAY") is { Length: > 0 } replayPath)
        {
            world = FrameCommand.LoadReplay(replayPath);
        }
        else if (address is not null)
        {
            var preparedWorld = await ServerWorldAsync(address, http, collectSeconds, identity, cancellationToken).ConfigureAwait(false);
            world = preparedWorld.World;
            joinedEarly = preparedWorld.Live;
        }
        else
        {
            // Standalone start with no server argument: singleplayer/local as applicable. There is no
            // integrated server yet (AGENTS.md principle 4, open item), so a local demo world is shown
            // rather than failing: the client must still run without a launcher or an address.
            world = FrameCommand.BuildDemoWorld();
            Console.WriteLine("window: no server address; showing a local world");
        }

        // The windowed path uses the same atlas and block layout as the offscreen path, otherwise the
        // window would show colour-only faces while the saved PNG is textured (docs/modules/render.md 6.1).
        // One resource manager for the session, created before anything reads resources so the atlas, models and
        // blockstates see the same packs as the font and the language table.
        using var resources = FrameCommand.CreateResourceManager();

        (SpriteAtlas Atlas, BlockSpriteMap Map)? prepared = FrameCommand.PrepareSprites(
            GameOptions.Get("MC_FRAME_LOCAL") is { Length: > 0 }
                ? FrameCommand.DemoLayout()
                : VanillaBlockRegistry.Layout,
            resources);

        // One pool serves both decoding and meshing, sized from the machine
        // (docs/specs/network/play-movement.md 7.24).
        var entityMesher = new EntityPlaceholderMesher();
        var visibleEntities = new List<Sharpcraft.Client.World.EntityRenderState>();
        System.Numerics.Vector3 lastEye = default;
        System.Numerics.Vector3 lastTarget = default;
        var entityCullFrames = 0;

        // Placeholder boxes sample one atlas cell and tint it, so no per entity texture lookup is needed.
        SpriteRect? entitySprite = prepared is null
            ? null
            : prepared.Value.Map.ForBlock(new Sharpcraft.Common.Identifiers.Identifier("minecraft", "stone"));


        var workerPool = new ChunkWorkerPool(
            world,
            prepared?.Map,
            fifoMeshOrder: GameOptions.Get("MC_MESH_FIFO") == "1",
            mergeFaces: GameOptions.Get("MC_MESH_NOMERGE") != "1",
            ambientOcclusion: GameOptions.Get("MC_NO_AO") != "1");
        Console.WriteLine($"chunk workers: {workerPool.WorkerCount} (of {Environment.ProcessorCount} logical processors)");

        var meshCache = new ChunkMeshCache(workerPool);

        // The sinks live as long as the cache: it re-meshes chunks incrementally, and a sink disposed between frames would
        // silently swallow the faces meshed after that point.
        // Layer routing is not optional: a sprite with holes drawn in the solid pass renders those holes opaque black.
        // Cutout geometry is not split out: it is alpha tested by the terrain shader and therefore streams together with the
        // solid mesh, so chunks that arrive later still show their leaves. Only blending needs its own buffer.
        var layerTranslucent = new Sharpcraft.Client.Render.MeshBuffer(4096);

        // The cache meshes the solid layer, so it needs the tint too: without it grass tops stay grey while only the
        // cutout and translucent layers are tinted.
        var biomeTint = Sharpcraft.Client.Render.BiomeTint.Load(resources);
        meshCache.Layers = new Sharpcraft.Client.Render.LayerSinks(
            VanillaBlockRegistry.Layout, null, layerTranslucent, biomeTint);
        workerPool.Layers = meshCache.Layers;
        var meshOrderSeen = 0;
        var meshOrderDistanceSum = 0f;
        // Host side speed readout, only when the host asks for it (principle 13 keeps it out of the game).
        var speedOverlay = SpeedOverlay.Enabled
            ? new SpeedOverlay()
            : null;

        var geometryEpoch = 0;
        Queue<Sharpcraft.Client.Render.MeshedChunk>? replayQueue = null;
    var uploadQueue = new Queue<Sharpcraft.Client.Render.MeshedChunk>();
        long drainedTotal = 0;
        var renderFrames = 0;
        var streamDiag = GameOptions.Get("MC_DIAG") == "1";
        var diagClock = System.Diagnostics.Stopwatch.StartNew();

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
                replayQueue = new Queue<Sharpcraft.Client.Render.MeshedChunk>(meshCache.SnapshotAll());
                Console.WriteLine(
                    $"resource reload: layout changed; meshes {meshCache.LastTotalChunks}/{world.LoadedChunkCount} " +
                    $"after {passes} pass(es), atlas {atlasClock.Elapsed.TotalMilliseconds:F1} ms");
            }
            else
            {
                Console.WriteLine($"resource reload: atlas refreshed in {atlasClock.Elapsed.TotalMilliseconds:F1} ms (models kept)");
            }

            atlasSource.Update(new TextureData(candidate.Width, candidate.Height, candidate.Pixels) { Mips = Sharpcraft.Client.Assets.AtlasMipChain.Build(candidate.Pixels, candidate.Width, candidate.Height, [.. candidate.Sprites.Values]) });
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
                : FrameCommand.WithMips(prepared.Value.Atlas),
            tint: biomeTint,
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
            GameOptions.Get("MC_FRAME_LOCAL") is not { Length: > 0 } &&
            GameOptions.Get("MC_FRAME_REPLAY") is not { Length: > 0 })
        {
            try
            {
                live = await LivePlaySession.ConnectAsync(address, http, identity, cancellationToken).ConfigureAwait(false);
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

            // Per-biome block tint (docs/modules/weather-and-sky-color.md 6.1): rebuild the tint with the server's biome
            // registry and remesh so grass/leaves/water use the biome at each position.
            if (Sharpcraft.Common.World.BiomeRegistry.From(registries) is { } biomes)
            {
                biomeTint = biomeTint.WithBiomes(world.GetBiomeAt, biomes, blendRadius: 2);
                meshCache.Layers = new Sharpcraft.Client.Render.LayerSinks(VanillaBlockRegistry.Layout, null, layerTranslucent, biomeTint);
                workerPool.Layers = meshCache.Layers;
                meshCache.Clear();
                meshCache.QueueMissing(world);
                Console.WriteLine($"biomes: {biomes.Entries.Count} from the server; per-biome block tint enabled");
            }

            if (GameOptions.Get("MC_DIAG") == "1")
            {
                Console.WriteLine("chat type table: " + string.Join(", ", chatTypes.Describe()));
            }
        }

        var session = InteractiveSession.Create(world, request, live, workerPool, language, chatTypes);

        // Sky/fog use SKY_COLOR (biome sky colour x day/night x weather), so the live session needs the biome registry.
        if (live?.Registries is { } biomeRegistries && Sharpcraft.Common.World.BiomeRegistry.From(biomeRegistries) is { } sessionBiomes)
        {
            session.SetBiomes(sessionBiomes);
        }

        session.SetCloudTexture(Sharpcraft.Client.Assets.CloudTexture.Load(resources));

        // Cloud options mirror the reference: CloudStatus OFF/FAST/FANCY and the separate cloudRange (chunks).
        var cloudMode = (GameOptions.Get("MC_CLOUD_MODE") ?? "fancy").ToLowerInvariant();
        var cloudRange = GameOptions.Get("MC_CLOUD_RANGE") is { Length: > 0 } rangeText
            && int.TryParse(rangeText, out var parsedRange) ? parsedRange : 128;
        var cloudOit = GameOptions.Get("MC_CLOUD_OIT") is not "0";
        var cloudAdaptive = GameOptions.Get("MC_CLOUD_ADAPTIVE") == "1";
        var oitLowPrecision = GameOptions.Get("MC_OIT_LOW_PRECISION") == "1";
        var cloudDepth = GameOptions.Get("MC_CLOUD_DEPTH") == "1";
        var presentMode = GameOptions.Get("MC_PRESENT_MODE")?.ToLowerInvariant() switch
        {
            "immediate" => PresentMode.Immediate,
            "mailbox" => PresentMode.Mailbox,
            _ => PresentMode.Fifo
        };
        var skyDiscMode = (GameOptions.Get("MC_SKY_DISC_MODE") ?? "vanilla").ToLowerInvariant();
        var skyDiscVanilla = skyDiscMode != "fast" && skyDiscMode != "optimized";
        session.SetCloudOptions(
            enabled: cloudMode != "off",
            fancy: cloudMode != "fast" && cloudMode != "off",
            rangeChunks: cloudRange,
            useOit: cloudOit,
            adaptive: cloudAdaptive);

        // The gradient sky is static day blue; use a flat SKY_COLOR clear colour instead (vanilla sky is SKY_COLOR).
        // MC_SKY_GRADIENT=1 keeps the gradient pass for experimentation, default stays flat.
        var skyGradient = GameOptions.Get("MC_SKY_GRADIENT") == "1";
        request = request with { SkipGradientSky = !skyGradient };

        // Sun, moon, stars and the sunrise band: the window now has the celestial pass, so populate its draws/textures.
        request = FrameCommand.AttachCelestial(request, world, resources, session.CurrentSkyColour(),
            new System.Numerics.Vector3(session.CurrentFogColour().X, session.CurrentFogColour().Y, session.CurrentFogColour().Z));

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
        var uiPath = UiDocumentFile.EnsureExists(GamePaths.DefaultUiDirectory(), UiScreenLayout.DefaultDocument);
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
        var customSkinPath = GameOptions.Get("MC_PLAYER_SKIN");
        Sharpcraft.Framework.Rendering.TextureData? customSkin = null;
        if (customSkinPath is { Length: > 0 } && File.Exists(customSkinPath))
        {
            var skinImage = Sharpcraft.Common.Imaging.PngDecoder.Decode(File.ReadAllBytes(customSkinPath));
            customSkin = new Sharpcraft.Framework.Rendering.TextureData(skinImage.Width, skinImage.Height, skinImage.Pixels);
        }

        var playerSkin = customSkin ?? Sharpcraft.Client.Render.EntitySkinTexture.Load(resources);
        var entitySkinLibrary = Sharpcraft.Client.Render.EntitySkinLibrary.Load(resources, playerSkin);
        var entitySkinTextureSource = new Sharpcraft.Framework.Rendering.AtlasTextureSource();
        entitySkinTextureSource.Update(entitySkinLibrary.Atlas?.Texture ?? playerSkin);
        var itemModelResolver = new Sharpcraft.Client.Render.ItemModelResolver(resources);
        Sharpcraft.Client.Render.EntityModelResolver.ItemModels = itemModelResolver;
        var itemAtlas = Sharpcraft.Client.Render.ItemAtlas.Load(resources,
        [
            Sharpcraft.Client.Render.WitchModelDefinition.PotionTexture,
            Sharpcraft.Client.Render.SpecialItemModelDefinition.TridentTexture,
            Sharpcraft.Client.Render.SpecialItemModelDefinition.ShieldTexture,
            Sharpcraft.Client.Render.SpecialModelDefinitions.ChestTexture
        ]);
        var itemAtlasSource = new Sharpcraft.Framework.Rendering.AtlasTextureSource();
        itemAtlasSource.Update(itemAtlas.Texture);
        var entitySkinSprite = playerSkin is { } skin
            ? new Sharpcraft.Client.Assets.SpriteRect(0, 0, skin.Width, skin.Height, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f }
            : (Sharpcraft.Client.Assets.SpriteRect?)null;
        using var signatureKeyClient = new MinecraftServicesKeyClient(http);
        var signatureKeys = await signatureKeyClient.GetAsync(!identity.Offline, cancellationToken).ConfigureAwait(false);
        var localProfileId = Guid.TryParse(identity.Uuid, out var parsedProfileId) ? parsedProfileId : (Guid?)null;
        var playerSkins = new Sharpcraft.Client.Render.PlayerSkinCache(
            Sharpcraft.Client.Render.PlayerSkinCache.DefaultCacheDirectory(GameOptions.Get("MC_ASSETS_DIR")),
            (path, image) =>
            {
                if (entitySkinLibrary.TryAddTexture(path, image))
                {
                    entitySkinTextureSource.Update(entitySkinLibrary.Atlas?.Texture);
                }
            },
            signatureKeys: signatureKeys,
            localProfileId: localProfileId);
        session.PlayerSkinLookup = playerSkins.TryGet;
        var modelMesher = new Sharpcraft.Client.Render.EntityModelMesher();
        var droppedItemMesher = new Sharpcraft.Client.Render.ItemEntityMesher();
        var heldItemMesher = new Sharpcraft.Client.Render.ItemInHandMesher();
        var variantTable = Sharpcraft.Client.Render.EntityVariantTable.FromRegistries(live?.Registries);

        /// <summary>
        /// Adds the dropped-item layer after the entity layers. Both branches of the entity path funnel here so the drop
        /// geometry is produced by exactly one place, whichever entity skin path is active.
        /// </summary>
        IReadOnlyList<Sharpcraft.Framework.Rendering.EntityGeometryLayer>? AppendItemLayers(
            IReadOnlyList<Sharpcraft.Framework.Rendering.EntityGeometryLayer>? layers,
            IReadOnlyList<EntityRenderState> entities)
        {
            // Drops and held items both consume the shared item geometry; held items additionally need the poses the
            // entity layers were just built with, so this runs after BuildLayers.
            var extra = new List<Sharpcraft.Framework.Rendering.EntityGeometryLayer>(2);
            foreach (var dropLayer in droppedItemMesher.Build(entities, itemAtlas))
            {
                extra.Add(dropLayer);
            }

            foreach (var heldLayer in heldItemMesher.Build(entities, modelMesher.PreparedPoses, variantTable, itemAtlas))
            {
                extra.Add(heldLayer);
            }

            if (extra.Count == 0)
            {
                return layers;
            }

            var combined = layers is null
                ? new List<Sharpcraft.Framework.Rendering.EntityGeometryLayer>(extra.Count)
                : [.. layers];
            combined.AddRange(extra);
            return combined;
        }

        void EnsureItemTextures(IReadOnlyList<EntityRenderState> entities)
        {
            static void AddIfPresent(List<string> target, string? path)
            {
                if (path is not null)
                {
                    target.Add(path);
                }
            }

            foreach (var entity in entities)
            {
                List<string> paths = [];
                AddIfPresent(paths, EntityModelResolver.ResolveHeldItemTexture(entity, offhand: false));
                AddIfPresent(paths, EntityModelResolver.ResolveHeldItemTexture(entity, offhand: true));
                AddIfPresent(paths, EntityModelResolver.ResolveHeldItemSpecialTexture(entity, offhand: false));
                AddIfPresent(paths, EntityModelResolver.ResolveHeldItemSpecialTexture(entity, offhand: true));
                paths.AddRange(Sharpcraft.Client.Render.ItemEntityMesher.ResolveTextures(entity));
                paths.AddRange(Sharpcraft.Client.Render.ItemInHandMesher.ResolveTextures(entity));
                AddIfPresent(paths, EntityModelResolver.ResolveHeldItemPotSideTexture(entity, offhand: false));
                AddIfPresent(paths, EntityModelResolver.ResolveHeldItemPotSideTexture(entity, offhand: true));
                paths.AddRange(EntityModelResolver.ResolveHeldItemPatternSprites(entity, offhand: false));
                paths.AddRange(EntityModelResolver.ResolveHeldItemPatternSprites(entity, offhand: true));
                foreach (var path in paths)
                {
                    if (path is null || itemAtlas.Contains(path))
                    {
                        continue;
                    }

                    var bytes = resources.Read(
                        Sharpcraft.Assets.ResourceKind.Texture,
                        new Sharpcraft.Common.Identifiers.Identifier("minecraft", path));
                    if (bytes is null)
                    {
                        continue;
                    }

                    var image = Sharpcraft.Common.Imaging.PngDecoder.Decode(bytes);
                    itemAtlas = itemAtlas.WithTexture(path, new TextureData(image.Width, image.Height, image.Pixels));
                    itemAtlasSource.Update(itemAtlas.Texture);
                }
            }
        }

        string LocalizeScreenshot(string key, string argument)
        {
            // The reference posts Component.translatable("screenshot.success"/"screenshot.failure", ...), so the text
            // comes from the loaded language table rather than a literal (docs/specs/render/screenshot.md 2.2).
            return session.Language.Format(key, [argument]);
        }
        var entitySource = entitySprite is { } sprite
            ? new Func<IReadOnlyList<Sharpcraft.Framework.Rendering.EntityGeometryLayer>?>(() =>
            {
                var states = session.EntityStates(session.PartialTick);
                visibleEntities.Clear();
                var frustum = Sharpcraft.Client.Render.ViewFrustum.FromViewProjection(
                    Sharpcraft.Framework.Rendering.CameraMath.BuildViewProjection(lastEye, lastTarget, 70f, 960, 540));
                foreach (var entity in states)
                {
                    var isPlayer = entity.TypeId == EntityPlaceholderMesher.PlayerEntityTypeId;
                    var width = isPlayer ? EntityPlaceholderMesher.PlayerWidth : EntityPlaceholderMesher.MobWidth;
                    var height = isPlayer ? EntityPlaceholderMesher.PlayerHeight : EntityPlaceholderMesher.MobHeight;
                    var centre = new System.Numerics.Vector3(entity.X, entity.Y + (height * 0.5f), entity.Z);
                    if (frustum.IsBoxVisible(centre, new System.Numerics.Vector3(width * 0.5f, height * 0.5f, width * 0.5f)))
                    {
                        visibleEntities.Add(entity);
                    }
                }

                if (streamDiag && (++entityCullFrames % 120) == 0)
                {
                    Console.WriteLine($"entity cull: candidates={states.Length} visible={visibleEntities.Count} culled={states.Length - visibleEntities.Count}");
                }

                EnsureItemTextures(visibleEntities);

                if (entitySkinLibrary.Atlas is { } entitySkinAtlas)
                {
                    var atlasSprite = new Sharpcraft.Client.Assets.SpriteRect(0, 0, entitySkinAtlas.Texture.Width, entitySkinAtlas.Texture.Height, 1) { U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
                    return AppendItemLayers(
                        modelMesher.BuildLayers(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(visibleEntities), atlasSprite, 64, 64, skinAtlas: entitySkinAtlas, variants: variantTable, itemAtlas: itemAtlas),
                        visibleEntities);
                }

                return entitySkinSprite is { } skinModel
                    ? AppendItemLayers(
                        modelMesher.BuildLayers(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(visibleEntities), skinModel, skinModel.Width == 1600 ? 64 : skinModel.Width, skinModel.Height == 1600 ? 64 : skinModel.Height, variants: variantTable, itemAtlas: itemAtlas),
                        visibleEntities)
                    : null;
            })
            : null;

        // A local demo scene has no player to steer, so the fixed camera from the request is used instead of the
        // one that follows the session player (whose position is meaningless without a server).
        var hasLocalScene =
            GameOptions.Get("MC_FRAME_LOCAL") is { Length: > 0 } ||
            GameOptions.Get("MC_FRAME_WATER") == "1" ||
            GameOptions.Get("MC_FRAME_REPLAY") is { Length: > 0 };

        // Host-only light probe (MC_LIGHT_PROBE=1): prints the sky/block light the mesher will read around the player, so
        // a too-dark scene can be told apart from a lighting-model problem. Principle 13: parsed here, in the host.
        var lightProbeTick = 0;
        var lightProbe = GameOptions.Get("MC_LIGHT_PROBE") == "1";
        var frameClock = System.Diagnostics.Stopwatch.StartNew();
        var previousFrameMs = 0.0;
        var removedChunks = new List<long>();
        long celestialCacheKey = long.MinValue;
        FrameRequest? celestialCached = null;

        var format = WindowedRenderer.Run(
            request,
            frames,
            visibleFor,

            (index, window) =>
        {
            var nowMs = frameClock.Elapsed.TotalMilliseconds;
            if (index > 0)
            {
                session.ObserveFrame(nowMs - previousFrameMs);
            }

            previousFrameMs = nowMs;

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

            // Apply skin arrivals between frames: the renderer checks the texture version before it builds the next
            // frame's entity geometry, so the atlas and its sprite rects change together.
            playerSkins.Sync(session.PlayerList.Entries);
            if (playerSkins.Pump() > 0)
            {
                entitySkinTextureSource.Update(entitySkinLibrary.Atlas?.Texture);
            }
        },
            hasLocalScene
                ? null
                : (window) =>
                {
                    var camera = session.UpdateCamera(window);
                    lastEye = camera.Eye;
                    lastTarget = camera.Target;
                    speedOverlay?.Observe(session.Player.Position, session.TickIndex);

                    if (lightProbe && (lightProbeTick++ % 90) == 0)
                    {
                        var lp = session.Player.Position;
                        var px = (int)Math.Floor(lp.X);
                        var pz = (int)Math.Floor(lp.Z);
                        var py = (int)Math.Floor(lp.Y);
                        Console.WriteLine($"[light] player=({lp.X:F1},{lp.Y:F1},{lp.Z:F1}) chunks={world.LoadedChunkCount}");

                        for (var dy = -12; dy <= 4; dy++)
                        {
                            var st = world.GetBlockState(px, py + dy, pz);
                            world.TryGetLight(px, py + dy, pz, out var sky, out var block);
                            var lm = Sharpcraft.Client.Render.VanillaLightmap.Colour(sky, block);
                            Console.WriteLine($"[light] y{py + dy} state={st} sky={sky} block={block} lm=({lm.X:F2},{lm.Y:F2},{lm.Z:F2})");
                        }
                    }

                    return camera;
                },
            geometryUpdates: () =>
            {
                removedChunks.Clear();
                var player = session.Player.Position;
                var playerChunkX = (int)MathF.Floor(player.X / 16f);
                var playerChunkZ = (int)MathF.Floor(player.Z / 16f);
                removedChunks.AddRange(world.RemoveChunksOutside(
                    playerChunkX,
                    playerChunkZ,
                    InteractiveSession.ViewDistanceChunks + 2));
                foreach (var removed in removedChunks)
                {
                    meshCache.Remove(removed);
                }

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
                List<Sharpcraft.Client.Render.MeshedChunk> drained;

                if (replayQueue is { Count: > 0 } pendingReplay)
                {
                    drained = new List<Sharpcraft.Client.Render.MeshedChunk>(64);

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
                drainedTotal += drained.Count;

                if (streamDiag && (++renderFrames % 60) == 0)
                {
                    Console.WriteLine(
                        $"streamrender: world={world.LoadedChunkCount} arrived={session.ChunksInstalled} arrivalLast={session.LastInstallSeconds:F1} " + $"meshes={meshCache.LastTotalChunks} "
                        + $"drained={drainedTotal} thisFrame={drained.Count} "
                        + $"built={workerPool.MeshesBuilt} empty={workerPool.MeshesEmpty} missing={workerPool.MeshesMissingChunk} " + $"t={diagClock.Elapsed.TotalSeconds:F1}s "
                        + $"meshPending={workerPool.MeshesPending} ready={workerPool.MeshesReady} frame={renderFrames}");
                }

                if (drained.Count == 0)
                {
                    return null;
                }

                if (meshOrderSeen < 64)
                {
                    var eye = session.Player.Position;

                    foreach (var entry in drained)
                    {
                        var key = entry.Key;
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

                // A whole world arriving at once used to mean one frame with hundreds of megabytes of uploads, which
                // showed up as a 100 ms plus frame. The budget spreads it: what does not fit waits for the next frame,
                // and a chunk may briefly show only its opaque layer, which is the recorded degradation.
                const long UploadBudgetBytes = 8L * 1024 * 1024;
                var updates = new List<(long Key, GeometryData Mesh)>(drained.Count);
                long queuedBytes = 0;

                foreach (var entry in uploadQueue)
                {
                    queuedBytes += entry.Opaque.Vertices.LongLength * sizeof(float) + (entry.Cutout?.Vertices.LongLength ?? 0) * sizeof(float);
                }

                var uploads = new List<Sharpcraft.Client.Render.MeshedChunk>(drained.Count + uploadQueue.Count);
                uploads.AddRange(uploadQueue);
                uploads.AddRange(drained);
                uploadQueue.Clear();

                long uploadedThisFrame = 0;

                foreach (var entry in uploads)
                {
                    var bytes = (entry.Opaque.Vertices.LongLength + (entry.Cutout?.Vertices.LongLength ?? 0) + (entry.Translucent?.Vertices.LongLength ?? 0)) * sizeof(float);

                    if (updates.Count > 0 && uploadedThisFrame + bytes > UploadBudgetBytes)
                    {
                        uploadQueue.Enqueue(entry);
                        continue;
                    }

                    uploadedThisFrame += bytes;
                    updates.Add((entry.Key, new GeometryData(
                        entry.Opaque.Vertices,
                        entry.Opaque.Indices,
                        entry.Cutout?.Vertices,
                        entry.Cutout?.Indices,
                        entry.Translucent?.Vertices,
                        entry.Translucent?.Indices)));
                }

                return updates;
            },
            expectedChunks: InteractiveSession.ExpectedChunkCount(),
            dynamicGeometry: null,
            entityLayers: entitySource,
            glyphTexture: glyphTexture,
            entitySkinSource: entitySkinTextureSource,
            itemAtlasSource: itemAtlasSource,
            spriteTextures: guiSprites?.Textures,
            textureSource: uiTextures,
            atlasSource: atlasSource,
            geometryEpoch: () => geometryEpoch,
            diagnostics: GameOptions.ForFrame(),
            lightmapPerFrame: () => session.CurrentLightmap(),
            fogPerFrame: () => session.CurrentFogColour(),
            clearColourPerFrame: () => session.CurrentFogColour(),
            cloudsPerFrame: () => session.CurrentCloudDraw(),
            cloudOitEnabled: session.CloudUseOit,
            oitLowPrecision: oitLowPrecision,
            cloudDepthEnabled: cloudDepth,
            presentMode: BackendConvert.ToWgpu(presentMode),
            screenshotRequest: window => window.Input.JustPressed(Sharpcraft.Framework.Platform.InputKey.F2)
                ? ScreenshotPaths.Next(gameDirectory ?? GamePaths.DefaultGameDirectory())
                : ScreenshotBridge.Take(),
            screenshotCaptured: (path, pixels, width, height) =>
            {
                try
                {
                    var directory = System.IO.Path.GetDirectoryName(path);
                    if (directory is { Length: > 0 })
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }

                    System.IO.File.WriteAllBytes(path, Sharpcraft.Common.Imaging.PngEncoder.EncodeRgba((int)width, (int)height, pixels));
                    session.ChatLog.Add(LocalizeScreenshot("screenshot.success", System.IO.Path.GetFileName(path)), session.ChatTicks, Sharpcraft.Client.Ui.ChatLog.MessageTag.System);
                }
                catch (Exception error)
                {
                    session.ChatLog.Add(LocalizeScreenshot("screenshot.failure", error.Message), session.ChatTicks, Sharpcraft.Client.Ui.ChatLog.MessageTag.System);
                }
            },
            skyDiscVanilla: skyDiscVanilla,
            skyDiscPerFrame: () =>
            {
                var fog = session.CurrentFogColour();
                return (session.CurrentSkyColour(),
                    new System.Numerics.Vector3(fog.X, fog.Y, fog.Z),
                    MathF.Min(InteractiveSession.ViewDistanceChunks * 16f, 512f), session.Player.Position + new System.Numerics.Vector3(0f, 1.62f, 0f));
            },
            celestialPerFrame: () =>
            {
                if (world is null)
                {
                    return null;
                }

                var rainKey = (long)(FrameCommand.EffectiveRainLevel(world, 0f) * 100f);
                var thunderKey = (long)(FrameCommand.EffectiveThunderLevel(world, 0f) * 100f);
                var key = FrameCommand.EffectiveDayTime(world) ^ (rainKey << 32) ^ (thunderKey << 24);
                if (celestialCached is null || key != celestialCacheKey)
                {
                    celestialCached = FrameCommand.AttachCelestial(request, world, resources, session.CurrentSkyColour(),
                        new System.Numerics.Vector3(session.CurrentFogColour().X, session.CurrentFogColour().Y, session.CurrentFogColour().Z));
                    celestialCacheKey = key;
                }

                return (celestialCached.CelestialDraws ?? [], celestialCached.CelestialColorDraws ?? [], celestialCached.SkyDiscDraws ?? []);
            },
            geometryRemovals: () => removedChunks,
            worldTextPerFrame: () =>
            {
                if (uiFont is null)
                {
                    return null;
                }

                var forward = lastTarget - lastEye;
                if (forward.LengthSquared() < 1e-8f)
                {
                    forward = -System.Numerics.Vector3.UnitZ;
                }

                forward = System.Numerics.Vector3.Normalize(forward);
                var right = System.Numerics.Vector3.Cross(forward, System.Numerics.Vector3.UnitY);
                if (right.LengthSquared() < 1e-8f)
                {
                    right = System.Numerics.Vector3.UnitX;
                }

                right = System.Numerics.Vector3.Normalize(right);
                var up = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(right, forward));
                var draws = new List<Sharpcraft.Framework.Rendering.WorldTextDraw>();
                foreach (var entity in session.EntityStates(session.PartialTick))
                {
                    if (session.EntityDisplayName(entity.Id) is not { Length: > 0 } name)
                    {
                        continue;
                    }

                    var height = entity.TypeId == EntityPlaceholderMesher.PlayerEntityTypeId
                        ? EntityPlaceholderMesher.PlayerHeight
                        : EntityPlaceholderMesher.MobHeight;
                    var anchorPoint = new System.Numerics.Vector3(
                        entity.X,
                        entity.Y + height + Sharpcraft.Client.Render.NameplateProjection.HeadOffset,
                        entity.Z);
                    draws.AddRange(Sharpcraft.Client.Render.WorldTextMeshBuilder.BuildNameplate(
                        name,
                        uiFont,
                        anchorPoint,
                        right,
                        up,
                        0x80FFFFFFu,
                        0x40000000u));
                }

                return draws;
            },            uiGeometry: frame =>            {
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
                    speedOverlay?.Draw(uiGeometry, uiFont, canvasHeight);

                    // The session needs the drawn height to know where the history sits, and the window layer owns the
                    // clipboard, so the two are handed over here rather than reached for from inside the session.
                    session.CanvasHeight = canvasHeight;

                    // An unattended run cannot move the pointer, so a fixed probe point can report what it resolves to.
                    var probe = GameOptions.Get("MC_CHAT_PROBE");

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
                    var preview = GameOptions.Get("MC_TOOLTIP_PREVIEW");

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
            GameOptions.Get("MC_FRAME_LOCAL") is { Length: > 0 }
                ? FrameCommand.DemoLayout()
                : VanillaBlockRegistry.Layout);
    }

    private static (int Width, int Height)? ParseSelfResize()
    {
        if (GameOptions.Get("MC_WINDOW_SELFRESIZE") is not { Length: > 0 } raw)
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
    internal readonly record struct PreparedWorld(ClientWorld World, LivePlaySession? Live);

    internal static async Task<PreparedWorld> ServerWorldAsync(
        string address,
        HttpClient http,
        int collectSeconds,
        GameIdentity identity,
        CancellationToken cancellationToken)
    {
        var (outcome, connection) = await GameConnector
            .LoginAsync(address, http, identity, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Success || connection is null)
        {
            throw new InvalidOperationException($"server refused the login: {outcome.Reason}");
        }

        {
            var probe = await ConfigurationProbe.RunAsync(
                connection,
                TimeSpan.FromSeconds(20),
                cancellationToken,
                log: GameOptions.Get("MC_PACKET_LOG") == "1" ? Console.WriteLine : null).ConfigureAwait(false);

            if (!probe.ReachedPlay)
            {
                throw new InvalidOperationException($"did not reach play: {probe.Reason}");
            }

            // Vanilla does not fill the whole view distance before showing the world, it waits for the ground
            // near the player and renders while the rest streams in. Nine chunks is a three by three patch, which
            // arrives in a fraction of a second and is enough for a first frame.
            var startChunks = GameOptions.Get("MC_START_CHUNKS") is { } rawStart && int.TryParse(rawStart, out var parsedStart)
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
                // The preload only saves a moment: a server still generating its spawn area can send nothing
                // for a while. Open the window on an empty world and let the live session stream it in.
                Console.WriteLine("no chunks preloaded yet; opening on an empty world and streaming as they arrive");
                var fallback = stats.JoinGame is { } joined ? layout(joined.DimensionTypeId) : (SectionCount: 24, MinSectionY: -4);
                world ??= new ClientWorld(fallback.MinSectionY, fallback.SectionCount);

                // A connection whose preload saw no chunks is not worth reusing: open a fresh session so the join runs
                // from the start instead of inheriting a half finished one.
                connection.Dispose();
                return new PreparedWorld(world, null);
            }
            // Hand the joined connection to the session instead of dropping it and logging in again. Joining
            // twice made startup slower and showed a spurious join and leave to everyone else on the server.
            // The registries are the ones this very connection received during configuration, which is where the
            // chat type decorations live.
            return new PreparedWorld(world, LivePlaySession.Adopt(connection, cancellationToken, probe.Registries, stats.DeferredPackets));
        }
    }
}
