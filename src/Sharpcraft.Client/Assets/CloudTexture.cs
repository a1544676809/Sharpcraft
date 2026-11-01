// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// The cloud texture (textures/environment/clouds.png) packed into per-cell data: colour plus four neighbour-empty flags,
/// exactly like CloudRenderer's prepare step (docs/modules/clouds.md 2). Alpha below the cell threshold means no cloud.
/// </summary>
public sealed class CloudTexture
{
    private CloudTexture(int width, int height, long[] cells)
    {
        Width = width;
        Height = height;
        Cells = cells;
    }

    /// <summary>Cloud texture width in cells.</summary>
    public int Width { get; }

    /// <summary>Cloud texture height in cells.</summary>
    public int Height { get; }

    /// <summary>Packed cells, indexed <c>x + y*Width</c>.</summary>
    public long[] Cells { get; }

    /// <summary>Loads the cloud texture from the resource stack, or null when the pack has none.</summary>
    public static CloudTexture? Load(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", "environment/clouds"));
        if (data is null)
        {
            return null;
        }

        var image = PngDecoder.Decode(data);
        if (image.Width <= 0 || image.Height <= 0)
        {
            return null;
        }

        var pixels = new int[image.Width * image.Height];
        for (var index = 0; index < pixels.Length; index++)
        {
            var offset = index * 4;
            pixels[index] = (image.Pixels[offset + 3] << 24)
                | (image.Pixels[offset] << 16)
                | (image.Pixels[offset + 1] << 8)
                | image.Pixels[offset + 2];
        }

        return FromPixels(image.Width, image.Height, pixels);
    }

    /// <summary>Builds the packed cells from ARGB pixels (used for tests and the loader).</summary>
    public static CloudTexture FromPixels(int width, int height, int[] argb)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(argb);
        if (argb.Length != width * height)
        {
            throw new ArgumentException("pixel count does not match size", nameof(argb));
        }

        var cells = new long[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = argb[x + (y * width)];
                if (Alpha(color) < Client.Render.CloudMeshBuilder.EmptyAlphaThreshold)
                {
                    continue;
                }

                var north = EmptyAt(argb, width, height, x, y - 1);
                var east = EmptyAt(argb, width, height, x + 1, y);
                var south = EmptyAt(argb, width, height, x, y + 1);
                var west = EmptyAt(argb, width, height, x - 1, y);
                cells[x + (y * width)] = Client.Render.CloudMeshBuilder.PackCell(color, north, east, south, west);
            }
        }

        return new CloudTexture(width, height, cells);
    }

    private static bool EmptyAt(int[] pixels, int width, int height, int x, int y)
    {
        var wrappedX = ((x % width) + width) % width;
        var wrappedY = ((y % height) + height) % height;
        return Alpha(pixels[wrappedX + (wrappedY * width)]) < Client.Render.CloudMeshBuilder.EmptyAlphaThreshold;
    }

    private static int Alpha(int color) => (color >> 24) & 0xFF;
}
