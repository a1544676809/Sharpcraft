// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<(int X, int Y, int Width, int Height), Transparency> _transparency = new();

    /// <summary>
    /// Alpha content of a sprite, computed once per rectangle.
    /// </summary>
    /// <param name="sprite">Sprite rectangle in atlas pixels.</param>
    /// <remarks>
    /// The reference scans the quad's UV sub-rectangle and flags a fully transparent pixel as cut out and a partially
    /// transparent one as needing blending
    /// (ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/NativeImage.java, computeTransparency).
    /// </remarks>
    public Transparency ComputeTransparency(SpriteRect sprite)
    {
        // Chunk meshing runs on the worker threads, so this cache is hit concurrently. A plain dictionary threw a
        // collection corrupted error under that load and took the device down with it.
        var key = (sprite.X, sprite.Y, sprite.Width, sprite.Height);

        return _transparency.GetOrAdd(key, _ => SpriteAlpha.Compute(Pixels, Width, sprite));
    }
}

/// <summary>
/// Builds the block atlas the way vanilla does in spirit: discover every sprite from the resource
/// stack, pack them with their real sizes (not a uniform grid), and record texture coordinates.
/// </summary>
public static class SpriteAtlasBuilder
{
    /// <summary>Texels of edge padding around every sprite.</summary>
    private const int Padding = 1;

    /// <summary>Animated textures are vertical frame strips; only the first frame enters the atlas.</summary>
    public const string BlockTexturePrefix = "assets/minecraft/textures/block/";

    /// <summary>Largest atlas the builder will grow to before scaling sprites down.</summary>
    public const int MaxAtlasSize = 8192;

    /// <summary>
    /// Builds an atlas from every <c>textures/block</c> sprite in the stack, plus the missing sprite.
    /// </summary>
    /// <param name="stack">Resource stack; higher sources win, as in the vanilla pack order.</param>
    private static readonly SpriteImageCache ImageCache = new();

    public static SpriteAtlas Build(AssetStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        var sprites = new List<(Identifier Location, RgbaImage Image, int Frames)>();
        var sectionClock = System.Diagnostics.Stopwatch.StartNew();
        var decodeMs = 0d;
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

            // Only changed files are decoded; a reload that recolours one sprite should not decode thirteen hundred.
            sectionClock.Restart();
            var image = ImageCache.GetOrDecode(location, bytes, data => PngDecoder.Decode(data));
            decodeMs += sectionClock.Elapsed.TotalMilliseconds;

            // The frame size is declared, not guessed: a square of the image width is only the common case.
            var declared = AnimationFrameSize.TryRead(stack.ReadMetadata(ResourceKind.Texture, location));
            var frameWidth = declared?.Width ?? image.Width;
            var frameHeight = declared?.Height ?? image.Width;

            if (frameWidth <= 0 || frameHeight <= 0 ||
                image.Width % frameWidth != 0 || image.Height % frameHeight != 0)
            {
                Console.WriteLine($"sprites: {location} declares a frame of {frameWidth}x{frameHeight} that does not tile; using one frame");
                frameWidth = image.Width;
                frameHeight = image.Height;
            }

            // The frame count is what Stitch needs; the declared frame width is used when it is narrower than the sheet.
            var frames = Math.Max(1, image.Height / frameHeight);

            if (frameWidth != image.Width)
            {
                Console.WriteLine($"sprites: {location} declares a frame {frameWidth} wide in a {image.Width} wide sheet; using the sheet width");
            }

            sprites.Add((location, image, frames));
        }

        var missing = PngDecoder.Decode(new BuiltInFallbackSource().Checkerboard);
        sprites.Add((SpriteAtlas.MissingSprite, missing, 1));

