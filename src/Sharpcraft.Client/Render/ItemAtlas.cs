// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// A compact item-texture atlas. Item textures are sampled by the same sprite-rect vertex format as entity skins, but
/// kept out of the entity atlas so item model resolution and item loading can grow independently.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.model.sprite.ItemAtlas", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/ItemTextureAtlas.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Sharpcraft item atlas; no direct reference counterpart")]
public sealed class ItemAtlas
{
    private readonly Dictionary<string, SpriteRect> _sprites;

    private ItemAtlas(TextureData texture, Dictionary<string, SpriteRect> sprites, int cellWidth, int cellHeight)
    {
        Texture = texture;
        _sprites = sprites;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }

    /// <summary>The packed RGBA atlas texture.</summary>
    public TextureData Texture { get; }

    /// <summary>Cell width in pixels; every item sprite is packed into a cell of this size.</summary>
    public int CellWidth { get; }

    /// <summary>Cell height in pixels.</summary>
    public int CellHeight { get; }

    /// <summary>Sprite rect for an item texture path, or null when it was not loaded.</summary>
    public SpriteRect? Get(string? texturePath) =>
        texturePath is not null && _sprites.TryGetValue(texturePath, out var rect) ? rect : null;

    /// <summary>Builds an atlas from already decoded item textures.</summary>
    public static ItemAtlas FromTextures(IReadOnlyDictionary<string, TextureData> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            return Missing();
        }

        var cellWidth = Math.Max(16, images.Values.Max(image => image.Width));
        var cellHeight = Math.Max(16, images.Values.Max(image => image.Height));
        var columns = ChooseColumns(images.Count, cellWidth, cellHeight);
        var rows = (images.Count + columns - 1) / columns;
        var width = columns * cellWidth;
        var height = rows * cellHeight;
        var pixels = new byte[width * height * 4];
        var sprites = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
        var index = 0;
        foreach (var pair in images)
        {
            var image = pair.Value;
            var x = (index % columns) * cellWidth;
            var y = (index / columns) * cellHeight;
            for (var row = 0; row < image.Height; row++)
            {
                Buffer.BlockCopy(
                    image.Rgba,
                    row * image.Width * 4,
                    pixels,
                    (((y + row) * width) + x) * 4,
                    image.Width * 4);
            }

            sprites[pair.Key] = new SpriteRect(x, y, image.Width, image.Height, 1)
            {
                U0 = (float)x / width,
                V0 = (float)y / height,
                U1 = (float)(x + image.Width) / width,
                V1 = (float)(y + image.Height) / height
            };
            index++;
        }

        return new ItemAtlas(new TextureData(width, height, pixels), sprites, cellWidth, cellHeight);
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

    private static ItemAtlas Missing()
    {
        const int size = 16;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var offset = ((y * size) + x) * 4;
                var checker = ((x / 8) + (y / 8)) % 2 == 0;
                pixels[offset] = checker ? (byte)255 : (byte)64;
                pixels[offset + 1] = checker ? (byte)0 : (byte)64;
                pixels[offset + 2] = checker ? (byte)255 : (byte)64;
                pixels[offset + 3] = 255;
            }
        }

        return new ItemAtlas(
            new TextureData(size, size, pixels),
            new Dictionary<string, SpriteRect>(StringComparer.Ordinal),
            size,
            size);
    }

    private static int ChooseColumns(int count, int cellWidth, int cellHeight)
    {
        var best = 1;
        var bestMax = long.MaxValue;
        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var width = (long)columns * cellWidth;
            var height = (long)rows * cellHeight;
            var currentMax = Math.Max(width, height);
            if (currentMax < bestMax)
            {
                bestMax = currentMax;
                best = columns;
            }
        }

        return best;
    }
}
