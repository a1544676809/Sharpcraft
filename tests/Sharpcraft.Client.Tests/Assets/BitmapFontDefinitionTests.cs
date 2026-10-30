// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Reading the font provider (docs/analysis/assets/font-slicing.md §1): the pack declares the sheet, the grid and the
/// metrics, and a reference provider has to be followed to reach the bitmap one.
/// </summary>
public sealed class BitmapFontDefinitionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-font-" + Guid.NewGuid().ToString("N"));

    public BitmapFontDefinitionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void AReferenceIsFollowedToTheBitmapProvider()
    {
        WriteFont("default", """{ "providers": [ { "type": "reference", "id": "minecraft:include/default" } ] }""");
        WriteFont("include/default", """
        { "providers": [
            { "type": "bitmap", "file": "minecraft:font/ascii.png", "ascent": 7,
              "chars": ["AAAAAAAAAAAAAAAA", "AAAAAAAAAAAAAAAA"] }
        ] }
        """);

        using var resources = Build();
        var provider = BitmapFontDefinition.Resolve(resources);

        Assert.NotNull(provider);
        Assert.Equal(new Identifier("minecraft", "font/ascii.png"), provider!.Value.File);
        Assert.Equal(8, provider.Value.Height);
        Assert.Equal(7, provider.Value.Ascent);
        Assert.Equal(16, provider.Value.Columns);
        Assert.Equal(2, provider.Value.Rows);
    }

    [Fact]
    public void ADeclaredHeightAndGridWin()
    {
        WriteFont("default", """
        { "providers": [
            { "type": "bitmap", "file": "minecraft:font/ascii.png", "height": 12, "ascent": 10,
              "chars": ["AAAAAAAAAA"] }
        ] }
        """);

        using var resources = Build();
        var provider = BitmapFontDefinition.Resolve(resources);

        Assert.NotNull(provider);
        Assert.Equal(12, provider!.Value.Height);
        Assert.Equal(10, provider.Value.Ascent);
        Assert.Equal(10, provider.Value.Columns);
        Assert.Equal(1, provider.Value.Rows);
    }

    [Fact]
    public void NoDefinitionYieldsNothing()
    {
        using var resources = Build();
        Assert.Null(BitmapFontDefinition.Resolve(resources));
    }

    private void WriteFont(string id, string json)
    {
        var file = Path.Combine(_root, "pack", "assets", "minecraft", "font", (id + ".json").Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, json);
        File.WriteAllText(
            Path.Combine(_root, "pack", "pack.mcmeta"),
            """{ "pack": { "description": "t" } }""");
    }

    private ResourceManager Build()
    {
        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn };

        foreach (var pack in ResourcePackDiscovery.Discover(_root))
        {
            stack.Add(new ResourcePackSource(pack));
        }

        return new ResourceManager(stack, 1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
