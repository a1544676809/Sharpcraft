// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Overlay folders (docs/analysis/assets/resource-pack-stack.md §6/§8): an entry applies only for the matching pack
/// format, the entry declared last wins over earlier ones, and the pack root is always the lowest priority.
/// </summary>
public sealed class ResourcePackOverlayTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-ov-" + Guid.NewGuid().ToString("N"));

    public ResourcePackOverlayTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void AnOverlayAppliesOnlyForItsFormatRange()
    {
        var pack = MakePack(
            """{ "overlays": { "entries": [ { "format": [60, 70], "overlay": "v64" } ] } }""",
            ("assets/minecraft/lang/en_us.json", "root"),
            ("v64/assets/minecraft/lang/en_us.json", "overlay"));

        using var lower = ResourcePack.FromDirectory(pack);
        lower.ClientFormat = 64;
        Assert.Equal("overlay", Read(lower));

        using var other = ResourcePack.FromDirectory(pack);
        other.ClientFormat = 80;
        Assert.Equal("root", Read(other));
    }

    [Fact]
    public void TheLastDeclaredOverlayWins()
    {
        var pack = MakePack(
            """{ "overlays": { "entries": [ { "format": 64, "overlay": "first" }, { "format": 64, "overlay": "second" } ] } }""",
            ("assets/minecraft/lang/en_us.json", "root"),
            ("first/assets/minecraft/lang/en_us.json", "one"),
            ("second/assets/minecraft/lang/en_us.json", "two"));

        using var readied = ResourcePack.FromDirectory(pack);
        readied.ClientFormat = 64;

        Assert.Equal("two", Read(readied));
    }

    [Fact]
    public void MetadataIsNeverOverlaid()
    {
        var pack = MakePack(
            """{ "overlays": { "entries": [ { "format": 64, "overlay": "v64" } ] } }""",
            ("v64/pack.mcmeta", "{ \"pack\": { \"description\": \"overlaid\" } }"));

        using var readied = ResourcePack.FromDirectory(pack);
        readied.ClientFormat = 64;

        using var metadata = readied.Open("pack.mcmeta");
        Assert.NotNull(metadata);
        using var reader = new StreamReader(metadata!);

        Assert.Contains("overlays", reader.ReadToEnd());
    }

    [Fact]
    public void AnUnsafeOverlayNameIsRefused()
    {
        var overlays = ResourcePackOverlays.TryParse(
            """{ "overlays": { "entries": [ { "format": 64, "overlay": "../escape" } ] } }""");

        Assert.Null(overlays);
    }

    private static string Read(ResourcePack pack)
    {
        using var stream = pack.Open("assets/minecraft/lang/en_us.json");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private string MakePack(string metadata, params (string Path, string Content)[] files)
    {
        var root = Path.Combine(_root, "pack");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "pack.mcmeta"), metadata);

        foreach (var (relative, content) in files)
        {
            var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, content);
        }

        return root;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
