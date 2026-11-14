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

internal static partial class FrameCommand
{
    /// <summary>Prefix of the item tag data extracted for the client's tag registry.</summary>
    internal const string ItemTagsPrefix = "data/minecraft/tags/item/";


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

    internal static (SpriteAtlas Atlas, BlockSpriteMap Map, Sharpcraft.Client.Render.BlockModelSet? Models)? PrepareSprites(
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

        var modelLibrary = new Sharpcraft.Client.Assets.BlockModelLibrary(
            models.ResolveModel,
            Sharpcraft.Client.Assets.BlockModelElements.FromStack(stack),
            models.ResolveTexture);
        var modelSet = Sharpcraft.Client.Render.BlockModelSet.Build(modelLibrary, layout!);

        return (atlas, map, modelSet);
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
}
