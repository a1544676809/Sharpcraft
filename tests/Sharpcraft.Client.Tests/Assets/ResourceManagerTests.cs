// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// The session resource manager (docs/analysis/assets/resource-pack-stack.md): one stack for every loader, so a pack is
/// discovered once and overrides apply everywhere, including the metadata files that sit beside a texture.
/// </summary>
public sealed class ResourceManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-rm-" + Guid.NewGuid().ToString("N"));

    public ResourceManagerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void AnInstalledPackOverridesTheBasePackAndIsCountedOnce()
    {
        var basePack = MakeArchive("base.zip", "assets/minecraft/lang/en_us.json", "from base");
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(Path.Combine(folder, "extra"), "assets/minecraft/lang/en_us.json", "from pack");

        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ZipAssetSource(basePack, "base-pack"));

        foreach (var pack in ResourcePackDiscovery.Discover(folder))
        {
            stack.Add(new ResourcePackSource(pack));
        }

        using var manager = new ResourceManager(stack, 1);

        Assert.Equal(1, manager.PackCount);
        var bytes = manager.Read(ResourceKind.Language, new Identifier("minecraft", "en_us"));
        Assert.NotNull(bytes);
        Assert.Equal("from pack", System.Text.Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public void MetadataBesideATextureComesFromTheSameOverridingPack()
    {
        var basePack = MakeArchive(
            "base.zip", "assets/minecraft/textures/gui/sprites/tooltip/frame.png.mcmeta", "base metadata");
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(
            Path.Combine(folder, "extra"),
            "assets/minecraft/textures/gui/sprites/tooltip/frame.png.mcmeta",
            """{ "gui": { "scaling": { "type": "tile", "width": 4, "height": 4 } } }""");

        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ZipAssetSource(basePack, "base-pack"));

        foreach (var pack in ResourcePackDiscovery.Discover(folder))
        {
            stack.Add(new ResourcePackSource(pack));
        }

        using var manager = new ResourceManager(stack, 1);
        using var stream = manager.OpenFile("assets/minecraft/textures/gui/sprites/tooltip/frame.png.mcmeta");

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var scaling = GuiSpriteScaling.TryParse(reader.ReadToEnd());

        Assert.NotNull(scaling);
        Assert.Equal(GuiSpriteScalingType.Tile, scaling!.Type);
    }

    [Fact]
    public void AReloadSwapsTheStackAndRunsListenersInOrder()
    {
        var folder = Path.Combine(_root, "resourcepacks");
        var packRoot = Path.Combine(folder, "extra");
        MakeFolderPack(packRoot, "assets/minecraft/lang/en_us.json", "first");

        var manager = new ResourceManager(
            BuildStack(folder), 1,
            () => (BuildStack(folder), 1));

        var order = new List<string>();
        manager.AddListener("later", 20, _ => order.Add("later"));
        manager.AddListener("early", 10, _ => order.Add("early"));

        Assert.Equal("first", ReadLanguage(manager));

        File.WriteAllText(
            Path.Combine(packRoot, "assets", "minecraft", "lang", "en_us.json"), "second");

        Assert.True(manager.Reload());
        Assert.Equal("second", ReadLanguage(manager));
        Assert.Equal(["early", "later"], order);
    }

    [Fact]
    public void AFailedReloadKeepsThePreviousStack()
    {
        var folder = Path.Combine(_root, "resourcepacks");
        MakeFolderPack(Path.Combine(folder, "extra"), "assets/minecraft/lang/en_us.json", "first");

        var attempts = 0;
        var manager = new ResourceManager(
            BuildStack(folder), 1,
            () =>
            {
                attempts++;

                if (attempts > 1)
                {
                    throw new IOException("file is mid-write");
                }

                return (BuildStack(folder), 1);
            });

        Assert.True(manager.Reload());
        Assert.False(manager.Reload());
        Assert.Equal("first", ReadLanguage(manager));
    }

    private static string ReadLanguage(ResourceManager manager)
    {
        var bytes = manager.Read(ResourceKind.Language, new Identifier("minecraft", "en_us"));
        Assert.NotNull(bytes);
        return System.Text.Encoding.UTF8.GetString(bytes!);
    }

    private AssetStack BuildStack(string folder)
    {
        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn };

        foreach (var pack in ResourcePackDiscovery.Discover(folder))
        {
            stack.Add(new ResourcePackSource(pack));
        }

        return stack;
    }

    private string MakeArchive(string name, string relativePath, string content)
    {
        var path = Path.Combine(_root, name);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(zip, relativePath, content);

        return path;
    }

    private static void MakeFolderPack(string root, string relativePath, string content)
    {
        var file = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(Path.Combine(root, "pack.mcmeta"), """{ "pack": { "description": "t" } }""");
        File.WriteAllText(file, content);
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
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
