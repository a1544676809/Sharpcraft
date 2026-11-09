// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// <c>minecraft:composite</c> item models: the reference submits every member, each under its own
/// <c>transformation</c> block. A bed is the visible case — <c>items/red_bed.json</c> composes
/// <c>block/red_bed_head</c> with <c>block/red_bed_foot</c> translated one block along Z — so taking only the first
/// member draws half a bed.
/// </summary>
public sealed class ItemModelCompositeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-composite-" + Guid.NewGuid().ToString("N"));

    public ItemModelCompositeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void EveryMemberIsResolvedWithItsOwnTransformation()
    {
        var resolver = Build();

        var placements = resolver.ResolveAll("minecraft:probe_bed");

        Assert.Equal(2, placements.Count);
        Assert.Contains("probe_head", placements[0].Model.ModelId, StringComparison.Ordinal);
        Assert.Equal(Matrix4x4.Identity, placements[0].Transform);
        Assert.Contains("probe_foot", placements[1].Model.ModelId, StringComparison.Ordinal);

        // Row-vector: the foot moves one block along +Z before any display transform.
        Assert.Equal(new Vector3(0f, 0f, 1f), Vector3.Transform(Vector3.Zero, placements[1].Transform));

        // Each member owns its texture family; the atlas pre-pass must see both, otherwise the second half renders with
        // every sprite missing and collapses to its remaining faces (the bed foot lost its mattress and legs this way).
        Assert.NotEqual(placements[0].Model.Textures["all"], placements[1].Model.Textures["all"]);
    }

    [Fact]
    public void TheSingleModelResolutionStillTakesTheFirstMember()
    {
        var resolver = Build();

        var single = resolver.Resolve("minecraft:probe_bed");

        Assert.NotNull(single);
        Assert.Contains("probe_head", single!.ModelId, StringComparison.Ordinal);
    }

    [Fact]
    public void APlainModelResolvesToOnePlacementWithNoTransform()
    {
        var resolver = Build();

        var placements = resolver.ResolveAll("minecraft:probe_plain");

        Assert.Single(placements);
        Assert.Equal(Matrix4x4.Identity, placements[0].Transform);
    }

    private ItemModelResolver Build()
    {
        var archive = Path.Combine(_root, "base.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            Write(zip, "assets/minecraft/items/probe_bed.json", """
                {
                  "model": {
                    "type": "minecraft:composite",
                    "models": [
                      { "type": "minecraft:model", "model": "minecraft:block/probe_head" },
                      {
                        "type": "minecraft:model",
                        "model": "minecraft:block/probe_foot",
                        "transformation": {
                          "left_rotation": [0.0, 0.0, 0.0, 1.0],
                          "right_rotation": [0.0, 0.0, 0.0, 1.0],
                          "scale": [1.0, 1.0, 1.0],
                          "translation": [0.0, 0.0, 1.0]
                        }
                      }
                    ]
                  }
                }
                """);
            Write(zip, "assets/minecraft/items/probe_plain.json", """
                { "model": { "type": "minecraft:model", "model": "minecraft:item/probe_plain" } }
                """);
            Write(zip, "assets/minecraft/models/block/probe_head.json",
                """{ "textures": { "all": "block/probe_head_tex" }, "elements": [ { "from": [0, 0, 0], "to": [16, 8, 16] } ] }""");
            Write(zip, "assets/minecraft/models/block/probe_foot.json",
                """{ "textures": { "all": "block/probe_foot_tex" }, "elements": [ { "from": [0, 0, 0], "to": [16, 8, 16] } ] }""");
            Write(zip, "assets/minecraft/models/item/probe_plain.json",
                """{ "textures": { "layer0": "item/probe_plain" } }""");
        }

        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ZipAssetSource(archive, "base-pack"));
        return new ItemModelResolver(new ResourceManager(stack, 1));
    }

    private static void Write(ZipArchive zip, string path, string contents)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
    }
}
