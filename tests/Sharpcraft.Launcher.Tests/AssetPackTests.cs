// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Text;
using Sharpcraft.Launcher.Assets;
using Xunit;

namespace Sharpcraft.Launcher.Tests;

/// <summary>
/// Asset pack building (docs/modules/assets.md §6, ADR-0005). Every fixture is a synthetic jar
/// assembled in the test, so no Minecraft artifact is involved.
/// </summary>
public sealed class AssetPackTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/block/stone.png", true)]
    [InlineData("assets/minecraft/textures/block/stone.png.mcmeta", true)]
    [InlineData("assets/minecraft/models/block/stone.json", true)]
    [InlineData("assets/minecraft/blockstates/stone.json", true)]
    [InlineData("assets/minecraft/items/stone.json", true)]
    [InlineData("assets/minecraft/font/default.json", true)]
    [InlineData("assets/minecraft/particles/flame.json", true)]
    [InlineData("assets/minecraft/shaders/core/block.fsh", true)]
    [InlineData("assets/minecraft/lang/en_us.json", true)]
    [InlineData("assets/minecraft/textures/", false)]
    [InlineData("assets/.mcassetsroot", false)]
    [InlineData("net/minecraft/client/Main.class", false)]
    [InlineData("data/minecraft/loot_tables/x.json", false)]
    [InlineData("com/mojang/x.class", false)]
    [InlineData("META-INF/MANIFEST.MF", false)]
    [InlineData("version.json", false)]
    [InlineData("../escape.png", false)]
    [InlineData("assets/minecraft/../../evil.png", false)]
    [InlineData("/absolute/evil.png", false)]
    [InlineData("C:/windows/evil.png", false)]
    public void SelectionFollowsTheMeasuredEntryMap(string entry, bool expected)
    {
        Assert.Equal(expected, AssetSelection.IsIncluded(entry));
    }

    [Fact]
    public void PackKeepsOriginalPathsAndManifest()
    {
        using var fixture = new PackFixture();
        var jar = fixture.WriteJar(
        [
            "assets/minecraft/textures/block/stone.png",
            "assets/minecraft/textures/block/stone.png.mcmeta",
            "assets/minecraft/models/block/stone.json",
            "net/minecraft/client/Main.class",
            "data/minecraft/advancement/x.json"
        ]);

        var result = AssetPackBuilder.Build(jar, fixture.Output, "26.3");

        Assert.False(result.Reused);
        Assert.Equal(3, result.EntryCount);
        Assert.True(File.Exists(result.PackPath));
        Assert.Contains("minecraft-26.3-", Path.GetFileName(result.PackPath), StringComparison.Ordinal);

        using var archive = ZipFile.OpenRead(result.PackPath);
        Assert.NotNull(archive.GetEntry("manifest.json"));
        Assert.NotNull(archive.GetEntry("assets/minecraft/textures/block/stone.png"));
        Assert.NotNull(archive.GetEntry("assets/minecraft/textures/block/stone.png.mcmeta"));
        Assert.Null(archive.GetEntry("net/minecraft/client/Main.class"));
        Assert.Null(archive.GetEntry("data/minecraft/advancement/x.json"));

        var manifest = AssetPackBuilder.ReadManifest(result.PackPath);
        Assert.Equal("26.3", manifest.VersionId);
        Assert.Equal(result.JarSha1, manifest.JarSha1);
        Assert.Equal(3, manifest.Entries.Count);
        Assert.All(manifest.Entries, entry => Assert.StartsWith("assets/minecraft/", entry.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void SecondBuildReusesTheExistingPack()
    {
        using var fixture = new PackFixture();
        var jar = fixture.WriteJar(["assets/minecraft/textures/block/dirt.png"]);

        var first = AssetPackBuilder.Build(jar, fixture.Output, "26.3");
        var written = File.GetLastWriteTimeUtc(first.PackPath);

        var second = AssetPackBuilder.Build(jar, fixture.Output, "26.3");

        Assert.True(second.Reused, "the same jar must reuse its pack instead of rebuilding");
        Assert.Equal(first.PackPath, second.PackPath);
        Assert.Equal(written, File.GetLastWriteTimeUtc(second.PackPath));
    }

    [Fact]
    public void SameJarProducesIdenticalBytes()
    {
        using var fixture = new PackFixture();
        var jar = fixture.WriteJar(
        [
            "assets/minecraft/textures/block/sand.png",
            "assets/minecraft/textures/block/sand.png.mcmeta"
        ]);

        var first = AssetPackBuilder.Build(jar, fixture.Output, "26.3");
        var firstBytes = File.ReadAllBytes(first.PackPath);
        Directory.Delete(fixture.Output, recursive: true);
        Directory.CreateDirectory(fixture.Output);

        var second = AssetPackBuilder.Build(jar, fixture.Output, "26.3");
        var secondBytes = File.ReadAllBytes(second.PackPath);

        Assert.Equal(first.PackSha256, second.PackSha256);
        Assert.Equal(firstBytes, secondBytes);
    }

    [Fact]
    public void DifferentJarProducesANewPack()
    {
        using var fixture = new PackFixture();
        var first = AssetPackBuilder.Build(
            fixture.WriteJar(["assets/minecraft/textures/block/grass_block_top.png"], "a.jar"),
            fixture.Output,
            "26.3");

        var second = AssetPackBuilder.Build(
            fixture.WriteJar(["assets/minecraft/textures/block/podzol_top.png"], "b.jar"),
            fixture.Output,
            "26.3");

        Assert.NotEqual(first.JarSha1, second.JarSha1);
        Assert.NotEqual(first.PackPath, second.PackPath);
        Assert.False(second.Reused);
    }

    [Fact]
    public void OutputInsideTheRepositoryIsRefused()
    {
        using var fixture = new PackFixture();
        var jar = fixture.WriteJar(["assets/minecraft/textures/block/stone.png"]);
        var repositoryRoot = HostPaths.FindRepositoryRoot();
        Assert.NotNull(repositoryRoot);

        // Unique per run so stale state from an earlier run cannot mask the guard.
        var insideRepository = Path.Combine(
            repositoryRoot!,
            "artifacts-should-never-exist-" + Guid.NewGuid().ToString("N"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => AssetPackBuilder.Build(jar, insideRepository, "26.3"));

        Assert.Contains("inside the repository", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(insideRepository));
    }

    [Fact]
    public void MismatchedJarHashIsRejectedBeforeWritingAnything()
    {
        using var fixture = new PackFixture();
        var jar = fixture.WriteJar(["assets/minecraft/textures/block/stone.png"]);

        Assert.Throws<JarVerificationException>(
            () => AssetPackBuilder.Build(jar, fixture.Output, "26.3", expectedJarSha1: new string('0', 40)));

        Assert.Empty(Directory.GetFiles(fixture.Output, "*.pack.zip"));
    }

    [Fact]
    public void DamagedPackDoesNotBlockARebuild()
    {
        using var fixture = new PackFixture();
        var jar = fixture.WriteJar(["assets/minecraft/textures/block/stone.png"]);
        var first = AssetPackBuilder.Build(jar, fixture.Output, "26.3");

        // Truncate the pack so its manifest cannot be read.
        File.WriteAllBytes(first.PackPath, [1, 2, 3]);

        var rebuilt = AssetPackBuilder.Build(jar, fixture.Output, "26.3");

        Assert.False(rebuilt.Reused);
        Assert.Equal(first.EntryCount, rebuilt.EntryCount);
    }

    private sealed class PackFixture : IDisposable
    {
        public PackFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "mcs-pack-" + Guid.NewGuid().ToString("N"));
            Output = Path.Combine(Root, "out");
            Directory.CreateDirectory(Output);
        }

        public string Root { get; }

        public string Output { get; }

        public string WriteJar(IReadOnlyList<string> entries, string name = "client.jar")
        {
            var path = Path.Combine(Root, name);
            using var stream = File.Create(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            foreach (var entry in entries)
            {
                var zip = archive.CreateEntry(entry, CompressionLevel.Optimal);
                using var writer = new StreamWriter(zip.Open(), Encoding.UTF8);
                writer.Write("payload:" + entry);
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

    [Fact]
    public void TooltipSpritesAreIncluded()
    {
        // The tooltip is drawn from GUI sprites, so they have to reach the pack (docs/analysis/gui/tooltip-background.md).
        Assert.True(AssetSelection.IsIncluded("assets/minecraft/textures/gui/sprites/tooltip/frame.png"));
        Assert.True(AssetSelection.IsIncluded("assets/minecraft/textures/gui/sprites/tooltip/frame.png.mcmeta"));
        Assert.True(AssetSelection.IsIncluded("assets/minecraft/textures/gui/sprites/tooltip/background.png"));
    }
}
