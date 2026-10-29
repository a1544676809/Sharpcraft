// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Assets;

/// <summary>Where one sprite lives inside the atlas, in pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Sprite width in pixels.</param>
/// <param name="Height">Sprite height of a single frame.</param>
/// <param name="FrameCount">Frames in the source image; 1 for a still texture.</param>
public sealed record SpriteRect(int X, int Y, int Width, int Height, int FrameCount)
{
    /// <summary>Left texture coordinate.</summary>
    public float U0 { get; init; }

    /// <summary>Right texture coordinate.</summary>
    public float U1 { get; init; }

    /// <summary>Top texture coordinate.</summary>
    public float V0 { get; init; }

    /// <summary>Bottom texture coordinate.</summary>
    public float V1 { get; init; }

    /// <summary>Formats the rect for diagnostics.</summary>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0},{1} {2}x{3} frames {4}",
        X,
        Y,
        Width,
        Height,
        FrameCount);
}

/// <summary>
/// A stitched texture atlas plus the lookup from resource location to sprite. Vanilla builds this
/// in the client from the resource stack (SpriteLoader + TextureAtlas), which is why a resource pack
/// only needs the atlas rebuilt (ADR-0007).
/// </summary>
public sealed class SpriteAtlas
{
    private readonly Dictionary<Identifier, SpriteRect> _sprites;

    internal SpriteAtlas(int width, int height, byte[] pixels, Dictionary<Identifier, SpriteRect> sprites)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        _sprites = sprites;
    }

    /// <summary>Atlas width in pixels; a power of two, as vanilla's stitcher produces.</summary>
    public int Width { get; }

    /// <summary>Atlas height in pixels.</summary>
    public int Height { get; }

    /// <summary>RGBA pixels, <see cref="Width"/> * <see cref="Height"/> * 4 bytes.</summary>
    public byte[] Pixels { get; }

    /// <summary>Every sprite in the atlas.</summary>
    public IReadOnlyDictionary<Identifier, SpriteRect> Sprites => _sprites;

    /// <summary>The sprite used for anything missing; always present.</summary>
    public static Identifier MissingSprite { get; } = new("minecraft", "missingno");

    /// <summary>Looks up a sprite, falling back to the missing sprite like the vanilla atlas.</summary>
    public SpriteRect Get(Identifier location) =>
        _sprites.TryGetValue(location, out var sprite) ? sprite : _sprites[MissingSprite];

    /// <summary>True when the atlas actually carries this sprite.</summary>
    public bool Contains(Identifier location) => _sprites.ContainsKey(location);
}

/// <summary>
/// Builds the block atlas the way vanilla does in spirit: discover every sprite from the resource
/// stack, pack them with their real sizes (not a uniform grid), and record texture coordinates.
/// </summary>
public static class SpriteAtlasBuilder
{
    /// <summary>Animated textures are vertical frame strips; only the first frame enters the atlas.</summary>
    public const string BlockTexturePrefix = "assets/minecraft/textures/block/";

    /// <summary>Largest atlas the builder will grow to before scaling sprites down.</summary>
    public const int MaxAtlasSize = 8192;

    /// <summary>
    /// Builds an atlas from every <c>textures/block</c> sprite in the stack, plus the missing sprite.
    /// </summary>
    /// <param name="stack">Resource stack; higher sources win, as in the vanilla pack order.</param>
    public static SpriteAtlas Build(AssetStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        var sprites = new List<(Identifier Location, RgbaImage Image, int Frames)>();
        var seen = new HashSet<Identifier>();

        foreach (var location in stack.ListAll(BlockTexturePrefix))
        {
            if (!seen.Add(location))
            {
                continue;
            }

            var bytes = stack.Read(ResourceKind.Texture, location);
            if (bytes is null)
            {
                continue;
            }

            var image = PngDecoder.Decode(bytes);
            var frames = Math.Max(1, image.Height / image.Width);
            sprites.Add((location, TakeFirstFrame(image, frames), frames));
        }

        var missing = PngDecoder.Decode(new BuiltInFallbackSource().Checkerboard);
        sprites.Add((SpriteAtlas.MissingSprite, missing, 1));

        return Stitch(sprites);
    }

