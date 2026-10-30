// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Packs attached to the asset stack (docs/analysis/assets/resource-pack-stack.md). The stack scans backwards, so a pack
/// appended after the base one overrides it without any loader knowing that packs exist.
/// </summary>
public sealed class ResourcePackSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-rps-" + Guid.NewGuid().ToString("N"));

    public ResourcePackSourceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void APackOverridesTheBasePackBecauseItIsAppendedLast()
    {
        var low = MakeFolder("low", "assets/minecraft/lang/en_us.json", "low");
        var high = MakeFolder("high", "assets/minecraft/lang/en_us.json", "high");

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ResourcePackSource(ResourcePack.FromDirectory(low)))
            .Add(new ResourcePackSource(ResourcePack.FromDirectory(high)));

        var bytes = stack.Read(ResourceKind.Language, new Identifier("minecraft", "en_us"));
        Assert.NotNull(bytes);
        Assert.Equal("high", System.Text.Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public void TheSourceMapsAKindToItsDirectoryAndExtension()
    {
        var folder = MakeFolder("one", "assets/minecraft/textures/gui/sprites/tooltip/frame.png", "bytes");

        using var source = new ResourcePackSource(ResourcePack.FromDirectory(folder));
        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }.Add(source);

        Assert.NotNull(stack.Read(ResourceKind.Texture, new Identifier("minecraft", "gui/sprites/tooltip/frame")));
        // The extension belongs to the kind, so a texture lookup appends .png and a json kind does not.
        Assert.Null(stack.Read(ResourceKind.Language, new Identifier("minecraft", "gui/sprites/tooltip/frame")));
    }

    [Fact]
    public void AnArchivePackWorksThroughTheSameSource()
    {
        var archive = Path.Combine(_root, "packed.zip");

        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            Write(zip, "pack.mcmeta", """{ "pack": { "description": "t" } }""");
            Write(zip, "assets/minecraft/lang/en_us.json", "from archive");
        }

        using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ResourcePackSource(ResourcePack.FromArchive(archive)));

        var bytes = stack.Read(ResourceKind.Language, new Identifier("minecraft", "en_us"));
        Assert.NotNull(bytes);
        Assert.Equal("from archive", System.Text.Encoding.UTF8.GetString(bytes!));
    }

    private string MakeFolder(string name, string relativePath, string content)
    {
        var root = Path.Combine(_root, name);
        var file = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(Path.Combine(root, "pack.mcmeta"), """{ "pack": { "description": "t" } }""");
        File.WriteAllText(file, content);
        return root;
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
