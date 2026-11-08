// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class ItemModelResolverTests
{
    [Fact]
    public void ResolvesItemModelParentTexturesAndDisplay()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/test_sword.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/test_sword"}}""");
            Write(root, "assets/minecraft/models/item/test_sword.json",
                """{"parent":"minecraft:item/handheld","textures":{"layer0":"minecraft:item/test_sword"}}""");
            Write(root, "assets/minecraft/models/item/handheld.json",
                """{"parent":"minecraft:item/generated","display":{"thirdperson_righthand":{"rotation":[0,-90,55],"translation":[0,4.0,0.5],"scale":[0.85,0.85,0.85]}}}""");
            Write(root, "assets/minecraft/models/item/generated.json",
                """{"parent":"builtin/generated","display":{"thirdperson_righthand":{"rotation":[0,0,0],"translation":[0,3,1],"scale":[0.55,0.55,0.55]},"head":{"rotation":[0,180,0],"translation":[0,13,7],"scale":[1,1,1]}}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var resolver = new ItemModelResolver(resources);
            var model = resolver.Resolve("minecraft:test_sword");

            Assert.NotNull(model);
            Assert.Equal("minecraft:item/test_sword", model!.ModelId);
            Assert.Equal("minecraft:item/test_sword", model.Textures["layer0"]);
            var third = model.Display[ItemDisplayContext.ThirdPersonRightHand];
            Assert.Equal(new System.Numerics.Vector3(0f, -90f, 55f), third.RotationDegrees);
            Assert.Equal(new System.Numerics.Vector3(0f, 0.25f, 0.03125f), third.Translation);
            Assert.Equal(new System.Numerics.Vector3(0.85f), third.Scale);
            // Missing left-hand entry falls back to the right-hand transform.
            Assert.Equal(third, model.Display[ItemDisplayContext.ThirdPersonLeftHand]);
            Assert.Equal(1f, model.Display[ItemDisplayContext.Head].Scale.X, 5);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void SelectConditionAndRangeUseTheirStaticFallbackModels()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/test_complex.json",
                """{"model":{"type":"minecraft:select","fallback":{"type":"minecraft:model","model":"minecraft:item/default"},"cases":[{"when":"special","model":{"type":"minecraft:condition","on_false":{"type":"minecraft:model","model":"minecraft:item/default"}}}]}}""");
            Write(root, "assets/minecraft/models/item/default.json",
                """{"parent":"builtin/generated","textures":{"layer0":"minecraft:item/default"}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var model = new ItemModelResolver(resources).Resolve("minecraft:test_complex");

            Assert.NotNull(model);
            Assert.Equal("minecraft:item/default", model!.ModelId);
            Assert.Equal("minecraft:item/default", model.Textures["layer0"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void SpecialItemModelKeepsTheDescriptorWithoutResolvingGeometry()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/test_special.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/default","model":{"type":"minecraft:head","kind":"creeper"}}}""");
            Write(root, "assets/minecraft/models/item/default.json",
                """{"parent":"builtin/generated","textures":{"layer0":"minecraft:item/default"}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var model = new ItemModelResolver(resources).Resolve("minecraft:test_special");

            Assert.NotNull(model);
            Assert.Equal("minecraft:head", model!.SpecialRendererType);
            Assert.Equal("creeper", model.SpecialProperties!["kind"]);
            Assert.Equal("minecraft:item/default", model.Textures["layer0"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ComponentModelIdOverridesTheItemDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/test_item.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/default"}}""");
            Write(root, "assets/minecraft/models/item/default.json",
                """{"parent":"builtin/generated","textures":{"layer0":"minecraft:item/default"}}""");
            Write(root, "assets/minecraft/models/item/override.json",
                """{"parent":"builtin/generated","textures":{"layer0":"minecraft:item/override"}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var resolver = new ItemModelResolver(resources);
            var model = resolver.Resolve("minecraft:test_item", "minecraft:item/override");

            Assert.NotNull(model);
            Assert.Equal("minecraft:item/override", model!.ModelId);
            Assert.Equal("minecraft:item/override", model.Textures["layer0"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void Write(string root, string relativePath, string text)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
