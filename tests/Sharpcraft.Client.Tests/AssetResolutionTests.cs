// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Asset resolution: stack order, overrides, listing, metadata siblings, language merge direction,
/// index validation, missing policy and custom resource packs
/// (docs/analysis/assets/resource-resolution.md, docs/modules/asset-resolution.md §7).
/// </summary>
public sealed class AssetResolutionTests
{
    [Fact]
    public void PathDerivationMatchesTheVanillaConverter()
    {
        Assert.Equal(
            "assets/minecraft/blockstates/stone.json",
            ResourcePath.FilePath(ResourceKind.BlockState, Id("stone")));
        Assert.Equal(
            "assets/minecraft/textures/block/stone.png",
            ResourcePath.FilePath(ResourceKind.Texture, Id("block/stone")));
        Assert.Equal(
            "assets/minecraft/lang/en_us.json",
            ResourcePath.FilePath(ResourceKind.Language, Id("en_us")));
        Assert.Equal("assets/minecraft/textures/", ResourcePath.Prefix(ResourceKind.Texture));
        Assert.Equal(
            "assets/minecraft/textures/block/stone.png.mcmeta",
            ResourcePath.MetadataPath("assets/minecraft/textures/block/stone.png"));

        Assert.True(ResourceLookup.TryParseUnderPrefix(
            "assets/minecraft/textures/",
            "assets/minecraft/textures/block/stone.png",
            out var parsed));
        Assert.Equal("minecraft:block/stone", parsed.ToString());

        Assert.False(ResourceLookup.TryParseUnderPrefix(
            "assets/minecraft/textures/",
            "assets/minecraft/models/block/stone.json",
            out _));
    }

    [Fact]
    public void HigherSourceInTheStackWins()
    {
        using var fixture = new ResolutionFixture();
        var low = fixture.WriteZip("low.zip", [("assets/minecraft/lang/en_us.json", """{"a":"low","b":"low"}""")]);
        var high = fixture.WriteZip("high.zip", [("assets/minecraft/lang/en_us.json", """{"a":"high"}""")]);

        using var stack = new AssetStack()
            .Add(new ZipAssetSource(low, "low"))
            .Add(new ZipAssetSource(high, "high"));

        var bytes = stack.Read(ResourceKind.Language, Id("en_us"));

        Assert.NotNull(bytes);
        Assert.Contains("high", Encoding.UTF8.GetString(bytes!), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTextureFallsBackToTheBuiltInCheckerboard()
    {
        using var fixture = new ResolutionFixture();
        var pack = fixture.WriteZip("pack.zip", [("assets/minecraft/models/block/stone.json", "{}")]);

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
            .Add(new BuiltInFallbackSource())
            .Add(new ZipAssetSource(pack, "pack"));

        var texture = stack.Read(ResourceKind.Texture, Id("block/stone"));

        Assert.NotNull(texture);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], texture![..4]);
    }

