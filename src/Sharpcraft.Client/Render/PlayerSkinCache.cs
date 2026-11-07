// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.AuthLib;
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
    private readonly ProfileSignatureKeySet? _signatureKeys;
    private readonly Guid? _localProfileId;
    private readonly Dictionary<Guid, (string Property, string? Signature)> _requested = [];
    private readonly Dictionary<Guid, PlayerSkinTextures> _ready = [];
    private readonly List<PendingSkin> _pending = [];

    private readonly record struct PendingSkin(Guid Id, PlayerSkinTextures Textures, Task<LoadedSkin?> Task);

    private readonly record struct LoadedSkin(PlayerSkinTextures Textures, TextureData? Body, TextureData? Cape, TextureData? Elytra);

    /// <summary>Creates a cache rooted at <paramref name="cacheDirectory"/>.</summary>
    /// <param name="cacheDirectory">Directory for downloaded texture files.</param>
    /// <param name="registerTexture">Main-thread callback that adds a decoded texture to the skin atlas.</param>
    /// <param name="download">Optional HTTP fetcher, for tests; null uses the shared <see cref="HttpClient"/>.</param>
    /// <param name="signatureKeys">Trusted profile-property keys and URL policy, or null for unverified local-only use.</param>
    /// <param name="localProfileId">Local profile UUID; remote players require a signed property when keys are supplied.</param>
    public PlayerSkinCache(
        string cacheDirectory,
        Action<string, TextureData> registerTexture,
        Func<Uri, byte[]?>? download = null,
        ProfileSignatureKeySet? signatureKeys = null,
        Guid? localProfileId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(registerTexture);
        _cacheDirectory = cacheDirectory;
        _registerTexture = registerTexture;
        _download = download ?? Download;
        _signatureKeys = signatureKeys;
        _localProfileId = localProfileId;
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
            if (entry.TexturesProperty is not { Length: > 0 } property)
            {
                continue;
            }

            if (_requested.TryGetValue(entry.Id, out var previous) &&
                string.Equals(previous.Property, property, StringComparison.Ordinal) &&
                string.Equals(previous.Signature, entry.TexturesSignature, StringComparison.Ordinal))
            {
                continue;
            }

            _requested[entry.Id] = (property, entry.TexturesSignature);
            var state = _signatureKeys?.Validate(new ProfileProperty("textures", property, entry.TexturesSignature))
                ?? ProfileSignatureState.Unsigned;
            var requireSecure = _localProfileId is { } local && entry.Id != local;
            if (requireSecure && state != ProfileSignatureState.Signed)
            {
                // The reference falls back to DefaultPlayerSkin.get(profileId); keep its slim flag here as well so
                // the cached value and the renderer agree even before the render state is built.
                _ready[entry.Id] = new PlayerSkinTextures(
                    null, null, null, DefaultPlayerSkin.Get(entry.Id).Slim, SignatureState: state);
                continue;
            }

            Func<string, bool>? isAllowedTextureUrl = _signatureKeys is null ? null : _signatureKeys.IsAllowedTextureUrl;
            if (!PlayerSkinTexturesParser.TryParse(property, isAllowedTextureUrl, out var textures))
            {
                continue;
            }

            textures = textures with { SignatureState = state };
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

            // The reference composes body, cape and elytra into one PlayerSkin future; if any requested texture
            // fails, the whole profile falls back to the default skin rather than mixing partial assets.
            var failed = (loaded.Textures.Body is not null && loaded.Body is null) ||
                         (loaded.Textures.Cape is not null && loaded.Cape is null) ||
                         (loaded.Textures.Elytra is not null && loaded.Elytra is null);
            if (failed)
            {
                _ready[pending.Id] = loaded.Textures with { Body = null, Cape = null, Elytra = null };
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

            _ready[pending.Id] = loaded.Textures;
            completed++;
        }

        return completed;
    }

    /// <summary>Looks up the textures for a profile; null until its downloads have completed.</summary>
    public PlayerSkinTextures? TryGet(Guid id) => _ready.TryGetValue(id, out var textures) ? textures : null;

    private LoadedSkin? Load(PlayerSkinTextures textures)
    {
        // Only SKIN/body textures go through the reference legacy-skin pass; cape and elytra are kept as downloaded.
        var body = textures.Body is { } bodyTexture ? LoadTexture(bodyTexture, processLegacySkin: true) : null;
        var cape = textures.Cape is { } capeTexture ? LoadTexture(capeTexture) : null;
        var elytra = textures.Elytra is { } elytraTexture ? LoadTexture(elytraTexture) : null;
        return new LoadedSkin(textures, body, cape, elytra);
    }

    private TextureData? LoadTexture(PlayerSkinTexture texture, bool processLegacySkin = false)
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
        if (image.Width <= 0 || image.Height <= 0)
        {
            return null;
        }

        if (processLegacySkin)
        {
            var converted = ProcessLegacySkin(image);
            if (converted is null)
            {
                return null;
            }

            image = converted;
        }

        return new TextureData(image.Width, image.Height, image.Pixels);
    }

    /// <summary>
    /// Reproduces the reference legacy-skin pass for SKIN/body textures: 64×32 is expanded to 64×64, the lower
    /// leg/arm regions are synthesised, and the head/body/hat alpha regions are normalised. Unsupported sizes return
    /// null so the caller drops the texture instead of registering a malformed one (docs/modules/entity-equipment.md §13).
    /// </summary>
    private static RgbaImage? ProcessLegacySkin(RgbaImage image)
    {
        if (image.Width != 64 || (image.Height != 32 && image.Height != 64))
        {
            return null;
        }

        var isLegacy = image.Height == 32;
        byte[] pixels;
        if (isLegacy)
        {
            // The reference allocates a zeroed 64×64 image and copyFrom copies only the source's first 64*32*4 bytes;
            // fillRect(0,32,64,32,0) is already satisfied by the zeroed tail.
            pixels = new byte[64 * 64 * 4];
            Array.Copy(image.Pixels, pixels, image.Pixels.Length);

            CopyRect(pixels, 4, 16, 16, 32, 4, 4);
            CopyRect(pixels, 8, 16, 16, 32, 4, 4);
            CopyRect(pixels, 0, 20, 24, 32, 4, 12);
            CopyRect(pixels, 4, 20, 16, 32, 4, 12);
            CopyRect(pixels, 8, 20, 8, 32, 4, 12);
            CopyRect(pixels, 12, 20, 16, 32, 4, 12);
            CopyRect(pixels, 44, 16, -8, 32, 4, 4);
            CopyRect(pixels, 48, 16, -8, 32, 4, 4);
            CopyRect(pixels, 40, 20, 0, 32, 4, 12);
            CopyRect(pixels, 44, 20, -8, 32, 4, 12);
            CopyRect(pixels, 48, 20, -16, 32, 4, 12);
            CopyRect(pixels, 52, 20, -8, 32, 4, 12);
        }
        else
        {
            pixels = (byte[])image.Pixels.Clone();
        }

        SetNoAlpha(pixels, 0, 0, 32, 16);
        if (isLegacy)
        {
            DoNotchTransparencyHack(pixels, 32, 0, 64, 32);
        }

        SetNoAlpha(pixels, 0, 16, 64, 32);
        SetNoAlpha(pixels, 16, 48, 48, 64);
        return new RgbaImage(64, 64, pixels);
    }

    /// <summary>
    /// Reference <c>NativeImage.copyRect(startX, startY, offsetX, offsetY, sizeX, sizeY, swapX, swapY)</c>: the third
    /// and fourth arguments are offsets from the source origin, not absolute coordinates. All legacy-skin copies use
    /// swapX=true / swapY=false and copy the raw four-channel pixel, so no channel reordering is needed.
    /// </summary>
    private static void CopyRect(byte[] pixels, int startX, int startY, int offsetX, int offsetY, int sizeX, int sizeY)
    {
        var targetX = startX + offsetX;
        var targetY = startY + offsetY;
        for (var y = 0; y < sizeY; y++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                var dx = sizeX - 1 - x;
                var source = (((startY + y) * 64) + startX + x) * 4;
                var target = (((targetY + y) * 64) + targetX + dx) * 4;
                for (var channel = 0; channel < 4; channel++)
                {
                    pixels[target + channel] = pixels[source + channel];
                }
            }
        }
    }

    private static void DoNotchTransparencyHack(byte[] pixels, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (pixels[(((y * 64) + x) * 4) + 3] < 128)
                {
                    return;
                }
            }
        }

        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                pixels[(((y * 64) + x) * 4) + 3] = 0;
            }
        }
    }

    private static void SetNoAlpha(byte[] pixels, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                pixels[(((y * 64) + x) * 4) + 3] = 255;
            }
        }
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
