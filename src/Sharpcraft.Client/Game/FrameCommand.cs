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
internal static class FrameCommand
{
    // Decoded celestial sprites are reused across frames: the window rebuilds the sun/moon/star geometry per frame but
    // must not re-read and re-decode the PNGs each time.
    private static readonly Dictionary<Sharpcraft.Common.Identifiers.Identifier, TextureData> CelestialTextureCache = [];

    /// <summary>Prefix of the item tag data extracted for the client's tag registry.</summary>
    internal const string ItemTagsPrefix = "data/minecraft/tags/item/";

    // Host diagnostics: print the rebuilt celestial state only when MC_DIAG is on (rebuilds now run every frame).
    private static bool LogCelestial => GameOptions.Flag("MC_DIAG");

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
    /// Builds the sprite atlas and state mapping for a frame. The client owns stitching (ADR-0007);
    /// the launcher only supplies the pack path it already manages.
    /// </summary>
    /// <param name="layout">Block state layout, or null when it is not known yet.</param>
    /// <returns>Atlas plus mapping, or null when no pack is installed.</returns>
    /// <summary>Loads a font definition referenced from another one.</summary>
    /// <param name="stack">Asset stack to read from.</param>
    /// <param name="reference">Identifier such as <c>minecraft:include/space</c>.</param>
    private static string? ReadFontReference(
        Sharpcraft.Assets.ResourceManager resources,
        string reference)
    {
        var colon = reference.IndexOf(':');

        if (colon <= 0 || colon == reference.Length - 1)
        {
            return null;
        }

        var bytes = resources.Read(
            ResourceKind.Font,
            new Sharpcraft.Common.Identifiers.Identifier(reference[..colon], reference[(colon + 1)..]));

        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Loads the translation table the server's messages are written against.
    /// </summary>
    /// <remarks>
    /// The language file is an asset like any other: it is extracted from the game by the launcher and never stored in
    /// the repository. Without it the interface can only show translation keys, which is what it did before this.
    /// </remarks>
    /// <summary>
    /// Loads the message table from the shared resource manager.
    /// </summary>
    /// <param name="resources">Session resource manager.</param>
    internal static Sharpcraft.Common.Localization.LanguageTable PrepareLanguage(
        Sharpcraft.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        // The interface follows the machine's language, the way the reference does: on a Chinese machine the same
        // message must not appear in English.
        // MC_LANGUAGE pins the language for a run, which is how the Chinese path can be verified on an English
        // machine; otherwise the machine's own language is used, as the reference does.
        var forced = GameOptions.Get("MC_LANGUAGE");
        var candidates = Sharpcraft.Common.Localization.LocaleSelector.Candidates(
            string.IsNullOrWhiteSpace(forced)
                ? System.Globalization.CultureInfo.CurrentUICulture.Name
                : forced);

        byte[]? bytes = null;
        string? chosen = null;

        foreach (var candidate in candidates)
        {
            bytes = resources.Read(
                ResourceKind.Language,
                new Sharpcraft.Common.Identifiers.Identifier("minecraft", candidate));

            if (bytes is not null)
            {
                chosen = candidate;
                break;
            }
        }

        if (bytes is null)
        {
            Console.WriteLine($"language: none of {string.Join(", ", candidates)} is in the pack; messages will show keys");
            return Sharpcraft.Common.Localization.LanguageTable.Empty;
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        var table = Sharpcraft.Common.Localization.LanguageTable.Parse(
            System.Text.Encoding.UTF8.GetString(bytes));
        started.Stop();

        Console.WriteLine($"language: {chosen} ({table.Count} keys), {started.ElapsedMilliseconds} ms");
        return table;
    }

    /// <summary>
    /// Loads the glyph sheet from the same asset pack the block textures come from, so text uses the game's own
    /// font without any asset entering the repository (docs/modules/ui.md 5).
    /// </summary>
    /// <summary>
    /// Builds the session resource manager: base pack, then every installed pack above it.
    /// </summary>
    /// <remarks>
    /// One manager per session, as the reference keeps one resource manager. Packs are discovered once here, so the font,
    /// the tooltip sprites and the language table all see the same overrides and a reload has a single place to rebuild.
    /// </remarks>
    internal static Sharpcraft.Assets.ResourceManager CreateResourceManager()
    {
        var (stack, count) = BuildStack();

        InstallItemTags();

        if (count > 0)
        {
            Console.WriteLine($"resource packs: {count} installed");
        }

        // The factory lets a reload rebuild the same stack later without any loader knowing how it was built.
        return new Sharpcraft.Assets.ResourceManager(stack, count, BuildStack);
    }

    /// <summary>The pack folders named by <c>MC_RESOURCE_PACKS</c>, split and trimmed.</summary>
    internal static IReadOnlyList<string> ResourcePackFolders()
    {
        var configured = GameOptions.Get("MC_RESOURCE_PACKS");

        if (string.IsNullOrWhiteSpace(configured))
        {
            return [];
        }

        var folders = new List<string>();

        foreach (var folder in configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.IO.Directory.Exists(folder))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }

    /// <summary>
    /// The pack format this client speaks, from <c>MC_PACK_FORMAT</c> or the snapshot default.
    /// </summary>
    /// <remarks>
    /// The baseline is the 26.3 release, whose resource format is 97.1 (Minecraft Wiki, Pack format). The folder name on a
    /// machine may still say "snapshot 8" from before an upgrade, so the number must not be inferred from a path. The value
    /// is overridable because a wrong number here would reject packs that are in fact fine.
    /// </remarks>
    private static Sharpcraft.Assets.PackFormatVersion ResolveClientPackFormat()
    {
        var configured = GameOptions.Get("MC_PACK_FORMAT");

        if (Sharpcraft.Assets.PackFormatVersion.TryParse(configured, out var parsed))
        {
            return parsed;
        }

        return new Sharpcraft.Assets.PackFormatVersion(97, 1);
    }

    /// <summary>Drops a trailing <c>.png</c> from a provider's file name.</summary>
    /// <param name="path">Path such as <c>font/ascii.png</c>.</param>
    private static string StripTextureExtension(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;

    /// <summary>Builds a stack from the base pack, the external asset index, then every installed pack.</summary>
    /// <returns>The stack and how many installed packs it carries.</returns>
    /// <remarks>
    /// Layering matches vanilla (ADR-0027): builtin jar pack (bottom), the read-only external asset index as an
    /// overlay (audio + every language), then the user resource packs (top). A missing external index degrades to the
    /// jar pack's <c>en_us</c> instead of failing.
    /// </remarks>
    private static (AssetStack Stack, int PackCount) BuildStack()
    {
        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new BuiltInFallbackSource());

        var packPath = FindPack();

        if (packPath is not null)
        {
            stack.Add(new ZipAssetSource(packPath, "base-pack"));
        }

        AddIndexedAssets(stack);

        var count = 0;
        var clientFormat = ResolveClientPackFormat();

        foreach (var folder in ResourcePackFolders())
        {
            foreach (var pack in Sharpcraft.Assets.ResourcePackDiscovery.Discover(folder, clientFormat))
            {
                stack.Add(new Sharpcraft.Assets.ResourcePackSource(pack));
                count++;
            }
        }

        return (stack, count);
    }

    /// <summary>
    /// Adds the external asset index as a read-only overlay above the jar pack, when the host supplied
    /// <c>--assetsDir</c> and <c>--assetIndex</c> (ADR-0027). Everything that reads through the stack, including the
    /// language loader, therefore sees non-<c>en_us</c> locales and audio without any copy into the repository.
    /// </summary>
    /// <param name="stack">Stack to extend; unchanged when the index is not configured or not present.</param>
    private static void AddIndexedAssets(AssetStack stack)
    {
        var assetsDirectory = GameOptions.Get("MC_ASSETS_DIR");
        var indexId = GameOptions.Get("MC_ASSET_INDEX");

        if (string.IsNullOrWhiteSpace(assetsDirectory) || string.IsNullOrWhiteSpace(indexId))
        {
            return;
        }

        var indexPath = Path.Combine(assetsDirectory, "indexes", indexId + ".json");
        if (!File.Exists(indexPath))
        {
            Console.WriteLine(
                $"[assets] external index {indexId} not found at {indexPath}; continuing with the jar pack only " +
                "(sounds and non-English languages unavailable)");
            return;
        }

        try
        {
            var source = IndexedAssetSource.Open(indexPath, indexId);
            stack.Add(source);
            Console.WriteLine($"[assets] external index {indexId}: {source.ObjectCount} objects (read-only overlay)");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Console.WriteLine(
                $"[assets] external index {indexId} could not be mounted ({exception.Message}); " +
                "continuing with the jar pack only");
        }
    }

    /// <summary>
    /// Loads the tooltip sprites and their nine slice border, or null when the pack or the files are missing.
    /// </summary>
    /// <remarks>
    /// The tooltip is a sprite pair, so without them the interface falls back to a plain rectangle. Reading the border
    /// from the <c>.mcmeta</c> rather than hard coding it keeps the drawing tied to the texture that will be used.
    /// </remarks>
    /// <summary>Reads one tooltip sprite's scaling from its <c>.mcmeta</c>, or a stretch when absent.</summary>
    /// <param name="resources">Session resource manager, so a pack may override the metadata too.</param>
    /// <param name="name">Sprite name, such as <c>frame</c>.</param>
    private static Sharpcraft.Client.Ui.GuiSpriteScaling ReadScaling(
        Sharpcraft.Assets.ResourceManager resources,
        string name)
    {
        using var stream = resources.OpenFile(
            $"assets/minecraft/textures/gui/sprites/tooltip/{name}.png.mcmeta");

        if (stream is null)
        {
            return new Sharpcraft.Client.Ui.GuiSpriteScaling();
        }

        using var reader = new StreamReader(stream);
        return Sharpcraft.Client.Ui.GuiSpriteScaling.TryParse(reader.ReadToEnd())
               ?? new Sharpcraft.Client.Ui.GuiSpriteScaling();
    }

    internal static Sharpcraft.Client.Ui.GuiSpriteAtlas? PrepareGuiSprites(
        Sharpcraft.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var background = resources.Read(
            ResourceKind.Texture,
            new Sharpcraft.Common.Identifiers.Identifier("minecraft", "gui/sprites/tooltip/background"));
        var frame = resources.Read(
            ResourceKind.Texture,
            new Sharpcraft.Common.Identifiers.Identifier("minecraft", "gui/sprites/tooltip/frame"));

        if (background is null || frame is null)
        {
            Console.WriteLine("tooltip: sprites missing from the pack; the tooltip falls back to a rectangle");
            return null;
        }

        // Two sprites, each with its own scaling: a pack may give them different borders or a plain stretch.
        var backgroundScaling = ReadScaling(resources, "background");
        var frameScaling = ReadScaling(resources, "frame");

        var backgroundImage = Sharpcraft.Common.Imaging.PngDecoder.Decode(background);
        var frameImage = Sharpcraft.Common.Imaging.PngDecoder.Decode(frame);

        var backgroundTexture = new Sharpcraft.Framework.Rendering.TextureData(
            backgroundImage.Width, backgroundImage.Height, backgroundImage.Pixels);
        var frameTexture = new Sharpcraft.Framework.Rendering.TextureData(
            frameImage.Width, frameImage.Height, frameImage.Pixels);

        return new Sharpcraft.Client.Ui.GuiSpriteAtlas(
            backgroundTexture, frameTexture, backgroundScaling, frameScaling);
    }

    internal static Sharpcraft.Client.Ui.BitmapFont? PrepareFont(
        Sharpcraft.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        // The pack declares the sheet, its grid and its metrics; assuming them is what made an HD pack overlap.
        var provider = Sharpcraft.Client.Assets.BitmapFontDefinition.Resolve(resources);

        var fallbackSheet = new Sharpcraft.Common.Identifiers.Identifier("minecraft", "font/ascii");

        // A provider names its file with the extension ("minecraft:font/ascii.png"), while a texture identifier must not
        // carry one because the kind supplies it; keeping it would look for ascii.png.png and lose the font entirely.
        var sheetId = provider is { } resolved
            ? new Sharpcraft.Common.Identifiers.Identifier(
                resolved.File.Namespace,
                StripTextureExtension(resolved.File.Path))
            : fallbackSheet;

        var bytes = resources.Read(ResourceKind.Texture, sheetId);

        if (bytes is null && sheetId != fallbackSheet)
        {
            Console.WriteLine($"font: {sheetId} is missing; falling back to the default sheet");
            bytes = resources.Read(ResourceKind.Texture, fallbackSheet);
        }

        if (bytes is null)
        {
            Console.WriteLine("font: the ascii sheet is missing from the pack; text uses placeholder bars");
            return null;
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        var image = Sharpcraft.Common.Imaging.PngDecoder.Decode(bytes);
        var font = Sharpcraft.Client.Ui.BitmapFont.FromSheet(
            image,
            provider?.Height ?? Sharpcraft.Client.Ui.BitmapFont.DefaultHeight,
            provider?.Ascent ?? Sharpcraft.Client.Ui.BitmapFont.DefaultAscent,
            provider?.Columns ?? Sharpcraft.Client.Ui.BitmapFont.SheetColumns);
        started.Stop();

        // Characters without ink, a space above all, take their advance from the font definition rather than from the
        // sheet. Without this a space advances a single pixel and every sentence loses its word breaks.
        var declared = 0;
        var definition = resources.Read(
            ResourceKind.Font,
            new Sharpcraft.Common.Identifiers.Identifier("minecraft", "default"));

        if (definition is not null)
        {
            declared = Sharpcraft.Client.Ui.FontDefinition.Apply(
                System.Text.Encoding.UTF8.GetString(definition),
                reference => ReadFontReference(resources, reference),
                font);
        }

        if (declared == 0)
        {
            // The definition is where the reference keeps this number; if it cannot be read, the value the game
            // itself declares for a space is used and the fallback is reported rather than hidden.
            font.SetAdvance(' ', 4);
            Console.WriteLine("font: definition unreadable; using the reference's default space advance of 4");
        }

        Console.WriteLine(
            $"font: {font.CellWidth}x{font.CellHeight} cells, line {font.Height}, {declared} declared advances, "
            + $"{started.ElapsedMilliseconds} ms");
        return font;
    }

    /// <param name="layout">Block state layout, or null for the demo layout.</param>
    /// <param name="resources">
    /// Session resource manager. When given, block textures, models and blockstates are read through it, which is what lets
    /// an installed pack override the world's textures; without it only the base pack is read.
    /// </param>
    /// <summary>
    /// Builds only the atlas, without resolving models.
    /// </summary>
    /// <param name="resources">Session resource manager.</param>
    /// <remarks>
    /// Resolving block models costs about half a second on a full pack while building the atlas costs about thirty
    /// milliseconds, so a reload that only changed pixels must not pay for the models again.
    /// </remarks>
    internal static SpriteAtlas? PrepareAtlas(Sharpcraft.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return SpriteAtlasBuilder.Build(resources.Stack);
    }

    /// <summary>Resolves block models into an atlas mapping, for a candidate atlas.</summary>
    /// <param name="atlas">Candidate atlas.</param>
    /// <param name="layout">Block state layout.</param>
    /// <param name="resources">Session resource manager.</param>
    internal static BlockSpriteMap PrepareMap(
        SpriteAtlas atlas,
        BlockStateLayout? layout,
        Sharpcraft.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(resources);

        return new BlockSpriteMap(atlas, layout, new BlockModelResolver(resources.Stack));
    }

    /// <summary>
    /// Wraps an atlas in the request texture data, with its mip chain already generated.
    /// </summary>
    /// <param name="atlas">Stitched atlas.</param>
    /// <remarks>
    /// Mips are built here because the per sprite alpha rescaling needs the sprite rectangles, which the renderer never sees.
    /// </remarks>
    internal static TextureData WithMips(SpriteAtlas atlas)
    {
        var sprites = new List<SpriteRect>(atlas.Sprites.Values);

        return new TextureData(atlas.Width, atlas.Height, atlas.Pixels)
        {
            Mips = Sharpcraft.Client.Assets.AtlasMipChain.Build(atlas.Pixels, atlas.Width, atlas.Height, sprites)
        };
    }

    internal static (SpriteAtlas Atlas, BlockSpriteMap Map)? PrepareSprites(
        BlockStateLayout? layout,
        Sharpcraft.Assets.ResourceManager? resources = null)
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

        // The stack stays alive until the atlas and mapping are built, then it is released. A session manager is preferred
        // so installed packs take part; the local stack remains for callers that have none.
        AssetStack stack;

        if (resources is not null)
        {
            stack = resources.Stack;
        }
        else
        {
            stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
                .Add(new BuiltInFallbackSource())
                .Add(new ZipAssetSource(packPath, "base-pack"));
            AddIndexedAssets(stack);
        }

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var atlas = SpriteAtlasBuilder.Build(stack);
        timer.Stop();

        var models = new BlockModelResolver(stack);
        var map = new BlockSpriteMap(atlas, layout, models);

        if (GameOptions.Get("MC_DEBUG_SPRITES") is "1" && layout is not null)
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

            foreach (var blockId in unresolved.Take(400))
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
        var configured = GameOptions.Get("MC_ASSETS_PACK");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var directory = GameOptions.Get("MC_ASSETS_OUT") is { Length: > 0 } output
            ? output
            : GamePaths.DefaultPacksDirectory();

        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.pack.zip")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;
    }

    /// <summary>
    /// Loads the item tag table extracted into the asset pack and installs it on the render resolver. Best effort: no
    /// pack or a malformed entry leaves the table empty (tag-dependent poses stay on their documented fallback).
    /// </summary>
    internal static void InstallItemTags()
    {
        var packPath = FindPack();
        if (packPath is null)
        {
            EntityModelResolver.ItemTags = ItemTagRegistry.Empty;
            return;
        }

        try
        {
            using var archive = ZipFile.OpenRead(packPath);
            var sources = new List<(Identifier Tag, string Json)>();
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(FrameCommand.ItemTagsPrefix, StringComparison.Ordinal) ||
                    !entry.FullName.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                var relative = entry.FullName[FrameCommand.ItemTagsPrefix.Length..^".json".Length];
                using var reader = new StreamReader(entry.Open());
                sources.Add((new Identifier("minecraft", relative), reader.ReadToEnd()));
            }

            EntityModelResolver.ItemTags = sources.Count == 0 ? ItemTagRegistry.Empty : ItemTagRegistry.Parse(sources);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            EntityModelResolver.ItemTags = ItemTagRegistry.Empty;
        }
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
        world.AddChunk(0, -1).AddBlockEntity(new Sharpcraft.Common.World.BlockEntityRecord(8, 17, 15, 1));

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
        BiomeTint? tint = null)
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

                blocks += Sharpcraft.Client.Render.ChunkMesher.MeshChunk(
                    world, chunk, airState: 0, buffer, maxBlocks: 2_000_000 - blocks, sprites: sprites, translucent: translucent, layout: layout, cutout: cutout, tint: tint,
                    merge: GameOptions.Get("MC_MESH_NOMERGE") != "1", lighting: lighting);

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
                    foreach (var record in chunk.BlockEntities)
                    {
                        states.Add(new Sharpcraft.Client.Render.BlockEntityRenderState(
                            (chunk.ChunkX * 16) + record.LocalX,
                            record.Y,
                            (chunk.ChunkZ * 16) + record.LocalZ,
                            record.TypeId));
                    }
                }

