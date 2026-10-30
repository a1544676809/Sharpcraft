// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Auth;
using Sharpcraft.Launcher.Accounts;
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
                DeviceCodePrompter.Show(challenge);
                return Task.CompletedTask;
            },
            cancellationToken,
            onSignedIn: AccountsCommand.RecordPremiumSignIn).ConfigureAwait(false);

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
    /// <summary>Loads a font definition referenced from another one.</summary>
    /// <param name="stack">Asset stack to read from.</param>
    /// <param name="reference">Identifier such as <c>minecraft:include/space</c>.</param>
    private static string? ReadFontReference(
        Sharpcraft.Client.Assets.ResourceManager resources,
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
        Sharpcraft.Client.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        // The interface follows the machine's language, the way the reference does: on a Chinese machine the same
        // message must not appear in English.
        // MC_LANGUAGE pins the language for a run, which is how the Chinese path can be verified on an English
        // machine; otherwise the machine's own language is used, as the reference does.
        var forced = Environment.GetEnvironmentVariable("MC_LANGUAGE");
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
    internal static Sharpcraft.Client.Assets.ResourceManager CreateResourceManager()
    {
        var (stack, count) = BuildStack();

        if (count > 0)
        {
            Console.WriteLine($"resource packs: {count} installed");
        }

        // The factory lets a reload rebuild the same stack later without any loader knowing how it was built.
        return new Sharpcraft.Client.Assets.ResourceManager(stack, count, BuildStack);
    }

    /// <summary>The pack folders named by <c>MC_RESOURCE_PACKS</c>, split and trimmed.</summary>
    internal static IReadOnlyList<string> ResourcePackFolders()
    {
        var configured = Environment.GetEnvironmentVariable("MC_RESOURCE_PACKS");

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
    private static Sharpcraft.Client.Assets.PackFormatVersion ResolveClientPackFormat()
    {
        var configured = Environment.GetEnvironmentVariable("MC_PACK_FORMAT");

        if (Sharpcraft.Client.Assets.PackFormatVersion.TryParse(configured, out var parsed))
        {
            return parsed;
        }

        return new Sharpcraft.Client.Assets.PackFormatVersion(97, 1);
    }

    /// <summary>Drops a trailing <c>.png</c> from a provider's file name.</summary>
    /// <param name="path">Path such as <c>font/ascii.png</c>.</param>
    private static string StripTextureExtension(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;

    /// <summary>Builds a stack from the base pack plus every installed pack.</summary>
    /// <returns>The stack and how many installed packs it carries.</returns>
    private static (AssetStack Stack, int PackCount) BuildStack()
    {
        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new BuiltInFallbackSource());

        var packPath = FindPack();

        if (packPath is not null)
        {
            stack.Add(new ZipAssetSource(packPath, "base-pack"));
        }

        var count = 0;
        var clientFormat = ResolveClientPackFormat();

        foreach (var folder in ResourcePackFolders())
        {
            foreach (var pack in Sharpcraft.Client.Assets.ResourcePackDiscovery.Discover(folder, clientFormat))
            {
                stack.Add(new Sharpcraft.Client.Assets.ResourcePackSource(pack));
                count++;
            }
        }

        return (stack, count);
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
        Sharpcraft.Client.Assets.ResourceManager resources,
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
        Sharpcraft.Client.Assets.ResourceManager resources)
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
        Sharpcraft.Client.Assets.ResourceManager resources)
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
    internal static SpriteAtlas? PrepareAtlas(Sharpcraft.Client.Assets.ResourceManager resources)
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
        Sharpcraft.Client.Assets.ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(resources);

        return new BlockSpriteMap(atlas, layout, new BlockModelResolver(resources.Stack));
    }

    internal static (SpriteAtlas Atlas, BlockSpriteMap Map)? PrepareSprites(
        BlockStateLayout? layout,
        Sharpcraft.Client.Assets.ResourceManager? resources = null)
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
        }

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
        bool meshSynchronously = true)
    {
        // With a cache only the chunks whose blocks changed are re-meshed; without one the whole world is
        // scanned, which is what the offscreen paths still do (docs/specs/network/play-movement.md 7.19).
        // The windowed path passes meshSynchronously false and lets the background worker do the meshing.
        if (cache is null)
        {
            // Mesh chunk by chunk so each one gets an index range: without ranges a culled chunk cannot be left out.
            using var buffer = new Sharpcraft.Client.Render.MeshBuffer(Math.Min(2_000_000, world.LoadedChunkCount * 512));
            var ranges = new List<(int FirstIndex, int IndexCount)>(world.LoadedChunkCount);
            var blocks = 0;

            foreach (var chunk in world.GetChunks())
            {
                var before = buffer.IndexCount;
                blocks += Sharpcraft.Client.Render.ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks: 2_000_000 - blocks, sprites: sprites);
                ranges.Add((before, buffer.IndexCount - before));
            }

            mesh = buffer.ToMeshData(blocks);
            drawRanges = [.. ranges];
        }
        else
        {
            mesh = meshSynchronously
                ? cache.Build(world, sprites, maxBlocks: 2_000_000)
                : cache.Snapshot();
            drawRanges = null;
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
        if (Environment.GetEnvironmentVariable("MC_FRAME_CAM") is { Length: > 0 } cameraText)
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
        return new FrameRequest(
            width,
            height,
            mesh.Vertices,
            mesh.Indices,
            new Vector4(0.55f, 0.72f, 0.95f, 1f),
            eye,
            target)
        {
            Atlas = atlas,
            DrawRanges = drawRanges
        };
    }

    /// <summary>Renders the world to an offscreen frame and writes a PNG.</summary>
    internal static string RenderWorld(ClientWorld world, int width, int height, BlockStateLayout? layout = null)
    {
        // Every frame path reports the same fingerprint for the same scene, so two runs can be proven comparable before
        // their pixels are compared.
        var firstChunk = world.GetChunks().Length > 0 ? world.GetChunks()[0] : null;
        Console.WriteLine($"scene: chunks {world.LoadedChunkCount}, minSectionY {firstChunk?.MinSectionY}, sections {firstChunk?.SectionCount}, " +
            $"fingerprint 0x{Sharpcraft.Client.World.SceneFingerprint.Of(world):X16}");

        if (Environment.GetEnvironmentVariable("MC_FRAME_RECORD") is { Length: > 0 } recordPath)
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
        var request = BuildRequest(
            world,
            width,
            height,
            out var mesh,
            out var drawRanges,
            prepared?.Map,
            prepared is null ? null : new TextureData(prepared.Value.Atlas.Width, prepared.Value.Atlas.Height, prepared.Value.Atlas.Pixels));
        Console.WriteLine($"mesh: {mesh.BlockCount} blocks, {mesh.TriangleCount} triangles");

        var frame = OffscreenRenderer.Render(request);

        Console.WriteLine($"rendered on adapter: {frame.AdapterName}");

        if (Environment.GetEnvironmentVariable("MC_FRAME_CULLTEST") == "1" && request.DrawRanges is { Length: > 0 } ranges)
        {
            // Two checks in one: with the natural decision the picture must not change at all, and with a forced drop it must
            // change, which proves the ranges are actually honoured rather than silently merged back into one draw.
            var forced = Environment.GetEnvironmentVariable("MC_FRAME_CULL_FORCE") == "1";
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
                var forcedIndex = Environment.GetEnvironmentVariable("MC_FRAME_CULL_FORCE_INDEX") is { Length: > 0 } indexText && int.TryParse(indexText, out var parsedIndex)
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

        if (Environment.GetEnvironmentVariable("MC_FRAME_COMPARE") is { Length: > 0 } baselinePath && File.Exists(baselinePath))
        {
            // Compare against a reference frame: zero mismatches is the only acceptable result for an optimisation that
            // claims not to change the picture, and a blend may round by one, which the tolerance covers.
            var baseline = Sharpcraft.Common.Imaging.PngDecoder.Decode(File.ReadAllBytes(baselinePath));
            var tolerance = int.TryParse(Environment.GetEnvironmentVariable("MC_FRAME_TOLERANCE"), out var parsed) ? parsed : 0;
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
        if (Environment.GetEnvironmentVariable("MC_FRAME_PROBE") is { Length: > 0 } probeText)
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

        if (Environment.GetEnvironmentVariable("MC_FRAME_CULL") == "1" && frame.Depth is { } depthPixels)
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

        if (Environment.GetEnvironmentVariable("MC_FRAME_COMPARE") is { Length: > 0 } baselinePath && File.Exists(baselinePath))
        {
            // Compare against a reference frame: zero mismatches is the only acceptable result for an optimisation that
            // claims not to change the picture, and a blend may round by one, which the tolerance covers.
            var baseline = Sharpcraft.Common.Imaging.PngDecoder.Decode(File.ReadAllBytes(baselinePath));
            var tolerance = int.TryParse(Environment.GetEnvironmentVariable("MC_FRAME_TOLERANCE"), out var parsed) ? parsed : 0;
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
