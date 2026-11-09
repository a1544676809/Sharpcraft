// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// The item/model sprite atlas. A facade over <see cref="SpriteAtlasBuilder"/>, so items share the single stitching
/// path the block atlas uses (padding around every sprite, real sprite sizes instead of a uniform grid, immutable
/// atlas objects). The reference does the same: one <c>SpriteLoader</c> and <c>Stitcher</c> feed every atlas, and the
/// sprite objects it produces carry the texture coordinates of the atlas they were stitched into
/// (docs/modules/item-rendering.md 3.5.13).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.sprite.ItemAtlas", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/ItemTextureAtlas.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Sharpcraft item atlas over the shared SpriteAtlasBuilder; no direct reference counterpart")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-ATLAS-VALIDATION", "T-631e", Note = "Synthetic packing tests only; real resource-pack UV validation is pending")]
public sealed class ItemAtlas : ISpriteAtlas{
    private readonly Dictionary<string, SpriteRect> _sprites;
    private readonly Dictionary<string, TextureData> _images;
    private readonly Dictionary<string, bool> _translucent;

    private ItemAtlas(
        TextureData texture,
        Dictionary<string, SpriteRect> sprites,
        Dictionary<string, TextureData> images,
        Dictionary<string, bool> translucent,
        int cellWidth,
        int cellHeight)
    {
        Texture = texture;
        _sprites = sprites;
        _images = images;
        _translucent = translucent;

        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }

    /// <summary>The packed RGBA atlas texture.</summary>
    public TextureData Texture { get; }

    /// <summary>
    /// Widest packed sprite in pixels. Informational only: sprites are packed at their own size with padding, so this
    /// is not a cell pitch any more.
    /// </summary>
    public int CellWidth { get; }

    /// <summary>Tallest packed sprite in pixels; informational, as <see cref="CellWidth"/>.</summary>
    public int CellHeight { get; }

    /// <summary>
    /// True when this sprite contains any pixel with partial alpha (0 &lt; a &lt; 255), i.e. the reference
    /// <c>NativeImage.computeTransparency</c> test that selects the translucent item sheet.
    /// </summary>
    public bool IsTranslucent(string? texturePath) =>
        texturePath is not null && _translucent.TryGetValue(texturePath, out var translucent) && translucent;

    /// <summary>Where one packed texture lives, or null when the atlas does not hold it.</summary>
    public SpriteRect? Get(string? texturePath) =>
        texturePath is not null && _sprites.TryGetValue(texturePath, out var sprite) ? sprite : null;

    /// <summary>Builds the atlas from the given textures, stitching them through the shared sprite builder.</summary>
    public static ItemAtlas FromTextures(IReadOnlyDictionary<string, TextureData> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            return Missing();
        }

        var entries = new List<(Identifier Location, RgbaImage Image, int Frames)>(images.Count + 1);
        foreach (var pair in images)
        {
            entries.Add((
                new Identifier("minecraft", pair.Key),
                new RgbaImage(pair.Value.Width, pair.Value.Height, pair.Value.Rgba),
                1));
        }

        // Stitch expects the missing sprite in the list, exactly as the block atlas builder supplies it.
        var fallback = PngDecoder.Decode(new BuiltInFallbackSource().Checkerboard);
        entries.Add((SpriteAtlas.MissingSprite, new RgbaImage(fallback.Width, fallback.Height, fallback.Pixels), 1));

        var atlas = SpriteAtlasBuilder.Stitch(entries);

        var sprites = new Dictionary<string, SpriteRect>(images.Count, StringComparer.Ordinal);
        var imageCopy = new Dictionary<string, TextureData>(images.Count, StringComparer.Ordinal);
        var translucent = new Dictionary<string, bool>(images.Count, StringComparer.Ordinal);
        var cellWidth = 0;
        var cellHeight = 0;
        foreach (var pair in images)
        {
            var rect = atlas.Get(new Identifier("minecraft", pair.Key));
            sprites[pair.Key] = rect;
            imageCopy[pair.Key] = pair.Value;
            translucent[pair.Key] = HasTranslucentPixel(pair.Value);
            cellWidth = Math.Max(cellWidth, rect.Width);
            cellHeight = Math.Max(cellHeight, rect.Height);
        }

        return new ItemAtlas(
            new TextureData(atlas.Width, atlas.Height, atlas.Pixels),
            sprites,
            imageCopy,
            translucent,
            cellWidth,
            cellHeight);
    }

    /// <summary>Loads the requested item texture paths through the resource manager.</summary>
    public static ItemAtlas Load(ResourceManager resources, IEnumerable<string> texturePaths)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(texturePaths);
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in texturePaths)
        {
            var bytes = resources.Read(ResourceKind.Texture, new Identifier("minecraft", path));
            if (bytes is null)
            {
                continue;
            }

            var image = PngDecoder.Decode(bytes);
            images[path] = new TextureData(image.Width, image.Height, image.Pixels);
        }

        return FromTextures(images);
    }

    /// <summary>Whether a texture path is already packed.</summary>
    public bool Contains(string? texturePath) => texturePath is not null && _sprites.ContainsKey(texturePath);

    /// <summary>
    /// Pixels of one packed texture, or null when the atlas does not hold it. Generated item models need this to scan
    /// the sprite border and decide which side faces to extrude.
    /// </summary>
    public TextureData? GetTexture(string? texturePath) =>
        texturePath is not null && _images.TryGetValue(texturePath, out var image) ? image : null;

    /// <summary>
    /// Returns a new atlas with one additional texture, or this atlas when it already exists. A rebuild produces a new
    /// atlas object, so anything holding sprite coordinates from the previous one must re-resolve them; that is the
    /// reference's reload behaviour (new sprites, models re-baked) expressed as an immutable value.
    /// </summary>
    public ItemAtlas WithTexture(string texturePath, TextureData image)
    {
        ArgumentException.ThrowIfNullOrEmpty(texturePath);
        ArgumentNullException.ThrowIfNull(image);
        if (_sprites.ContainsKey(texturePath))
        {
            return this;
        }

        var images = new Dictionary<string, TextureData>(_images, StringComparer.Ordinal)
        {
            [texturePath] = image
        };
        return FromTextures(images);
    }

    /// <summary>Reference <c>NativeImage.computeTransparency</c>: any 0 &lt; alpha &lt; 255 pixel is translucent.</summary>
    private static bool HasTranslucentPixel(TextureData image)
    {
        var rgba = image.Rgba;
        for (var i = 3; i < rgba.Length; i += 4)
        {
            var alpha = rgba[i];
            if (alpha is > 0 and < 255)
            {
                return true;
            }
        }

        return false;
    }

    private static ItemAtlas Missing()
    {
        const int size = 16;
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xF0;
            pixels[i + 1] = 0x00;
            pixels[i + 2] = 0xF0;
            pixels[i + 3] = 0xFF;
        }

        var rect = new SpriteRect(0, 0, size, size, 1)
        {
            U0 = 0f,
            V0 = 0f,
            U1 = 1f,
            V1 = 1f
        };
        return new ItemAtlas(
            new TextureData(size, size, pixels),
            new Dictionary<string, SpriteRect>(StringComparer.Ordinal),
            new Dictionary<string, TextureData>(StringComparer.Ordinal),
            new Dictionary<string, bool>(StringComparer.Ordinal),
            rect.Width,
            rect.Height);
    }
}