                if (states.Count > 0)
                {
                    var mesher = new Sharpcraft.Client.Render.BlockEntityPlaceholderMesher();
                    var blockEntityGeometry = mesher.Build(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(states), new SpriteRect(0, 0, 16, 16, 1));

                    // Appended to the same arrays, so the block entity stage shares the opaque pipeline and its depth.
                    var mergedVertices = new float[mesh.Vertices.Length + blockEntityGeometry.VertexFloats];
                    mesh.Vertices.CopyTo(mergedVertices, 0);
                    blockEntityGeometry.Vertices[..blockEntityGeometry.VertexFloats].CopyTo(mergedVertices.AsSpan(mesh.Vertices.Length));

                    var mergedIndices = new uint[mesh.Indices.Length + blockEntityGeometry.IndexCount];
                    mesh.Indices.CopyTo(mergedIndices, 0);
                    var vertexOffset = (uint)(mesh.Vertices.Length / Sharpcraft.Client.Render.ChunkMesher.FloatsPerVertex);

                    for (var index = 0; index < blockEntityGeometry.IndexCount; index++)
                    {
                        mergedIndices[mesh.Indices.Length + index] = blockEntityGeometry.Indices[index] + vertexOffset;
                    }

                    // The block entity indices sit past every chunk range, so they need a range of their own or they are
                    // never drawn: the draw loop only visits ranges.
                    var blockEntityFirstIndex = mesh.Indices.Length;
                    mesh = new Sharpcraft.Client.Render.MeshData(mergedVertices, mergedIndices, mesh.BlockCount);
                    drawRanges = [.. drawRanges, (blockEntityFirstIndex, blockEntityGeometry.IndexCount)];
                    Console.WriteLine($"block entities: {states.Count} boxes, {blockEntityGeometry.IndexCount / 3} triangles");
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
                ? cache.Build(world, sprites, maxBlocks: 2_000_000)
                : cache.Snapshot();
            drawRanges = null;

            if (layout is not null && sprites is not null)
            {
                // The cache builds one merged mesh, so the translucent layer is meshed separately here. It costs a second
                // pass over the chunks, which is acceptable for the presented view and keeps the cached path untouched.
                using var opaqueSink = new Sharpcraft.Client.Render.MeshBuffer(4096);
                using var translucentBuffer = new Sharpcraft.Client.Render.MeshBuffer(4096);
                using var cutoutBuffer = new Sharpcraft.Client.Render.MeshBuffer(4096);

                foreach (var chunk in world.GetChunks())
                {
                    Sharpcraft.Client.Render.ChunkMesher.MeshChunk(
                        world, chunk, airState: 0, opaqueSink, maxBlocks: 2_000_000, sprites: sprites, translucent: translucentBuffer, layout: layout, cutout: cutoutBuffer, tint: tint,
                        merge: GameOptions.Get("MC_MESH_NOMERGE") != "1", lighting: lighting);
                }

                if (translucentBuffer.IndexCount > 0)
                {
                    var split = translucentBuffer.ToMeshData(0);
                    translucentVertices = split.Vertices;
                    translucentIndices = split.Indices;
                }

                if (cutoutBuffer.IndexCount > 0)
                {
                    var split = cutoutBuffer.ToMeshData(0);
                    cutoutVertices = split.Vertices;
                    cutoutIndices = split.Indices;
                }

                Console.WriteLine(
                    $"layers: cutout {(cutoutIndices?.Length ?? 0) / 3} tri, translucent {(translucentIndices?.Length ?? 0) / 3} tri");
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
            DrawRanges = drawRanges,
            TranslucentVertices = translucentVertices,
            TranslucentIndices = translucentIndices,
            CutoutVertices = cutoutVertices,
            CutoutIndices = cutoutIndices,
            TranslucentRanges = translucentOrdered.Length > 0 ? translucentOrdered : null
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
            layout: layout);
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

    /// <summary>
    /// Renders a sky-only frame with a body (sun, or the moon under MC_MOON_TEST) at angle 0 and a camera aimed straight
    /// at it, proving the celestial pipeline draws at the position the CPU projection predicts. Returns the PNG path.
    /// </summary>
    internal static string RenderSunTest(int width, int height)
    {
        // MC_MOON_TEST switches to the moon (size 20) so the moon sprite can be verified the same way as the sun.
        var isMoon = GameOptions.Get("MC_MOON_TEST") is { Length: > 0 };
        var bodySize = isMoon ? 20f : 30f;
        var label = isMoon ? "moon" : "sun";

        var (vertices, indices) = CelestialMeshBuilder.CelestialQuad(
            0f, height: 100f, size: bodySize, uv: (0f, 0f, 1f, 1f));

        // A 2x2 pure-white texture by default so a lit pixel is unambiguous against the blue sky and no pack is needed.
        TextureData bodyTexture = new(2, 2,
        [
            255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255
        ]);

        // With MC_SUN_TEST_PACK set, draw the real sprite from the pack instead, to check its orientation and UVs.
        if (GameOptions.Get("MC_SUN_TEST_PACK") is { Length: > 0 })
        {
            using var resources = CreateResourceManager();
            var identifier = isMoon ? CelestialSprites.MoonPhase(0) : CelestialSprites.Sun;
            var bytes = resources.Read(ResourceKind.Texture, identifier);
            if (bytes is not null)
            {
                var image = PngDecoder.Decode(bytes);
                bodyTexture = new TextureData(image.Width, image.Height, image.Pixels);
                Console.WriteLine($"{label} test: real sprite {identifier.Path} {image.Width}x{image.Height}");
            }
        }

        var draw = new CelestialQuadDraw(0, vertices, indices, Matrix4x4.Identity, Vector4.One);

        // The body lies in the horizontal plane y=100 around the origin; look at its centre from below and in front.
        var request = new FrameRequest(
            width,
            height,
            [],
            [],
            new Vector4(0f, 0f, 0f, 1f),
            new Vector3(0f, 70f, 30f),
            new Vector3(0f, 100f, 0f),
            60f)
        {
            CelestialTextures = [bodyTexture],
            CelestialDraws = [draw]
        };

        var frame = OffscreenRenderer.Render(request);

        // The centre pixel is on the ray to the body's centroid, so it must be the disc, not the sky gradient.
        var centreX = frame.Width / 2;
        var centreY = frame.Height / 2;
        var offset = ((centreY * frame.Width) + centreX) * 4;
        Console.WriteLine($"{label} test: centre rgba({frame.Rgba[offset]},{frame.Rgba[offset + 1]},{frame.Rgba[offset + 2]},{frame.Rgba[offset + 3]})");

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), $"sharpcraft-{label}-test.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>
    /// Day time used for lighting and celestial angles. A host override (<c>MC_FORCE_DAYTIME</c>) renders a frame at a chosen
    /// tick for verification; otherwise it is the world clock.
    /// </summary>
    internal static int EffectiveDayTime(ClientWorld world) =>
        GameOptions.Get("MC_FORCE_DAYTIME") is { Length: > 0 } text && int.TryParse(text, out var forced)
            ? forced
            : world.DayTime;

    /// <summary>
    /// Sub-tick fraction for interpolating environment attributes. <c>MC_FORCE_PARTIAL</c> fixes it for verification;
    /// otherwise it is the overworld clock's partial tick (matching the reference EnvironmentAttributeProbe).
    /// </summary>
    internal static float EffectiveDayTimePartial(ClientWorld world) =>
        GameOptions.Get("MC_FORCE_PARTIAL") is { Length: > 0 } text && float.TryParse(text, out var forced)
            ? Math.Clamp(forced, 0f, 1f)
            : world.DayTimePartial;

    /// <summary>Rain level at a sub-tick fraction, with <c>MC_FORCE_RAIN</c> override for verification.</summary>
    internal static float EffectiveRainLevel(ClientWorld world, float partialTick) =>
        GameOptions.Get("MC_FORCE_RAIN") is { Length: > 0 } text && float.TryParse(text, out var forced)
            ? Math.Clamp(forced, 0f, 1f)
            : world.RainLevel(partialTick);

    /// <summary>Thunder level (already scaled by rain), with <c>MC_FORCE_THUNDER</c> override for verification.</summary>
    internal static float EffectiveThunderLevel(ClientWorld world, float partialTick)
    {
        var rain = EffectiveRainLevel(world, partialTick);
        return GameOptions.Get("MC_FORCE_THUNDER") is { Length: > 0 } text && float.TryParse(text, out var forced)
            ? Math.Clamp(forced, 0f, 1f) * rain
            : world.ThunderLevel(partialTick);
    }

    /// <summary>
    /// The lightmap parameters for the current time and weather: the day/night sky factor and colour from the timelines,
    /// then the RAIN/THUNDER layers applied (docs/modules/weather-and-sky-color.md 6.3).
    /// </summary>
    internal static LightmapParameters EffectiveLighting(ClientWorld world)
    {
        var partialTick = EffectiveDayTimePartial(world);
        var parameters = LightmapParameters.OverworldAt(EffectiveDayTime(world), partialTick);
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);

        return parameters with
        {
            SkyFactor = Sharpcraft.Common.World.WeatherBlend.SkyLightFactor(parameters.SkyFactor, rain, thunder),
            SkyLightColor = Sharpcraft.Common.World.WeatherBlend.ApplySkyLightColor(parameters.SkyLightColor, rain, thunder)
        };
    }

