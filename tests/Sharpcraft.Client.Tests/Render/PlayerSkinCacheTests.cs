// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.AuthLib;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Player profile textures reach the entity atlas through a background download and a main-thread registration step;
/// these tests keep the HTTP boundary fake so they are deterministic and offline.
/// </summary>
public sealed class PlayerSkinCacheTests
{
    private static string ProfileProperty() => Convert.ToBase64String(Encoding.UTF8.GetBytes("""
        {
          "textures": {
            "SKIN": { "url": "https://textures.minecraft.net/texture/aaaa" },
            "CAPE": { "url": "https://textures.minecraft.net/texture/abc123" },
            "ELYTRA": { "url": "https://textures.minecraft.net/texture/deadbeef" }
          }
        }
        """));

    [Fact]
    public void DownloadsAndRegistersCapeAndElytraTextures()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "sharpcraft-skin-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var registered = new Dictionary<string, TextureData>(StringComparer.Ordinal);
            var image = PngEncoder.EncodeRgba(64, 32, new byte[64 * 32 * 4]);
            var cache = new PlayerSkinCache(cacheDirectory, (path, texture) => registered[path] = texture, _ => image);
            var id = Guid.NewGuid();
            var entry = new PlayerListEntry(id, "Alice", 1, 20, true, 0, ProfileProperty());

            cache.Sync([entry]);
            for (var attempt = 0; attempt < 200 && cache.Pump() == 0; attempt++)
            {
                Thread.Sleep(10);
            }

            var textures = cache.TryGet(id);
            Assert.True(textures is not null, "profile did not become ready");
            Assert.Equal("skins/ab1df493e6bf8de49a74f5d9ea8f32266343fb1d", textures!.Value.Body?.Path);
            Assert.Equal("capes/689307d2fc53af0fb941bc1bb42737ce4f3ef540", textures.Value.Cape?.Path);
            Assert.Equal("elytra/8c00f528eca4aa2abd8617baad2a9fb47e0a2097", textures.Value.Elytra?.Path);
            Assert.Contains(textures.Value.Body!.Value.Path, registered.Keys);
            Assert.Contains(textures.Value.Cape!.Value.Path, registered.Keys);
            Assert.Contains(textures.Value.Elytra!.Value.Path, registered.Keys);

            // A second cache rooted at the same directory must load the captured files without downloading again.
            var second = new PlayerSkinCache(cacheDirectory, (_, _) => { }, _ => throw new InvalidOperationException("cache miss"));
            second.Sync([entry]);
            for (var attempt = 0; attempt < 200 && second.Pump() == 0; attempt++)
            {
                Thread.Sleep(10);
            }

