// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// The pack stack (docs/analysis/assets/resource-pack-stack.md): a folder or an archive is a pack, the last pack added
/// wins, and discovery skips anything the reference would skip.
/// </summary>
public sealed class ResourcePackStackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-packs-" + Guid.NewGuid().ToString("N"));

    public ResourcePackStackTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void TheLastPackAddedWins()
    {
        var low = MakeDirectoryPack("low", "assets/minecraft/lang/en_us.json", "low");
        var high = MakeDirectoryPack("high", "assets/minecraft/lang/en_us.json", "high");

        using var stack = new ResourcePackStack();
        stack.Add(ResourcePack.FromDirectory(low));
        stack.Add(ResourcePack.FromDirectory(high));

        using var stream = stack.Open("assets/minecraft/lang/en_us.json");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);

        Assert.Equal("high", reader.ReadToEnd());
    }

    [Fact]
    public void AFolderAndAnArchiveAreBothPacks()
    {
        var folder = MakeDirectoryPack("folder", "assets/minecraft/one.txt", "from folder");
        var archive = MakeArchivePack("archive", "assets/minecraft/two.txt", "from archive");

        using var stack = new ResourcePackStack();
        stack.Add(ResourcePack.FromDirectory(folder));
        stack.Add(ResourcePack.FromArchive(archive));

        Assert.Equal(2, stack.Count);

        using var first = stack.Open("assets/minecraft/one.txt");
        using var second = stack.Open("assets/minecraft/two.txt");

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public void AMissingPathFallsThroughEveryPack()
    {
        var folder = MakeDirectoryPack("only", "assets/minecraft/here.txt", "x");

        using var stack = new ResourcePackStack();
        stack.Add(ResourcePack.FromDirectory(folder));

        Assert.Null(stack.Open("assets/minecraft/absent.txt"));
    }

    [Fact]
    public void DiscoveryAcceptsFoldersAndArchivesAndIgnoresTheRest()
    {
        MakeDirectoryPack("good", "assets/minecraft/a.txt", "a");
        MakeArchivePack("zipped", "assets/minecraft/b.txt", "b");

        // A folder without pack.mcmeta, and a stray file, are not packs.
        Directory.CreateDirectory(Path.Combine(_root, "notapack", "assets"));
        File.WriteAllText(Path.Combine(_root, "loose.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "empty.zip"), "not a zip");

        var packs = ResourcePackDiscovery.Discover(_root);

        Assert.Equal(2, packs.Count);
        Assert.Contains(packs, pack => pack.Id == "good" && pack.Kind == ResourcePackKind.Directory);
        Assert.Contains(packs, pack => pack.Id == "zipped" && pack.Kind == ResourcePackKind.Archive);

        foreach (var pack in packs)
        {
            pack.Dispose();
        }
    }

    [Fact]
    public void NamespacesAreTheUnionWithoutDuplicates()
    {
        var first = MakeDirectoryPack("first", "assets/minecraft/f.txt", "f");
        var second = MakeDirectoryPack("second", "assets/othermod/g.txt", "g");

        using var stack = new ResourcePackStack();
        stack.Add(ResourcePack.FromDirectory(first));
        stack.Add(ResourcePack.FromDirectory(second));

        var namespaces = stack.Namespaces();

        Assert.Contains("minecraft", namespaces);
        Assert.Contains("othermod", namespaces);
        Assert.Equal(namespaces.Count, namespaces.Distinct().Count());
    }

    private string MakeDirectoryPack(string name, string relativePath, string content)
    {
        var root = Path.Combine(_root, name);
        var file = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(Path.Combine(root, ResourcePackDiscovery.MetadataFile), """{ "pack": { "description": "t" } }""");
        File.WriteAllText(file, content);
        return root;
    }

    private string MakeArchivePack(string name, string relativePath, string content)
    {
        var path = Path.Combine(_root, name + ".zip");

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Write(archive, ResourcePackDiscovery.MetadataFile, """{ "pack": { "description": "t" } }""");
            Write(archive, relativePath, content);
        }

        return path;
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
