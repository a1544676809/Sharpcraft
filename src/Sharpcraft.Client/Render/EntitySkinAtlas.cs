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
[Sharpcraft.Common.Reference.ReferenceClass("sharpcraft.EntitySkinAtlas", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/TextureAtlas.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.New, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full, Note = "Our 2D-grid skin atlas builder; no direct reference counterpart")]
public sealed class EntitySkinAtlas : ISpriteAtlas
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

        // Lay the skins out on a 2D grid instead of a single vertical column: stacking every skin vertically grows the
        // atlas past the GPU maximum texture height (16384) once enough mobs are registered. We pick the column count
        // that keeps the larger atlas dimension smallest.
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
                Buffer.BlockCopy(image.Rgba, row * image.Width * 4, pixels, (((y + row) * width) + x) * 4, image.Width * 4);
            }

            var logicalWidth = image.Width == 1600 ? 64 : image.Width;
            var logicalHeight = image.Height == 1600 ? 64 : image.Height;
            sprites[pair.Key] = new SpriteRect(x, y, logicalWidth, logicalHeight, 1)
            {
                U0 = (float)x / width,
                V0 = (float)y / height,
                U1 = (float)(x + image.Width) / width,
                V1 = (float)(y + image.Height) / height
            };
            index++;
        }

        return new EntitySkinAtlas(new TextureData(width, height, pixels), sprites, cellWidth, cellHeight);
    }

    /// <summary>Chooses the grid column count that minimises the larger atlas dimension.</summary>
    private static int ChooseColumns(int count, int cellWidth, int cellHeight)
    {
        var best = 1;
        long bestMax = long.MaxValue;
        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            long width = (long)columns * cellWidth;
            long height = (long)rows * cellHeight;
            var currentMax = Math.Max(width, height);
            if (currentMax < bestMax)
            {
                bestMax = currentMax;
                best = columns;
            }
        }

        return best;
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
