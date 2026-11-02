// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// A tiny vertical atlas of entity skins. Every cell is a 64x64 logical skin; the mesh uses the rect for its model's
/// TexturePath, so player and zombie can share one texture binding. This is intentionally separate from the block atlas.
/// </summary>
public sealed class EntitySkinAtlas
{
    private readonly Dictionary<string, SpriteRect> _sprites;
    private EntitySkinAtlas(TextureData texture, Dictionary<string, SpriteRect> sprites, int cellWidth, int cellHeight)
    {
        Texture = texture;
        _sprites = sprites;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }

    public TextureData Texture { get; }
    public int CellWidth { get; }
    public int CellHeight { get; }

    public SpriteRect? Get(string? texturePath) =>
        texturePath is not null && _sprites.TryGetValue(texturePath, out var rect) ? rect : null;

    public static EntitySkinAtlas FromTextures(IReadOnlyDictionary<string, TextureData> images)
    {
        if (images.Count == 0)
        {
            throw new ArgumentException("at least one skin is required", nameof(images));
        }

        var cellWidth = images.Values.Max(image => image.Width);
        var cellHeight = images.Values.Max(image => image.Height);
        var height = cellHeight * images.Count;
        var pixels = new byte[cellWidth * height * 4];
        var sprites = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
        var row = 0;
        foreach (var pair in images)
        {
            var image = pair.Value;
            for (var y = 0; y < image.Height; y++)
            {
                Buffer.BlockCopy(image.Rgba, y * image.Width * 4, pixels, ((row * cellHeight + y) * cellWidth) * 4, image.Width * 4);
            }

            sprites[pair.Key] = new SpriteRect(0, row * cellHeight, 64, 64, 1)
            {
                U0 = 0f,
                V0 = (float)(row * cellHeight) / height,
                U1 = (float)image.Width / cellWidth,
                V1 = (float)((row * cellHeight) + image.Height) / height
            };
            row++;
        }

        return new EntitySkinAtlas(new TextureData(cellWidth, height, pixels), sprites, cellWidth, cellHeight);
    }

    public static EntitySkinAtlas? Load(ResourceManager resources, IReadOnlyList<string> paths)
    {
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", path));
            if (data is null)
            {
                continue;
            }

            var image = PngDecoder.Decode(data);
            images[path] = new TextureData(image.Width, image.Height, image.Pixels);
        }

        return images.Count == 0 ? null : FromTextures(images);
    }
}
