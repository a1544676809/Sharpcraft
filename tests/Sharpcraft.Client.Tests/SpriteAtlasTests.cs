// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Text;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Atlas stitching (docs/modules/render.md §6.1): real sprite sizes, no overlap, stable output, and
/// the missing sprite always present — the same guarantees the vanilla atlas gives.
/// </summary>
public sealed class SpriteAtlasTests
{
    [Fact]
    public void SpritesKeepTheirRealSizesAndDoNotOverlap()
    {
        var sprites = new List<(Identifier, RgbaImage, int)>
        {
            (new Identifier("minecraft", "small"), Solid(16, 16, 255, 0, 0), 1),
            (new Identifier("minecraft", "tall"), Solid(16, 64, 0, 255, 0), 1),
            (new Identifier("minecraft", "wide"), Solid(64, 16, 0, 0, 255), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        };

        var atlas = SpriteAtlasBuilder.Stitch(sprites);

        Assert.Equal(atlas.Width, atlas.Height);
        Assert.Equal(0, atlas.Width & (atlas.Width - 1)); // power of two

        foreach (var (location, image, _) in sprites)
        {
            var sprite = atlas.Sprites[location];
            Assert.Equal(image.Width, sprite.Width);
            Assert.Equal(image.Height, sprite.Height);
            Assert.InRange(sprite.X + sprite.Width, 0, atlas.Width);
            Assert.InRange(sprite.Y + sprite.Height, 0, atlas.Height);
        }

        // No two sprites may share a pixel.
        var occupied = new bool[atlas.Width * atlas.Height];
        foreach (var sprite in atlas.Sprites.Values)
        {
            for (var y = sprite.Y; y < sprite.Y + sprite.Height; y++)
            {
                for (var x = sprite.X; x < sprite.X + sprite.Width; x++)
                {
                    var index = (y * atlas.Width) + x;
                    Assert.False(occupied[index], $"pixel {x},{y} is used twice");
                    occupied[index] = true;
                }
            }
        }
    }

    [Fact]
    public void PixelsLandWhereTheRectSaysTheyDo()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "red"), Solid(16, 16, 200, 10, 20), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        ]);

        var sprite = atlas.Sprites[new Identifier("minecraft", "red")];
        var offset = (((sprite.Y + 1) * atlas.Width) + sprite.X + 1) * RgbaImage.BytesPerPixel;

        Assert.Equal(200, atlas.Pixels[offset]);
        Assert.Equal(10, atlas.Pixels[offset + 1]);
        Assert.Equal(20, atlas.Pixels[offset + 2]);
        Assert.Equal(255, atlas.Pixels[offset + 3]);
    }

    [Fact]
    public void TextureCoordinatesCoverTheSpriteExactly()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "a"), Solid(32, 16, 1, 2, 3), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        ]);

        var sprite = atlas.Sprites[new Identifier("minecraft", "a")];

        // The range covers the sprite exactly; a half texel inset used to squeeze a tile onto one texel less and left
        // every block edge with a thin dark line (ADR-0015). Bleed is now handled by the padding ring.
        Assert.Equal((float)sprite.X / atlas.Width, sprite.U0, 5);
        Assert.Equal((float)(sprite.X + 32) / atlas.Width, sprite.U1, 5);
        // V0 names the sprite's top row, because row zero of the atlas is its top row and the mesh emits v = 0 at
        // the top edge of a face.
        Assert.Equal((float)sprite.Y / atlas.Height, sprite.V0, 5);
        Assert.Equal((float)(sprite.Y + 16) / atlas.Height, sprite.V1, 5);
        Assert.True(sprite.U1 > sprite.U0);
        Assert.True(sprite.V1 > sprite.V0);
    }

    [Fact]
    public void MissingSpriteIsAddressableForAnyUnknownLocation()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "known"), Solid(16, 16, 1, 1, 1), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        ]);

        Assert.True(atlas.Contains(new Identifier("minecraft", "known")));
        Assert.False(atlas.Contains(new Identifier("minecraft", "unknown")));

        var fallback = atlas.Get(new Identifier("minecraft", "unknown"));
        Assert.Equal(atlas.Sprites[SpriteAtlas.MissingSprite], fallback);
    }

    [Fact]
    public void AnimatedStripContributesItsFirstFrame()
    {
        // A 16x512 strip is 32 frames; the atlas must take the first frame only.
        var strip = Solid(16, 512, 10, 20, 30);
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "water_still"), strip, 32),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        ]);

        var sprite = atlas.Sprites[new Identifier("minecraft", "water_still")];
        Assert.Equal(16, sprite.Height);
        Assert.Equal(32, sprite.FrameCount);
    }

    [Fact]
    public void StitchingIsDeterministic()
    {
        var sprites = new List<(Identifier, RgbaImage, int)>
        {
            (new Identifier("minecraft", "b"), Solid(16, 16, 1, 1, 1), 1),
            (new Identifier("minecraft", "a"), Solid(16, 32, 2, 2, 2), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 3, 3, 3), 1)
        };

        var first = SpriteAtlasBuilder.Stitch(sprites);
        var second = SpriteAtlasBuilder.Stitch(sprites);

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Pixels, second.Pixels);
        Assert.Equal(first.Sprites.Keys.OrderBy(key => key.ToString()), second.Sprites.Keys.OrderBy(key => key.ToString()));
    }

    [Fact]
    public void BuildingFromAStackHonoursPackPriority()
    {
        using var fixture = new AtlasFixture();
        var basePack = fixture.WritePack("base.zip",
        [
            ("assets/minecraft/textures/block/stone.png", Png(16, 16, 10, 10, 10)),
            ("assets/minecraft/textures/block/dirt.png", Png(16, 16, 20, 20, 20))
        ]);
        var userPack = fixture.WritePack("user.zip",
        [
            ("assets/minecraft/textures/block/stone.png", Png(16, 16, 200, 30, 30))
        ]);

        using var stack = new AssetStack()
            .Add(new ZipAssetSource(basePack, "base"))
            .Add(new ZipAssetSource(userPack, "user-pack"));

        var atlas = SpriteAtlasBuilder.Build(stack);

        // Both sprites exist, the missing sprite is present, and the user pack won for stone.
        Assert.True(atlas.Contains(new Identifier("minecraft", "block/stone")));
        Assert.True(atlas.Contains(new Identifier("minecraft", "block/dirt")));
        Assert.True(atlas.Contains(SpriteAtlas.MissingSprite));

        var stone = atlas.Sprites[new Identifier("minecraft", "block/stone")];
        var offset = ((stone.Y * atlas.Width) + stone.X) * RgbaImage.BytesPerPixel;
        Assert.Equal(200, atlas.Pixels[offset]);
        Assert.Equal(30, atlas.Pixels[offset + 1]);
    }

    private static RgbaImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }

    private static byte[] Png(int width, int height, byte r, byte g, byte b) =>
        PngEncoder.EncodeRgba(width, height, Solid(width, height, r, g, b).Pixels);

    private sealed class AtlasFixture : IDisposable
    {
        public AtlasFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-atlas-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WritePack(string name, (string Path, byte[] Content)[] entries)
        {
            var path = Path.Combine(Root, name);
            using var stream = File.Create(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            foreach (var (entryPath, content) in entries)
            {
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }

            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Cleanup must never fail a test run.
            }
        }
    }
}
