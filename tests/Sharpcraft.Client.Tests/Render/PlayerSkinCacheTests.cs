// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
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
}
