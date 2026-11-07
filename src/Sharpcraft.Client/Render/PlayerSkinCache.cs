// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Downloads and caches the cape/elytra textures named by player profile properties, then publishes the resulting
/// paths to the renderer. The reference uses <c>SkinManager</c> and an HTTP texture cache for the same data; this type
/// keeps that work off the frame thread and registers images into the entity skin atlas on the main thread.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.SkinManager", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/SkinManager.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Async texture download/cache; atlas registration stays host-side")]
public sealed class PlayerSkinCache
{
    private readonly string _cacheDirectory;
    private readonly Action<string, TextureData> _registerTexture;
    private readonly Func<Uri, byte[]?> _download;
    private readonly Dictionary<Guid, string> _requested = [];
    private readonly Dictionary<Guid, PlayerSkinTextures> _ready = [];
    private readonly List<PendingSkin> _pending = [];

    private readonly record struct PendingSkin(Guid Id, PlayerSkinTextures Textures, Task<LoadedSkin?> Task);

    private readonly record struct LoadedSkin(PlayerSkinTextures Textures, TextureData? Body, TextureData? Cape, TextureData? Elytra);

    /// <summary>Creates a cache rooted at <paramref name="cacheDirectory"/>.</summary>
    /// <param name="cacheDirectory">Directory for downloaded texture files.</param>
    /// <param name="registerTexture">Main-thread callback that adds a decoded texture to the skin atlas.</param>
    /// <param name="download">Optional HTTP fetcher, for tests; null uses the shared <see cref="HttpClient"/>.</param>
    public PlayerSkinCache(string cacheDirectory, Action<string, TextureData> registerTexture, Func<Uri, byte[]?>? download = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(registerTexture);
        _cacheDirectory = cacheDirectory;
        _registerTexture = registerTexture;
        _download = download ?? Download;
    }

    /// <summary>Default cache directory; mirrors the reference's assets-dir <c>skins</c> folder when one is supplied.</summary>
    /// <param name="assetsDirectory">Official assets root, or null to use the per-user Sharpcraft directory.</param>
    public static string DefaultCacheDirectory(string? assetsDirectory = null) =>
        string.IsNullOrWhiteSpace(assetsDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sharpcraft",
                "skins")
            : Path.Combine(assetsDirectory, "skins");

    /// <summary>Starts loads for newly seen profile texture properties. Call once per frame on the main thread.</summary>
    public void Sync(ReadOnlySpan<PlayerListEntry> entries)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.TexturesProperty is not { Length: > 0 } property ||
                (_requested.TryGetValue(entry.Id, out var previous) && string.Equals(previous, property, StringComparison.Ordinal)))
            {
                continue;
            }

            _requested[entry.Id] = property;
            if (!PlayerSkinTexturesParser.TryParse(property, out var textures))
            {
                continue;
            }

            if (textures.Body is null && textures.Cape is null && textures.Elytra is null)
            {
                _ready[entry.Id] = textures;
                continue;
            }

            _pending.Add(new PendingSkin(entry.Id, textures, Task.Run(() => Load(textures))));
        }
    }

    /// <summary>Integrates finished downloads on the main thread and returns how many profiles became ready.</summary>
    public int Pump()
    {
        var completed = 0;
        for (var index = _pending.Count - 1; index >= 0; index--)
        {
            var pending = _pending[index];
            if (!pending.Task.IsCompleted)
            {
                continue;
            }

            _pending.RemoveAt(index);
            if (pending.Task.Status != TaskStatus.RanToCompletion || pending.Task.Result is not { } loaded)
            {
                continue;
            }

            if (loaded.Body is not null && loaded.Textures.Body is { } body)
            {
                _registerTexture(body.Path, loaded.Body);
            }

            if (loaded.Cape is not null && loaded.Textures.Cape is { } cape)
            {
                _registerTexture(cape.Path, loaded.Cape);
            }

            if (loaded.Elytra is not null && loaded.Textures.Elytra is { } elytra)
            {
                _registerTexture(elytra.Path, loaded.Elytra);
            }

            // A failed download must not point the renderer at a path the atlas does not contain; drop only the
            // texture that failed so the layer falls back to the equipment texture, like the reference default skin.
            _ready[pending.Id] = loaded.Textures with
            {
                Body = loaded.Body is null ? null : loaded.Textures.Body,
                Cape = loaded.Cape is null ? null : loaded.Textures.Cape,
                Elytra = loaded.Elytra is null ? null : loaded.Textures.Elytra
            };
            completed++;
        }

        return completed;
    }

    /// <summary>Looks up the textures for a profile; null until its downloads have completed.</summary>
    public PlayerSkinTextures? TryGet(Guid id) => _ready.TryGetValue(id, out var textures) ? textures : null;

    private LoadedSkin? Load(PlayerSkinTextures textures)
    {
        var body = textures.Body is { } bodyTexture ? LoadTexture(bodyTexture) : null;
        var cape = textures.Cape is { } capeTexture ? LoadTexture(capeTexture) : null;
        var elytra = textures.Elytra is { } elytraTexture ? LoadTexture(elytraTexture) : null;
        return new LoadedSkin(textures, body, cape, elytra);
    }

    private TextureData? LoadTexture(PlayerSkinTexture texture)
    {
        var path = texture.Path;
        var slash = path.LastIndexOf('/');
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        var directory = Path.Combine(_cacheDirectory, name.Length > 2 ? name[..2] : "xx");
        var file = Path.Combine(directory, name);

        byte[]? contents = null;
        if (File.Exists(file))
        {
            try
            {
                contents = File.ReadAllBytes(file);
            }
            catch (IOException)
            {
                contents = null;
            }
            catch (UnauthorizedAccessException)
            {
                contents = null;
            }
        }

        contents ??= _download(new Uri(texture.Url));
        if (contents is null)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(file, contents);
        }
        catch (IOException)
        {
            // A read-only cache directory must not prevent rendering; the downloaded bytes are still used.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: the cache is an optimisation, not a rendering prerequisite.
        }

        var image = PngDecoder.Decode(contents);
        return image.Width > 0 && image.Height > 0
            ? new TextureData(image.Width, image.Height, image.Pixels)
            : null;
    }

    private static byte[]? Download(Uri uri)
    {
        try
        {
            return SharedHttpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
}