            Assert.True(second.TryGet(id) is not null, "cached profile did not become ready");
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void RemoteUnsignedProfileIsNotPublishedWhenAKeySetIsConfigured()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "sharpcraft-skin-test-" + Guid.NewGuid().ToString("N"));
        var registered = new List<string>();
        var cache = new PlayerSkinCache(
            cacheDirectory,
            (path, _) => registered.Add(path),
            _ => throw new InvalidOperationException("remote unsigned texture must not download"),
            signatureKeys: ProfileSignatureKeySet.Empty,
            localProfileId: Guid.NewGuid());
        var id = Guid.NewGuid();
        cache.Sync([new PlayerListEntry(id, "Alice", 1, 20, true, 0, ProfileProperty())]);

        var textures = cache.TryGet(id);
        Assert.True(textures is not null);
        Assert.Null(textures!.Value.Body);
        Assert.Null(textures.Value.Cape);
        Assert.Null(textures.Value.Elytra);
        Assert.Equal(ProfileSignatureState.Unsigned, textures.Value.SignatureState);
        Assert.Empty(registered);
    }

    [Fact]
    public void MalformedPropertyLeavesTheProfileUnavailable()
    {
        var cache = new PlayerSkinCache(
            Path.Combine(Path.GetTempPath(), "sharpcraft-skin-test-" + Guid.NewGuid().ToString("N")),
            (_, _) => { },
            _ => null);
        var id = Guid.NewGuid();
        cache.Sync([new PlayerListEntry(id, "Alice", 1, 20, true, 0, "not base64!")]);
        Assert.Equal(0, cache.Pump());
        Assert.Null(cache.TryGet(id));
    }

    private static (PlayerSkinTextures? Textures, Dictionary<string, TextureData> Registered) Run(byte[] png)
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "sharpcraft-skin-test-" + Guid.NewGuid().ToString("N"));
        var registered = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        try
        {
            var cache = new PlayerSkinCache(cacheDirectory, (path, texture) => registered[path] = texture, _ => png);
            var id = Guid.NewGuid();
            cache.Sync([new PlayerListEntry(id, "Alice", 1, 20, true, 0, ProfileProperty())]);
            for (var attempt = 0; attempt < 200 && cache.Pump() == 0; attempt++)
            {
                Thread.Sleep(10);
            }

            return (cache.TryGet(id), registered);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    private static (byte R, byte G, byte B, byte A) At(TextureData texture, int x, int y)
    {
        var offset = ((y * 64) + x) * 4;
        return (texture.Rgba[offset], texture.Rgba[offset + 1], texture.Rgba[offset + 2], texture.Rgba[offset + 3]);
    }

    [Fact]
    public void LegacySixtyFourByThirtyTwoSkinIsExpandedAndCopied()
    {
        var pixels = new byte[64 * 32 * 4];
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var offset = ((y * 64) + x) * 4;
                pixels[offset] = (byte)x;
                pixels[offset + 1] = (byte)y;
                pixels[offset + 2] = 0x33;
                pixels[offset + 3] = 255;
            }
        }

        var (textures, registered) = Run(PngEncoder.EncodeRgba(64, 32, pixels));
        Assert.True(textures is not null, "profile did not become ready");
        var body = registered[textures!.Value.Body!.Value.Path];
        Assert.Equal(64, body.Width);
        Assert.Equal(64, body.Height);
        Assert.Equal(64 * 64 * 4, body.Rgba.Length);

        // Upper half keeps its RGB; the reference alpha fixes are applied on top.
        Assert.Equal((0, 0, 0x33, 255), At(body, 0, 0));
        Assert.Equal((16, 20, 0x33, 255), At(body, 16, 20));
        // A fully opaque top-right quadrant is made transparent by the Notch hack.
        Assert.Equal((32, 0, 0x33, 0), At(body, 32, 0));
        // Rows 32..47 are empty.
        Assert.Equal((0, 0, 0, 0), At(body, 0, 40));

        // copyRect(4,16,16,32,4,4,swapX): source (4,16) lands at (23,48), source (7,16) at (20,48).
        Assert.Equal((4, 16, 0x33, 255), At(body, 23, 48));
        Assert.Equal((7, 16, 0x33, 255), At(body, 20, 48));
        // copyRect(8,16,16,32,...): target x = 8 + 16 = 24, so source (11,16) lands at (24,48).
        Assert.Equal((11, 16, 0x33, 255), At(body, 24, 48));

        // Cape/elytra never go through the SKIN converter.
        Assert.Equal(32, registered[textures.Value.Cape!.Value.Path].Height);
        Assert.Equal(32, registered[textures.Value.Elytra!.Value.Path].Height);
    }

    [Fact]
    public void ModernSixtyFourBySixtyFourSkinKeepsItsSizeAndAlphaRegions()
    {
        var (textures, registered) = Run(PngEncoder.EncodeRgba(64, 64, new byte[64 * 64 * 4]));
        Assert.True(textures is not null, "profile did not become ready");
        var body = registered[textures!.Value.Body!.Value.Path];
        Assert.Equal(64, body.Width);
        Assert.Equal(64, body.Height);
        Assert.Equal(255, At(body, 0, 0).A);
        Assert.Equal(255, At(body, 0, 20).A);
        Assert.Equal(0, At(body, 0, 40).A);
        Assert.Equal(255, At(body, 16, 48).A);
        Assert.Equal(64, registered[textures.Value.Cape!.Value.Path].Height);
    }

    [Fact]
    public void IncorrectlySizedSkinIsDropped()
    {
        var (textures, registered) = Run(PngEncoder.EncodeRgba(32, 32, new byte[32 * 32 * 4]));
        Assert.True(textures is not null, "profile did not become ready");
        Assert.Null(textures!.Value.Body);
        Assert.Null(textures.Value.Cape);
        Assert.Empty(registered);
    }
}
