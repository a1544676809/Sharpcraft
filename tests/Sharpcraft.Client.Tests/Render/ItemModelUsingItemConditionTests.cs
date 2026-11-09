// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.IO.Compression;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The <c>minecraft:using_item</c> item-model condition, which is how the reference switches a shield to its blocking
/// model (<c>ItemModelGenerators.generateShield</c> wraps the two special models in
/// <c>ItemModelUtils.conditional(..., isUsingItem(), blocking, normal)</c>).
/// <para>
/// Every other item-model property still takes the static branch; this test pins the one branch that is evaluated.
/// </para>
/// </summary>
public sealed class ItemModelUsingItemConditionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sharpcraft-using-" + Guid.NewGuid().ToString("N"));

    public ItemModelUsingItemConditionTests() => Directory.CreateDirectory(_root);

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
    public void TheConditionSelectsTheBlockingModelWhileUsing()
    {
        var resolver = Build();

        var (normal, blocking) = (resolver.Resolve("minecraft:probe", usingItem: false), resolver.Resolve("minecraft:probe", usingItem: true));

        Assert.NotNull(normal);
        Assert.NotNull(blocking);
        Assert.Contains("probe_normal", normal!.ModelId, StringComparison.Ordinal);
        Assert.Contains("probe_blocking", blocking!.ModelId, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnrelatedPropertyStillTakesTheFalseBranch()
    {
        var resolver = Build(conditionProperty: "minecraft:has_component");

        var usingIt = resolver.Resolve("minecraft:probe", usingItem: true);
        Assert.NotNull(usingIt);
        Assert.Contains("probe_normal", usingIt!.ModelId, StringComparison.Ordinal);
    }

    private ItemModelResolver Build(string itemId = "minecraft:probe", string conditionProperty = "minecraft:using_item")
    {
        var archive = Path.Combine(_root, "base.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            Write(zip, "assets/minecraft/items/probe.json", $$"""
                {
                  "model": {
                    "type": "minecraft:condition",
                    "property": "{{conditionProperty}}",
                    "on_true": { "type": "minecraft:model", "model": "minecraft:item/probe_blocking" },
                    "on_false": { "type": "minecraft:model", "model": "minecraft:item/probe_normal" }
                  }
                }
                """);
            Write(zip, "assets/minecraft/models/item/probe_normal.json", """{ "textures": { "layer0": "item/probe_normal" } }""");
            Write(zip, "assets/minecraft/models/item/probe_blocking.json", """{ "textures": { "layer0": "item/probe_blocking" } }""");
        }

        var stack = new AssetStack { Policy = MissingResourcePolicy.Warn }
            .Add(new ZipAssetSource(archive, "base-pack"));
        var resources = new ResourceManager(stack, 1);
        return new ItemModelResolver(resources);
    }

    private static void Write(ZipArchive zip, string path, string contents)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(contents);
    }
}