    /// <summary>Packs the given sprites into a power-of-two atlas.</summary>
    /// <param name="sprites">Sprites to pack; the missing sprite is expected to be present.</param>
    public static SpriteAtlas Stitch(IReadOnlyList<(Identifier Location, RgbaImage Image, int Frames)> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        // A 16x512 texture is 32 frames stacked vertically; only the first frame is stitched.
        var firstFrames = sprites
            .Select(sprite => (sprite.Location, Image: TakeFirstFrame(sprite.Image, sprite.Frames), sprite.Frames))
            .ToList();

        // Vanilla's stitcher sorts by height then width, so tall sprites claim rows first.
        var ordered = firstFrames
            .OrderByDescending(sprite => sprite.Image.Height)
            .ThenByDescending(sprite => sprite.Image.Width)
            .ThenBy(sprite => sprite.Location.ToString(), StringComparer.Ordinal)
            .ToList();

        var size = NextPowerOfTwo(ordered.Max(sprite => Math.Max(sprite.Image.Width, sprite.Image.Height)));

        while (true)
        {
            if (TryShelfPack(ordered, size, out var placements))
            {
                return Compose(ordered, placements, size);
            }

            if (size >= MaxAtlasSize)
            {
                throw new InvalidOperationException(
                    $"cannot fit {ordered.Count} sprites into a {MaxAtlasSize}x{MaxAtlasSize} atlas");
            }

            size *= 2;
        }
    }

    /// <summary>
    /// Shelf packing: sprites of one row share the row height. Deterministic for a given input order,
    /// which keeps the atlas stable across runs and therefore comparable in tests.
    /// </summary>
    private static bool TryShelfPack(
        List<(Identifier Location, RgbaImage Image, int Frames)> ordered,
        int size,
        out List<(int X, int Y)> placements)
    {
        placements = new List<(int, int)>(ordered.Count);
        var x = 0;
        var y = 0;
        var shelfHeight = 0;

        foreach (var sprite in ordered)
        {
            var width = sprite.Image.Width;
            var height = sprite.Image.Height;

            if (width > size || height > size)
            {
                return false;
            }

            if (x + width > size)
            {
                y += shelfHeight;
                x = 0;
                shelfHeight = 0;
            }

            if (y + height > size)
            {
                return false;
            }

            placements.Add((x, y));
            x += width;
            shelfHeight = Math.Max(shelfHeight, height);
        }

        return true;
    }

    private static SpriteAtlas Compose(
        List<(Identifier Location, RgbaImage Image, int Frames)> ordered,
        List<(int X, int Y)> placements,
        int size)
    {
        var pixels = new byte[size * size * RgbaImage.BytesPerPixel];
        var index = new Dictionary<Identifier, SpriteRect>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var (location, image, frames) = ordered[i];
            var (x, y) = placements[i];

            for (var row = 0; row < image.Height; row++)
            {
                var source = row * image.Width * RgbaImage.BytesPerPixel;
                var target = (((y + row) * size) + x) * RgbaImage.BytesPerPixel;
                Buffer.BlockCopy(
                    image.Pixels,
                    source,
                    pixels,
                    target,
                    image.Width * RgbaImage.BytesPerPixel);
            }

            // Half-texel inset: sampling exactly on a cell edge would let nearest-neighbour pick a
            // texel from the neighbouring sprite, so the UV range stops just inside the cell.
            const float HalfTexel = 0.5f;
            // V0 names the sprite's top row because row zero of the atlas is its top row, and the mesh emits v = 0
            // at the top edge of a face, following the reference model baker.
            index[location] = new SpriteRect(x, y, image.Width, image.Height, frames)
            {
                U0 = (x + HalfTexel) / size,
                U1 = (x + image.Width - HalfTexel) / size,
                V0 = (y + HalfTexel) / size,
                V1 = (y + image.Height - HalfTexel) / size
            };
        }

        return new SpriteAtlas(size, size, pixels, index);
    }

    private static RgbaImage TakeFirstFrame(RgbaImage image, int frames)
    {
        if (frames <= 1)
        {
            return image;
        }

        var framePixels = new byte[image.Width * image.Width * RgbaImage.BytesPerPixel];
        Buffer.BlockCopy(image.Pixels, 0, framePixels, 0, framePixels.Length);
        return new RgbaImage(image.Width, image.Width, framePixels);
    }

    private static int NextPowerOfTwo(int value)
    {
        var size = 1;
        while (size < value)
        {
            size *= 2;
        }

        return Math.Max(size, 16);
    }
}