    [Fact]
    public void ListingDeduplicatesAndKeepsHigherSources()
    {
        using var fixture = new ResolutionFixture();
        var low = fixture.WriteZip("low.zip",
        [
            ("assets/minecraft/textures/block/a.png", "low-a"),
            ("assets/minecraft/textures/block/b.png", "low-b")
        ]);
        var high = fixture.WriteZip("high.zip", [("assets/minecraft/textures/block/b.png", "high-b")]);

        using var stack = new AssetStack()
            .Add(new ZipAssetSource(low, "low"))
            .Add(new ZipAssetSource(high, "high"));

        var listed = stack.ListAll(ResourcePath.Prefix(ResourceKind.Texture))
            .Select(id => id.ToString())
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["minecraft:block/a", "minecraft:block/b"], listed);
        Assert.Equal("high-b", Encoding.UTF8.GetString(stack.Read(ResourceKind.Texture, Id("block/b"))!));
    }

    [Fact]
    public void MetadataSiblingIsFoundNextToTheTexture()
    {
        using var fixture = new ResolutionFixture();
        var pack = fixture.WriteZip("pack.zip",
        [
            ("assets/minecraft/textures/block/water_still.png", "png"),
            ("assets/minecraft/textures/block/water_still.png.mcmeta", """{"animation":{"frametime":2}}""")
        ]);

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Throw }
            .Add(new ZipAssetSource(pack, "pack"));

        var meta = stack.ReadMetadata(ResourceKind.Texture, Id("block/water_still"));
        Assert.NotNull(meta);
        Assert.Contains("frametime", Encoding.UTF8.GetString(meta!), StringComparison.Ordinal);

        Assert.Null(stack.ReadMetadata(ResourceKind.Texture, Id("block/stone")));
    }

    [Fact]
    public void LanguageMergeFollowsVanillaDirection()
    {
        using var fixture = new ResolutionFixture();
        // Vanilla merges the resource stack highest-first with a plain put, so the lower priority
        // value wins for a duplicated key (resource-resolution.md §5).
        var low = fixture.WriteZip("low.zip", [("assets/minecraft/lang/en_us.json", """{"key":"low"}""")]);
        var high = fixture.WriteZip("high.zip", [("assets/minecraft/lang/en_us.json", """{"key":"high"}""")]);

        using var stack = new AssetStack()
            .Add(new ZipAssetSource(low, "low"))
            .Add(new ZipAssetSource(high, "high"));

        var merged = MergeLanguage(stack, Id("en_us"));

        Assert.Equal("low", merged["key"]);
    }

    [Fact]
    public void IndexIdMustMatchTheManifest()
    {
        using var fixture = new ResolutionFixture();
        var indexPath = fixture.WriteIndex("34", new Dictionary<string, string>());

        var mismatch = Assert.Throws<InvalidOperationException>(
            () => OfficialObjectsSource.Open(indexPath, expectedIndexId: "32"));

        Assert.Contains("id mismatch", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficialIndexServesObjectsAndCountsMissingFiles()
    {
        using var fixture = new ResolutionFixture();
        var present = new string('a', 40);
        var absent = new string('b', 40);

        var indexPath = fixture.WriteIndex(
            "34",
            new Dictionary<string, string>
            {
                ["minecraft/lang/en_us.json"] = present,
                ["minecraft/textures/block/missing.png"] = absent
            });

        var source = OfficialObjectsSource.Open(indexPath, expectedIndexId: "34");
        fixture.WriteObject(present, """{"greeting":"hi"}""");

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
            .Add(new BuiltInFallbackSource())
            .Add(source);

        var language = stack.Read(ResourceKind.Language, Id("en_us"));
        Assert.NotNull(language);
        Assert.Contains("greeting", Encoding.UTF8.GetString(language!), StringComparison.Ordinal);

        // The second object has no file on disk: the source records the miss and the built-in
        // fallback answers with the placeholder instead.
        var placeholder = stack.Read(ResourceKind.Texture, Id("block/missing"));
        Assert.NotNull(placeholder);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], placeholder![..4]);
        Assert.Equal(1, source.MissingCount);
        Assert.Equal(2, source.ObjectCount);
    }

    [Fact]
    public void MissingPolicyThrowRaisesWithTheSearchedChain()
    {
        using var fixture = new ResolutionFixture();

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Throw }
            .Add(new DirectoryAssetSource(fixture.Root, "empty"));

        var exception = Assert.Throws<AssetNotFoundException>(
            () => stack.Read(ResourceKind.Model, Id("definitely_missing")));

        Assert.Contains("empty", exception.Chain);
        Assert.Contains("definitely_missing", exception.Location.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CustomResourcePackOverridesTheBasePackAndReportsItsFormat()
    {
        using var fixture = new ResolutionFixture();
        var basePack = fixture.WriteZip("base.zip", [("assets/minecraft/textures/block/stone.png", "base")]);
        var userPack = fixture.WriteZip("user.zip",
        [
            ("pack.mcmeta", """{"pack":{"pack_format":34,"description":"my pack"}}"""),
            ("assets/minecraft/textures/block/stone.png", "user")
        ]);

        using var stack = new AssetStack()
            .Add(new ZipAssetSource(basePack, "base"))
            .Add(new ZipAssetSource(userPack, "user-pack"));

        Assert.Equal("user", Encoding.UTF8.GetString(stack.Read(ResourceKind.Texture, Id("block/stone"))!));

        var userSource = Assert.IsType<ZipAssetSource>(stack.Sources[^1]);
        Assert.NotNull(userSource.Metadata);
        Assert.Equal(34, userSource.Metadata!.PackFormat);
        Assert.Equal("my pack", userSource.Metadata.Description);
    }

    [Fact]
    public void DirectorySourceListsAndReads()
    {
        using var fixture = new ResolutionFixture();
        var directory = Path.Combine(fixture.Root, "packdir");
        Directory.CreateDirectory(Path.Combine(directory, "assets", "minecraft", "lang"));
        File.WriteAllText(Path.Combine(directory, "assets", "minecraft", "lang", "de_de.json"), """{"x":"y"}""");
        File.WriteAllText(Path.Combine(directory, "pack.mcmeta"), """{"pack":{"pack_format":34}}""");

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Throw }
            .Add(new DirectoryAssetSource(directory, "dir-pack"));

        var listed = stack.ListAll("assets/minecraft/lang/").Select(id => id.ToString()).ToArray();
        Assert.Equal(["minecraft:de_de"], listed);

        var source = Assert.IsType<DirectoryAssetSource>(stack.Sources[0]);
        Assert.Equal(34, source.Metadata!.PackFormat);
        Assert.NotNull(stack.Read(ResourceKind.Language, Id("de_de")));
    }

    [Fact]
    public void BuiltInFallbackServesOnlyTextures()
    {
        var source = new BuiltInFallbackSource();

        Assert.True(source.TryOpen(ResourceKind.Texture, Id("block/stone"), out var stream));
        using (stream)
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            Assert.Equal(source.Checkerboard, buffer.ToArray());
        }

        Assert.False(source.TryOpen(ResourceKind.Model, Id("block/stone"), out _));

        // Decode the first pixel: purple on black, proving the placeholder is ours, not borrowed art.
        using var inflate = new ZLibStream(new MemoryStream(PngIdat(source.Checkerboard)), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        inflate.CopyTo(raw);
        var scanlines = raw.ToArray();

        Assert.Equal(0xFF, scanlines[1]); // R
        Assert.Equal(0x00, scanlines[2]); // G
        Assert.Equal(0xFF, scanlines[3]); // B
        Assert.Equal(0xFF, scanlines[4]); // A
    }

    private static Identifier Id(string path) => new("minecraft", path);

    private static byte[] PngIdat(byte[] png)
    {
        var offset = 8;
        while (offset < png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IDAT")
            {
                return png[(offset + 8)..(offset + 8 + length)];
            }

            offset += 12 + length;
        }

        throw new InvalidOperationException("no IDAT chunk");
    }

    private static Dictionary<string, string> MergeLanguage(AssetStack stack, Identifier language)
    {
        var translations = new Dictionary<string, string>(StringComparer.Ordinal);

        // Vanilla walks the resource stack highest-first and puts in that order, so the lower
        // priority entry wins a duplicated key.
        foreach (var source in stack.Sources.Reverse())
        {
            if (!source.TryOpen(ResourceKind.Language, language, out var stream))
            {
                continue;
            }

            using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var document = JsonDocument.Parse(reader.ReadToEnd()))
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    translations[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }
        }

        return translations;
    }

    private sealed class ResolutionFixture : IDisposable
    {
        public ResolutionFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-res-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteZip(string name, (string Path, string Content)[] entries)
        {
            var path = Path.Combine(Root, name);
            using var stream = File.Create(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            foreach (var (entryPath, content) in entries)
            {
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(content);
            }

            return path;
        }

        public string WriteIndex(string id, IReadOnlyDictionary<string, string> objects)
        {
            var directory = Path.Combine(Root, "indexes");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, id + ".json");
            var payload = new
            {
                objects = objects.ToDictionary(
                    pair => pair.Key,
                    pair => new { hash = pair.Value, size = 1 })
            };

            File.WriteAllText(path, JsonSerializer.Serialize(payload));
            return path;
        }

        public string WriteObject(string hash, string content)
        {
            var directory = Path.Combine(Root, "objects", hash[..2]);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, hash);
            File.WriteAllText(path, content);
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
                // Temp cleanup must never fail a test run.
            }
        }
    }
}
