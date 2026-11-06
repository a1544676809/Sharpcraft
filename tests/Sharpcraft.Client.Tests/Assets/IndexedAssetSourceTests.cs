// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// IndexedAssetSource (reference-equivalent to IndexedAssetSource.createIndexFs) with a TEMP fixture only: a small
/// <c>indexes/&lt;id&gt;.json</c> plus <c>objects/&lt;ab&gt;/&lt;sha&gt;</c> files of dummy text. No Minecraft assets are used
/// (ADR-0027).
/// </summary>
public sealed class IndexedAssetSourceTests
{
    [Fact]
    public void ResolvesLogicalPathThroughTheIndexToTheObjectFile()
    {
        using var fixture = new IndexFixture();
        var hash = new string('a', 40);
        var indexPath = fixture.WriteIndex("34", new Dictionary<string, string>
        {
            ["minecraft/lang/zh_cn.json"] = hash
        });
        fixture.WriteObject(hash, """{"greeting":"ni hao"}""");

        var source = IndexedAssetSource.Open(indexPath, expectedIndexId: "34");

        Assert.True(source.TryOpen(ResourceKind.Language, Id("zh_cn"), out var stream));
        using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            Assert.Contains("ni hao", reader.ReadToEnd(), StringComparison.Ordinal);
        }

        Assert.Equal(1, source.ObjectCount);
    }

    [Fact]
    public void MissingObjectIsRecordedAndDoesNotThrow()
    {
        using var fixture = new IndexFixture();
        var present = new string('b', 40);
        var absent = new string('c', 40);
        var indexPath = fixture.WriteIndex("34", new Dictionary<string, string>
        {
            ["minecraft/lang/en_us.json"] = present,
            ["minecraft/lang/zh_cn.json"] = absent
        });
        fixture.WriteObject(present, """{"k":"v"}""");

        var source = IndexedAssetSource.Open(indexPath, expectedIndexId: "34");

        Assert.False(source.TryOpen(ResourceKind.Language, Id("zh_cn"), out _));
        Assert.Equal(1, source.MissingCount);
    }

    [Fact]
    public void OverlayPrecedenceBeatsTheBuiltinJarPack()
    {
        using var fixture = new IndexFixture();
        var hash = new string('d', 40);
        var indexPath = fixture.WriteIndex("34", new Dictionary<string, string>
        {
            ["minecraft/lang/en_us.json"] = hash
        });
        fixture.WriteObject(hash, """{"source":"external"}""");

        // A dummy builtin "jar" pack that also declares en_us.
        var jarPack = fixture.WriteZip("jar.zip",
        [
            ("assets/minecraft/lang/en_us.json", """{"source":"jar"}""")
        ]);

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Throw }
            .Add(new BuiltInFallbackSource())
            .Add(new ZipAssetSource(jarPack, "base-pack"))
            .Add(IndexedAssetSource.Open(indexPath, expectedIndexId: "34"));

        var bytes = stack.Read(ResourceKind.Language, Id("en_us"));
        Assert.NotNull(bytes);
        Assert.Contains("external", Encoding.UTF8.GetString(bytes!), StringComparison.Ordinal);
    }

    [Fact]
    public void FallsBackToTheJarPackWhenTheIndexMisses()
    {
        using var fixture = new IndexFixture();
        var indexPath = fixture.WriteIndex("34", new Dictionary<string, string>());

        var jarPack = fixture.WriteZip("jar.zip",
        [
            ("assets/minecraft/lang/en_us.json", """{"source":"jar"}""")
        ]);

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Throw }
            .Add(new BuiltInFallbackSource())
            .Add(new ZipAssetSource(jarPack, "base-pack"))
            .Add(IndexedAssetSource.Open(indexPath, expectedIndexId: "34"));

        var bytes = stack.Read(ResourceKind.Language, Id("en_us"));
        Assert.NotNull(bytes);
        Assert.Contains("jar", Encoding.UTF8.GetString(bytes!), StringComparison.Ordinal);
    }

    [Fact]
    public void RawPathsSuchAsSoundsAndMcmetaResolve()
    {
        using var fixture = new IndexFixture();
        var soundsHash = new string('e', 40);
        var metaHash = new string('f', 40);
        var indexPath = fixture.WriteIndex("34", new Dictionary<string, string>
        {
            ["sounds.json"] = soundsHash,
            ["pack.mcmeta"] = metaHash
        });
        fixture.WriteObject(soundsHash, """{"events":{}}""");
        fixture.WriteObject(metaHash, """{"pack":{"pack_format":34}}""");

        var source = IndexedAssetSource.Open(indexPath, expectedIndexId: "34");

        Assert.True(source.TryOpenPath("sounds.json", out var sounds));
        sounds.Dispose();
        Assert.True(source.TryOpenPath("pack.mcmeta", out var meta));
        meta.Dispose();
        Assert.False(source.TryOpenPath("does/not/exist.json", out _));
    }

    private static Identifier Id(string path) => new("minecraft", path);

    private sealed class IndexFixture : IDisposable
    {
        public IndexFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-index-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteIndex(string id, IReadOnlyDictionary<string, string> objects)
        {
            var directory = Path.Combine(Root, "indexes");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, id + ".json");
            var payload = new
            {
                objects = objects.ToDictionary(pair => pair.Key, pair => new { hash = pair.Value, size = 1 })
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
