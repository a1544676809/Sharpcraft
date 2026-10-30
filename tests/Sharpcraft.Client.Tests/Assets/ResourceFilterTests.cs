// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// A pack's <c>filter</c> block (docs/analysis/assets/resource-pack-stack.md §2). A filtered path must be absent even
/// when a pack below carries it, which is the part a plain "not found" cannot express.
/// </summary>
public sealed class ResourceFilterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-filter-" + Guid.NewGuid().ToString("N"));

    public ResourceFilterTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void AFilteredPathIsAbsentEvenThoughALowerPackHasIt()
    {
        var basePack = MakeArchive("base.zip", "assets/minecraft/lang/en_us.json", "from base");
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(Path.Combine(folder, "hidden"), null, """{ "filter": { "block": [ { "path": "en_us" } ] } }""");

        using var manager = Build(basePack, folder);

        Assert.Null(manager.Read(ResourceKind.Language, new Identifier("minecraft", "en_us")));
    }

    [Fact]
    public void APathOutsideTheFilterStillResolves()
    {
        var basePack = MakeArchive("base.zip", "assets/minecraft/lang/en_us.json", "from base");
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(Path.Combine(folder, "hidden"), null, """{ "filter": { "block": [ { "path": "de_de" } ] } }""");

        using var manager = Build(basePack, folder);
        var bytes = manager.Read(ResourceKind.Language, new Identifier("minecraft", "en_us"));

        Assert.NotNull(bytes);
        Assert.Equal("from base", System.Text.Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public void AFilterWithNoFieldsHidesEverything()
    {
        var basePack = MakeArchive("base.zip", "assets/minecraft/lang/en_us.json", "from base");
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(Path.Combine(folder, "blackhole"), null, """{ "filter": { "block": [ {} ] } }""");

        using var manager = Build(basePack, folder);

        Assert.Null(manager.Read(ResourceKind.Language, new Identifier("minecraft", "en_us")));
    }

    [Fact]
    public void ANamespaceFilterOnlyHidesThatNamespace()
    {
        var basePack = MakeArchive("base.zip", "assets/minecraft/lang/en_us.json", "from base");
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(Path.Combine(folder, "ns"), null, """{ "filter": { "block": [ { "namespace": "othermod" } ] } }""");

        using var manager = Build(basePack, folder);

        Assert.NotNull(manager.Read(ResourceKind.Language, new Identifier("minecraft", "en_us")));
        Assert.Null(manager.Read(ResourceKind.Language, new Identifier("othermod", "en_us")));
    }

    private ResourceManager Build(string basePack, string folder)
    {
        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ZipAssetSource(basePack, "base-pack"));

        foreach (var pack in ResourcePackDiscovery.Discover(folder))
        {
            stack.Add(new ResourcePackSource(pack));
        }

        return new ResourceManager(stack, 1);
    }

    private string MakeArchive(string name, string relativePath, string content)
    {
        var path = Path.Combine(_root, name);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry(relativePath);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);

        return path;
    }

    private static void MakeFolderPack(string root, string? relativePath, string metadata)
    {
        Directory.CreateDirectory(root);

        if (relativePath is not null)
        {
            var file = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "x");
        }

        File.WriteAllText(Path.Combine(root, "pack.mcmeta"), metadata);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