        sectionClock.Restart();
        var atlas = Stitch(sprites);
        var stitchMs = sectionClock.Elapsed.TotalMilliseconds;
        Console.WriteLine($"sprites: build decode {decodeMs:F1} ms, stitch {stitchMs:F1} ms, {sprites.Count} sprites");
        return atlas;
    }

    /// <summary>Packs the given sprites into a power-of-two atlas.</summary>
    /// <param name="sprites">Sprites to pack; the missing sprite is expected to be present.</param>
    public static SpriteAtlas Stitch(IReadOnlyList<(Identifier Location, RgbaImage Image, int Frames)> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        // A 16x512 texture is 32 frames stacked vertically; only the first frame is stitched.
        // Frames stack vertically, so the first frame is the top Height/frames rows; that holds for any frame aspect,
        // including the non-square frames a pack may declare.
        var firstFrames = sprites
            .Select(sprite =>
            {
                var frameHeight = sprite.Frames > 1 ? sprite.Image.Height / sprite.Frames : sprite.Image.Height;
                return (sprite.Location, Image: TakeFirstFrame(sprite.Image, sprite.Image.Width, frameHeight), sprite.Frames);
            })
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
            // One texel of padding on every side, filled by repeating the sprite's edge. Linear filtering and mip levels
            // read a texel outside the sprite rectangle at its border; without the padding that neighbour is an unrelated
            // sprite, which showed up as black seams along block edges.
            var width = sprite.Image.Width + (Padding * 2);
            var height = sprite.Image.Height + (Padding * 2);

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

            placements.Add((x + Padding, y + Padding));
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

            // Repeat the edge texels into the padding ring, so any sample just outside the sprite reads the sprite itself.
            for (var pad = 1; pad <= Padding; pad++)
            {
                for (var column = 0; column < image.Width; column++)
                {
                    var top = (((y * size) + x + column) * RgbaImage.BytesPerPixel);
                    var topTarget = ((((y - pad) * size) + x + column) * RgbaImage.BytesPerPixel);
                    var bottom = ((((y + image.Height - 1) * size) + x + column) * RgbaImage.BytesPerPixel);
                    var bottomTarget = ((((y + image.Height - 1 + pad) * size) + x + column) * RgbaImage.BytesPerPixel);
                    Buffer.BlockCopy(pixels, top, pixels, topTarget, RgbaImage.BytesPerPixel);
                    Buffer.BlockCopy(pixels, bottom, pixels, bottomTarget, RgbaImage.BytesPerPixel);
                }

                for (var row = -pad; row < image.Height + pad; row++)
                {
                    var leftSource = ((((y + Math.Clamp(row, 0, image.Height - 1)) * size) + x) * RgbaImage.BytesPerPixel);
                    var leftTarget = ((((y + row) * size) + x - pad) * RgbaImage.BytesPerPixel);
                    var rightSource = ((((y + Math.Clamp(row, 0, image.Height - 1)) * size) + x + image.Width - 1) * RgbaImage.BytesPerPixel);
                    var rightTarget = ((((y + row) * size) + x + image.Width - 1 + pad) * RgbaImage.BytesPerPixel);
                    Buffer.BlockCopy(pixels, leftSource, pixels, leftTarget, RgbaImage.BytesPerPixel);
                    Buffer.BlockCopy(pixels, rightSource, pixels, rightTarget, RgbaImage.BytesPerPixel);
                }
            }

            // The range covers the sprite exactly, one texel per texel. It used to be inset by half a texel on every
            // edge, which squeezed a sixteen pixel tile onto fifteen and made the outermost texel unreadable: every block
            // then showed a thin dark line where the face met its neighbour (ADR-0015). Bleed is now prevented by the
            // padding ring below, not by shrinking the range.
            // V0 names the sprite's top row because row zero of the atlas is its top row, and the mesh emits v = 0
            // at the top edge of a face, following the reference model baker.
            index[location] = new SpriteRect(x, y, image.Width, image.Height, frames)
            {
                U0 = (float)x / size,
                U1 = (float)(x + image.Width) / size,
                V0 = (float)y / size,
                V1 = (float)(y + image.Height) / size
            };
        }

        return new SpriteAtlas(size, size, pixels, index);
    }

    private static RgbaImage TakeFirstFrame(RgbaImage image, int frameWidth, int frameHeight)
    {
        if (frameWidth == image.Width && frameHeight == image.Height)
        {
            return image;
        }

        var framePixels = new byte[frameWidth * frameHeight * RgbaImage.BytesPerPixel];

        for (var row = 0; row < frameHeight; row++)
        {
            Buffer.BlockCopy(
                image.Pixels,
                row * image.Width * RgbaImage.BytesPerPixel,
                framePixels,
                row * frameWidth * RgbaImage.BytesPerPixel,
                frameWidth * RgbaImage.BytesPerPixel);
        }

        return new RgbaImage(frameWidth, frameHeight, framePixels);
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