    /// <summary>Fog colour for the current day time and weather (base overworld fog × FOG_COLOR timeline × weather).</summary>
    internal static Vector4 EffectiveFogColour(ClientWorld world)
    {
        var partialTick = EffectiveDayTimePartial(world);
        var factor = Sharpcraft.Common.World.SkyAttributeTracks.FogColorFactorAt(EffectiveDayTime(world), partialTick);
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);
        var fog = Sharpcraft.Common.World.WeatherBlend.ApplyFog(new Vector3(0.75f, 0.85f, 1.0f) * factor, rain, thunder);
        return new Vector4(fog, 1f);
    }

    /// <summary>
    /// The SKY_COLOR at a world position: the biome's calculateSkyColor blended over neighbours, multiplied by the
    /// day/night SKY_COLOR timeline and darkened by weather (docs/modules/weather-and-sky-color.md 6.2).
    /// </summary>
    internal static Vector3 EffectiveSkyColour(ClientWorld world, Sharpcraft.Common.World.BiomeRegistry biomes, int x, int y, int z)
    {
        // The reference clamps the biome sample's quart Y into the world height (ChunkAccess.getNoiseBiome), so a camera
        // above/below the build range still gets the top/bottom section's biome instead of no weight and a black colour.
        var minBlockY = world.MinSectionY * 16;
        var maxBlockY = minBlockY + (world.SectionCount * 16) - 1;
        var sampleY = Math.Clamp(y, minBlockY, maxBlockY);
        var baseSky = Sharpcraft.Common.World.SkyColourSource.ColorAt(world.GetBiomeAt, biomes, x, sampleY, z);
        if (baseSky == Vector3.Zero)
        {
            // A column whose extreme sections are missing still falls back to a loaded section near sea level.
            baseSky = Sharpcraft.Common.World.SkyColourSource.ColorAt(world.GetBiomeAt, biomes, x, 64, z);
        }
        var partialTick = EffectiveDayTimePartial(world);
        var multiplier = Sharpcraft.Common.World.SkyAttributeTracks.SkyColorMultiplierAt(EffectiveDayTime(world), partialTick);
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);
        return Sharpcraft.Common.World.WeatherBlend.ApplySkyColour(baseSky * multiplier, rain, thunder);
    }

    /// <summary>
    /// Builds the per-frame cloud position-colour draw: visible faces via CloudMeshBuilder, geometry via
    /// CloudGeometryBuilder, tinted by the CLOUD_COLOR timeline and weather (docs/modules/clouds.md 2).
    /// </summary>
    internal static CelestialColorDraw? BuildCloudDraw(
        ClientWorld world,
        Sharpcraft.Client.Assets.CloudTexture texture,
        Vector3 eye,
        Sharpcraft.Client.Render.CloudDrawCache? cache = null,
        int rangeChunks = 128,
        bool fancy = true,
        float? partialTickOverride = null)
    {
        var partialTick = partialTickOverride ?? EffectiveDayTimePartial(world);
        var offset = (float)(world.GameTime % (texture.Width * 400L)) + partialTick;
        var cloudX = eye.X + (offset * 0.03f);
        var cloudZ = eye.Z + 3.96f;
        var widthBlocks = texture.Width * 12.0;
        var heightBlocks = texture.Height * 12.0;
        cloudX -= (float)(Math.Floor(cloudX / widthBlocks) * widthBlocks);
        cloudZ -= (float)(Math.Floor(cloudZ / heightBlocks) * heightBlocks);
        var cellX = (int)Math.Floor(cloudX / 12.0);
        var cellZ = (int)Math.Floor(cloudZ / 12.0);
        var xInCell = (float)(cloudX - (cellX * 12.0));
        var zInCell = (float)(cloudZ - (cellZ * 12.0));

        const float bottomY = 192.33f;
        var relativeBottom = bottomY - eye.Y;
        var relativeTop = relativeBottom + 4f;
        var side = relativeTop < 0f
            ? Sharpcraft.Client.Render.CloudCameraSide.AboveClouds
            : relativeBottom > 0f
                ? Sharpcraft.Client.Render.CloudCameraSide.BelowClouds
                : Sharpcraft.Client.Render.CloudCameraSide.InsideClouds;

        var radiusCells = (int)MathF.Ceiling(rangeChunks * 16f / 12f);
        float[] vertices;
        uint[] indices;
        Matrix4x4 model;

        if (cache is null || !fancy)
        {
            var quads = fancy
                ? Sharpcraft.Client.Render.CloudMeshBuilder.Build(
                    texture.Cells, texture.Width, texture.Height, cellX, cellZ, radiusCells, side)
                : Sharpcraft.Client.Render.CloudMeshBuilder.BuildFlat(
                    texture.Cells, texture.Width, texture.Height, cellX, cellZ, radiusCells);
            if (quads.Count == 0)
            {
                return null;
            }

            // Absolute-vertex path (offscreen one-shot): origin is the eye shifted by the in-cell remainder.
            var origin = Sharpcraft.Client.Render.CloudGeometryBuilder.OriginFromEye(eye, xInCell, zInCell, bottomY);
            (vertices, indices) = Sharpcraft.Client.Render.CloudGeometryBuilder.Build(quads, eye, origin);
            model = Matrix4x4.Identity;
        }
        else
        {
            // Cached camera-relative mesh: vertices already carry the vertical offset; move it with the eye each frame.
            (vertices, indices) = cache.Get(
                texture.Cells, texture.Width, texture.Height, cellX, cellZ, radiusCells, side);
            if (vertices.Length == 0)
            {
                return null;
            }

            // Keep the cloud plane at the fixed absolute bottom height; only horizontal offset follows the eye, so a
            // jump adds no vertical snapping.
            model = Matrix4x4.CreateTranslation(eye.X - xInCell, bottomY, eye.Z - zInCell);
        }

        var factor = Sharpcraft.Common.World.SkyAttributeTracks.CloudColorFactorAt(EffectiveDayTime(world));
        var cloudAlpha = Sharpcraft.Common.World.SkyAttributeTracks.CloudColorAlphaAt(EffectiveDayTime(world));
        var rain = EffectiveRainLevel(world, partialTick);
        var tint = new Vector4(
            factor.X * (1f - (0.5f * rain)),
            factor.Y * (1f - (0.5f * rain)),
            factor.Z * (1f - (0.4f * rain)),
            cloudAlpha);

        return new CelestialColorDraw(vertices, indices, model, tint, CelestialColorBlend.Translucent);
    }

    /// <summary>Maps the client lightmap parameters to the framework uniform the terrain shader reads.</summary>
    internal static LightmapUniform ToLightmapUniform(LightmapParameters parameters) => new(
        SkyFactor: parameters.SkyFactor,
        BlockFactor: parameters.BlockFactor,
        BrightnessFactor: parameters.BrightnessFactor,
        Padding: 0f,
        SkyLightColor: new Vector4(parameters.SkyLightColor, 1f),
        BlockLightTint: new Vector4(parameters.BlockLightTint, 1f),
        AmbientColor: new Vector4(parameters.AmbientColor, 0f));

    /// <summary>
    /// Adds the celestial bodies for the world's current day time. The sun (and later the moon, stars and sunrise band)
    /// are attached here so every offscreen path draws the same sky.
    /// </summary>
    internal static FrameRequest AttachCelestial(
        FrameRequest request,
        ClientWorld world,
        Sharpcraft.Assets.ResourceManager resources,
        Vector3? skyColour = null,
        Vector3? fogColour = null)
    {
        var textures = new List<TextureData>();
        var textureIndices = new Dictionary<Sharpcraft.Common.Identifiers.Identifier, int>();
        var draws = new List<CelestialQuadDraw>();
        var colorDraws = new List<CelestialColorDraw>();
        var skyDiscDraws = new List<CelestialColorDraw>();
        var dayTime = EffectiveDayTime(world);
        var partialTick = EffectiveDayTimePartial(world);

        // Rain/thunder levels (host override for verification); rain brightness dims the sun and moon, and rain/thunder
        // dims the stars (WeatherAttributes sets STAR_BRIGHTNESS to 0 under rain/thunder).
        var rainLevel = EffectiveRainLevel(world, partialTick);
        var thunderLevel = EffectiveThunderLevel(world, partialTick);
        var rainBrightness = Sharpcraft.Common.World.WeatherBlend.RainBrightness(rainLevel);

        // The moon phase follows total world ticks (one phase per 24000 ticks); angles follow the day clock.
        var totalTicks = world.TryGetClock(ClientWorld.OverworldClockId, out var clock) ? clock.TotalTicks : world.GameTime;
        var moonPhaseIndex = Sharpcraft.Common.World.SkyAttributeTracks.MoonPhaseIndex(totalTicks);
        var moonAngle = GameOptions.Get("MC_MOON_ANGLE") is { Length: > 0 } moonText &&
            float.TryParse(moonText, out var parsedMoonAngle)
            ? parsedMoonAngle
            : Sharpcraft.Common.World.SkyTimelines.MoonAngleDegrees(dayTime, partialTick);

        // A fixed angle is available for verification; otherwise the sun follows the overworld clock.
        var sunAngle = GameOptions.Get("MC_SUN_ANGLE") is { Length: > 0 } angleText &&
            float.TryParse(angleText, out var parsedAngle)
            ? parsedAngle
            : Sharpcraft.Common.World.SkyTimelines.SunAngleDegrees(dayTime, partialTick);

        // Preload the sun and every moon phase so the texture array has a stable index while the phase advances.
        AddTexture(CelestialSprites.Sun);
        for (var phase = 0; phase < CelestialSprites.MoonPhaseCount; phase++)
        {
            AddTexture(CelestialSprites.MoonPhase(phase));
        }

        AddBody(CelestialSprites.Sun, sunAngle, size: 30f);
        // The reference builds the moon quads with reversed UVs (buildMoonPhases) while the sun uses forward UVs, so the
        // raw phase images are rotated 180 degrees to match the reference's on-screen orientation.
        AddBody(CelestialSprites.MoonPhase(moonPhaseIndex), moonAngle, size: 20f, flip: true);

        // Sunrise/sunset band: a position-colour triangle fan, skipped below the reference alpha cutoff. Rain/thunder
        // multiply the colour (WeatherAttributes SUNRISE_SUNSET_COLOR MULTIPLY_ARGB).
        var sunrise = Sharpcraft.Common.World.WeatherBlend.ApplySunrise(
            Sharpcraft.Common.World.SkyAttributeTracks.SunriseSunsetColorAt(dayTime, partialTick), rainLevel, thunderLevel);
        if (sunrise.W > 0.001f)
        {
            var sinSun = MathF.Sin(sunAngle * (MathF.PI / 180f));
            var theta = ((sinSun < 0f ? 180f : 0f) + 90f) * (MathF.PI / 180f);
            var fan = CelestialGeometry.SunriseFanWorld(theta, sunrise.W);

            var colours = new Vector4[fan.Length];
            colours[0] = new Vector4(1f, 1f, 1f, 1f);
            for (var i = 1; i < colours.Length; i++)
            {
                colours[i] = new Vector4(1f, 1f, 1f, 0f);
            }

            var triangles = new List<uint>((fan.Length - 2) * 3);
            for (var i = 1; i + 1 < fan.Length; i++)
            {
                triangles.Add(0);
                triangles.Add((uint)i);
                triangles.Add((uint)(i + 1));
            }

            AddColor(fan, colours, triangles, sunrise, CelestialColorBlend.Translucent);
        }

        // Stars: the reference star field rotated by the star angle, brightened by the star-brightness attribute.
        var starBrightness = Sharpcraft.Common.World.SkyAttributeTracks.StarBrightnessAt(dayTime, partialTick)
            * (1f - MathF.Max(0f, rainLevel - thunderLevel)) * (1f - thunderLevel);
        if (starBrightness > 0f)
        {
            var starCorners = StarField.Build(out var keptStars);
            var starAngle = Sharpcraft.Common.World.SkyTimelines.StarAngleDegrees(dayTime, partialTick);
            var rotated = new Vector3[starCorners.Length];
            CelestialGeometry.SkyRotate(starCorners, starAngle * (MathF.PI / 180f), rotated);

            var colours = new Vector4[rotated.Length];
            Array.Fill(colours, Vector4.One);
            var triangles = new List<uint>(keptStars * 6);
            for (var quad = 0; quad < keptStars; quad++)
            {
                var b = (uint)(quad * 4);
                triangles.Add(b);
                triangles.Add(b + 1);
                triangles.Add(b + 2);
                triangles.Add(b);
                triangles.Add(b + 2);
                triangles.Add(b + 3);
            }

            AddColor(rotated, colours, triangles, new Vector4(starBrightness, starBrightness, starBrightness, starBrightness), CelestialColorBlend.Overlay);
        }

        if (LogCelestial && (draws.Count > 0 || colorDraws.Count > 0))
        {
            Console.WriteLine(
                $"celestial: {textures.Count} texture(s), {draws.Count} textured draw(s), {colorDraws.Count} colour draw(s), " +
                $"sun {sunAngle:F1} deg, moon {moonAngle:F1} deg (phase {moonPhaseIndex}), sunrise a={sunrise.W:F3}, stars {starBrightness:F3}, rain {rainLevel:F2} (sun/moon alpha {rainBrightness:F2})");
        }

        var effectiveSkyColour = skyColour ?? new Vector3(request.ClearColour.X, request.ClearColour.Y, request.ClearColour.Z);
        var effectiveFogColour = fogColour ?? effectiveSkyColour;
        var skyFogEnd = MathF.Min(InteractiveSession.ViewDistanceChunks * 16f, 512f);
        const int skyRings = 16;
        const int skySegments = 64;
        const float skyRadius = 512f;
        var skyVertexCount = 1 + (skyRings * skySegments);
        var skyVertices = new float[skyVertexCount * 7];

        void WriteSkyVertex(int index, float x, float y, float z, Vector3 colour)
        {
            var offset = index * 7;
            skyVertices[offset] = x;
            skyVertices[offset + 1] = y;
            skyVertices[offset + 2] = z;
            skyVertices[offset + 3] = colour.X;
            skyVertices[offset + 4] = colour.Y;
            skyVertices[offset + 5] = colour.Z;
            skyVertices[offset + 6] = 1f;
        }

        WriteSkyVertex(0, 0f, 16f, 0f, effectiveSkyColour);
        var skyIndex = 1;
        for (var ring = 1; ring <= skyRings; ring++)
        {
            var radius = skyRadius * ring / skyRings;
            var fogFactor = Math.Clamp(radius / MathF.Max(skyFogEnd, 0.001f), 0f, 1f);
            var colour = Vector3.Lerp(effectiveSkyColour, effectiveFogColour, fogFactor);
            for (var segment = 0; segment < skySegments; segment++)
            {
                var angle = segment * MathF.Tau / skySegments;
                WriteSkyVertex(skyIndex++, radius * MathF.Cos(angle), 16f, radius * MathF.Sin(angle), colour);
            }
        }

        var skyTriangles = new List<uint>((skySegments * 3) + ((skyRings - 1) * skySegments * 6));
        for (var segment = 0; segment < skySegments; segment++)
        {
            var next = (segment + 1) % skySegments;
            skyTriangles.Add(0);
            skyTriangles.Add((uint)(1 + segment));
            skyTriangles.Add((uint)(1 + next));
        }

        for (var ring = 1; ring < skyRings; ring++)
        {
            var inner = 1 + ((ring - 1) * skySegments);
            var outer = inner + skySegments;
            for (var segment = 0; segment < skySegments; segment++)
            {
                var next = (segment + 1) % skySegments;
                skyTriangles.Add((uint)(inner + segment));
                skyTriangles.Add((uint)(outer + segment));
                skyTriangles.Add((uint)(outer + next));
                skyTriangles.Add((uint)(inner + segment));
                skyTriangles.Add((uint)(outer + next));
                skyTriangles.Add((uint)(inner + next));
            }
        }

        skyDiscDraws.Add(new CelestialColorDraw(
            skyVertices,
            [.. skyTriangles],
            Matrix4x4.CreateTranslation(request.CameraPosition),
            new Vector4(1f, 1f, 1f, 1f),
            CelestialColorBlend.Opaque));

        return request with
        {
            CelestialTextures = textures.Count > 0 ? [.. textures] : null,
            CelestialDraws = draws.Count > 0 ? [.. draws] : null,
            CelestialColorDraws = colorDraws.Count > 0 ? [.. colorDraws] : null,
            SkyDiscDraws = skyDiscDraws.Count > 0 ? [.. skyDiscDraws] : null
        };

        int AddTexture(Sharpcraft.Common.Identifiers.Identifier identifier)
        {
            if (textureIndices.TryGetValue(identifier, out var existing))
            {
                return existing;
            }

            if (!CelestialTextureCache.TryGetValue(identifier, out var decoded))
            {
                var data = resources.Read(ResourceKind.Texture, identifier);
                if (data is null)
                {
                    return -1;
                }

                var decodedImage = PngDecoder.Decode(data);
                decoded = new TextureData(decodedImage.Width, decodedImage.Height, decodedImage.Pixels);
                CelestialTextureCache[identifier] = decoded;
            }

            var index = textures.Count;
            textures.Add(decoded);
            textureIndices[identifier] = index;
            return index;
        }

        void AddBody(Sharpcraft.Common.Identifiers.Identifier identifier, float angleDegrees, float size, bool flip = false)
        {
            var textureIndex = AddTexture(identifier);
            if (textureIndex < 0)
            {
                return;
            }

            var uv = flip ? (1f, 1f, 0f, 0f) : (0f, 0f, 1f, 1f);
            var (vertices, indices) = CelestialMeshBuilder.CelestialQuad(
                angleDegrees * (MathF.PI / 180f), height: 100f, size: size, uv: uv);

            // Translate to the eye so the body stays at the same place relative to the camera, i.e. infinitely far.
            // RAIN_BRIGHTNESS (= 1 - rainLevel) is the reference alpha on the sun/moon transform.
            draws.Add(new CelestialQuadDraw(textureIndex, vertices, indices, Matrix4x4.CreateTranslation(request.CameraPosition), new Vector4(1f, 1f, 1f, rainBrightness)));
        }

        void AddColor(Vector3[] positions, Vector4[] colours, List<uint> triangles, Vector4 tint, CelestialColorBlend blend)
        {
            var vertices = new float[positions.Length * 7];
            for (var i = 0; i < positions.Length; i++)
            {
                var offset = i * 7;
                vertices[offset] = positions[i].X;
                vertices[offset + 1] = positions[i].Y;
                vertices[offset + 2] = positions[i].Z;
                vertices[offset + 3] = colours[i].X;
                vertices[offset + 4] = colours[i].Y;
                vertices[offset + 5] = colours[i].Z;
                vertices[offset + 6] = colours[i].W;
            }

            colorDraws.Add(new CelestialColorDraw(vertices, [.. triangles], Matrix4x4.CreateTranslation(request.CameraPosition), tint, blend));
        }
    }

    /// <summary>
    /// Offscreen check of the position-colour celestial path: renders a sky-only frame with the sunrise fan (solid red) and
    /// the star field, then without them, and reports how many pixels the colour draws changed.
    /// </summary>
    internal static string RenderColorTest(int width, int height)
    {
        var fan = CelestialGeometry.SunriseFanWorld(MathF.PI / 2f, 1f);
        var fanColours = new Vector4[fan.Length];
        Array.Fill(fanColours, new Vector4(1f, 0f, 0f, 1f));
        var fanTriangles = new List<uint>((fan.Length - 2) * 3);
        for (var i = 1; i + 1 < fan.Length; i++)
        {
            fanTriangles.Add(0);
            fanTriangles.Add((uint)i);
            fanTriangles.Add((uint)(i + 1));
        }

        var starCorners = StarField.Build(out var keptStars);
        var starRotated = new Vector3[starCorners.Length];
        CelestialGeometry.SkyRotate(starCorners, 0f, starRotated);
        var starColours = new Vector4[starRotated.Length];
        Array.Fill(starColours, Vector4.One);
        var starTriangles = new List<uint>(keptStars * 6);
        for (var quad = 0; quad < keptStars; quad++)
        {
            var b = (uint)(quad * 4);
            starTriangles.Add(b);
            starTriangles.Add(b + 1);
            starTriangles.Add(b + 2);
            starTriangles.Add(b);
            starTriangles.Add(b + 2);
            starTriangles.Add(b + 3);
        }

        var (fanVertices, fanIndices) = PackColor(fan, fanColours, fanTriangles);
        var (starVertices, starIndices) = PackColor(starRotated, starColours, starTriangles);

        var eye = new Vector3(0f, 150f, 300f);
        var withBodies = new FrameRequest(
            width,
            height,
            [],
            [],
            new Vector4(0f, 0f, 0f, 1f),
            eye,
            Vector3.Zero)
        {
            CelestialColorDraws =
            [
                new CelestialColorDraw(fanVertices, fanIndices, Matrix4x4.Identity, new Vector4(1f, 0f, 0f, 1f), CelestialColorBlend.Translucent),
                new CelestialColorDraw(starVertices, starIndices, Matrix4x4.Identity, Vector4.One, CelestialColorBlend.Overlay)
            ]
        };

        var frame = OffscreenRenderer.Render(withBodies);
        var without = OffscreenRenderer.Render(withBodies with { CelestialColorDraws = null });
        var difference = PixelComparison.Compare(without.Rgba, frame.Rgba, frame.Width, frame.Height, 0);
        Console.WriteLine(
            $"color test: {keptStars} stars + sunrise changed {difference.Mismatches} pixels" +
            (difference.Mismatches > 0 ? $", first ({difference.FirstX},{difference.FirstY})" : string.Empty));

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "sharpcraft-color-test.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    /// <summary>
    /// Diagnostic: draws the sky disc (<see cref="CelestialGeometry.SkyDisc"/> at y=16) with the calculateSkyColor(temperature)
    /// tint and probes the centre pixel, proving the disc geometry and colour pipeline. <c>MC_BIOME_TEMP</c> overrides the
    /// temperature (default 0.5).
    /// </summary>
    internal static string RenderSkyDiscTest(int width, int height)
    {
        var temperature = GameOptions.Get("MC_BIOME_TEMP") is { Length: > 0 } text && float.TryParse(text, out var parsed)
            ? parsed
            : 0.5f;
        var packed = Sharpcraft.Common.World.SkyColour.FromTemperature(temperature);
        var tint = new Vector4(
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            (packed & 0xFF) / 255f,
            1f);

        var disc = CelestialGeometry.SkyDisc(16f);
        var colours = new Vector4[disc.Length];
        Array.Fill(colours, Vector4.One);
        var triangles = new List<uint>();
        for (var i = 1; i + 1 < disc.Length; i++)
        {
            triangles.Add(0);
            triangles.Add((uint)i);
            triangles.Add((uint)(i + 1));
        }

        var (vertices, indices) = PackColor(disc, colours, triangles);
        var request = new FrameRequest(
            width,
            height,
            [],
            [],
            new Vector4(0f, 0f, 0f, 1f),
            new Vector3(0f, -10f, 30f),
            new Vector3(0f, 16f, 0f),
            60f)
        {
            CelestialColorDraws = [new CelestialColorDraw(vertices, indices, Matrix4x4.Identity, tint, CelestialColorBlend.Translucent)]
        };

        var frame = OffscreenRenderer.Render(request);
        var without = OffscreenRenderer.Render(request with { CelestialColorDraws = null });
        var difference = PixelComparison.Compare(without.Rgba, frame.Rgba, frame.Width, frame.Height, 0);

        var centreX = frame.Width / 2;
        var centreY = frame.Height / 2;
        var offset = ((centreY * frame.Width) + centreX) * 4;
        Console.WriteLine(
            $"sky disc test: temp {temperature:F2} expect argb 0x{packed:X8}, " +
            $"centre rgba({frame.Rgba[offset]},{frame.Rgba[offset + 1]},{frame.Rgba[offset + 2]},{frame.Rgba[offset + 3]}), " +
            $"changed {difference.Mismatches} px");

        var png = PngEncoder.EncodeRgba(frame.Width, frame.Height, frame.Rgba);
        var path = Path.Combine(Path.GetTempPath(), "sharpcraft-sky-disc.png");
        File.WriteAllBytes(path, png);
        return path;
    }

    private static (float[] Vertices, uint[] Indices) PackColor(Vector3[] positions, Vector4[] colours, List<uint> triangles)
    {
        var vertices = new float[positions.Length * 7];
        for (var i = 0; i < positions.Length; i++)
        {
            var offset = i * 7;
            vertices[offset] = positions[i].X;
            vertices[offset + 1] = positions[i].Y;
            vertices[offset + 2] = positions[i].Z;
            vertices[offset + 3] = colours[i].X;
            vertices[offset + 4] = colours[i].Y;
            vertices[offset + 5] = colours[i].Z;
            vertices[offset + 6] = colours[i].W;
        }

        return (vertices, [.. triangles]);
    }

    private static Vector3 SkyTintColour(float temperature)
    {
        var packed = Sharpcraft.Common.World.SkyColour.FromTemperature(temperature);
        return new Vector3(((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f);
    }

    /// <summary>Applies the SKY_COLOR timeline multiplier and weather layers to a base sky RGB (docs/... 6.2/6.3).</summary>
    private static Vector3 ApplySkyWeather(Vector3 baseSky, ClientWorld world)
    {
        var partialTick = EffectiveDayTimePartial(world);
        var multiplier = Sharpcraft.Common.World.SkyAttributeTracks.SkyColorMultiplierAt(EffectiveDayTime(world), partialTick);
        var sky = baseSky * multiplier;
        var rain = EffectiveRainLevel(world, partialTick);
        var thunder = EffectiveThunderLevel(world, partialTick);
        return Sharpcraft.Common.World.WeatherBlend.ApplySkyColour(sky, rain, thunder);
    }

    /// <summary>Replaces the gradient sky with the SKY_COLOR top disc (plus the dark bottom disc below the horizon).</summary>
    private static FrameRequest AppendSkyDisc(FrameRequest request, Vector3 skyColour)
    {
        var eye = request.CameraPosition;
        var draws = request.CelestialColorDraws is null
            ? new List<CelestialColorDraw>()
            : new List<CelestialColorDraw>(request.CelestialColorDraws);

        AddDisc(CelestialGeometry.SkyDisc(16f), Matrix4x4.CreateTranslation(eye), new Vector4(skyColour, 1f));

        var horizon = GameOptions.Get("MC_HORIZON") is { Length: > 0 } horizonText && float.TryParse(horizonText, out var parsedHorizon)
            ? parsedHorizon
            : 63f;
        var underwater = GameOptions.Get("MC_UNDERWATER") == "1";
        if (SkyVisibility.ShouldRenderDarkDisc(eye.Y, horizon, underwater))
        {
            AddDisc(CelestialGeometry.SkyDisc(-16f), Matrix4x4.CreateTranslation(eye + new Vector3(0f, 12f, 0f)), new Vector4(0f, 0f, 0f, 1f));
        }

        return request with { SkipGradientSky = true, CelestialColorDraws = [.. draws] };

        void AddDisc(Vector3[] positions, Matrix4x4 model, Vector4 tint)
        {
            var colours = new Vector4[positions.Length];
            Array.Fill(colours, Vector4.One);
            var triangles = new List<uint>((positions.Length - 2) * 3);
            for (var i = 1; i + 1 < positions.Length; i++)
            {
                triangles.Add(0);
                triangles.Add((uint)i);
                triangles.Add((uint)(i + 1));
            }

            var (vertices, indices) = PackColor(positions, colours, triangles);
            draws.Add(new CelestialColorDraw(vertices, indices, model, tint, CelestialColorBlend.Translucent));
        }
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
        var prepared = PrepareSprites(DemoLayout());

        if (GameOptions.Get("MC_DEBUG_SPRITES") is "1")
        {
            for (var y = 0; y < 12; y++)
            {
                Console.WriteLine($"  demo block (0,{y},0) state {world.GetBlockState(0, y, 0)}");
            }
        }
        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 2_000_000, sprites: prepared?.Map,
            merge: GameOptions.Get("MC_MESH_NOMERGE") != "1", lighting: EffectiveLighting(world));
        Console.WriteLine($"local demo: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var bounds = (Min: new Vector3(0, -16, 0), Max: new Vector3(16, 16, 16));
        var centre = (bounds.Min + bounds.Max) / 2f;
        var (eye, target) = CameraMath.DefaultCamera(centre, 16f);

        using var resources = CreateResourceManager();
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
                : WithMips(prepared.Value.Atlas)
        };
        request = AttachCelestial(request, world, resources);

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
}
