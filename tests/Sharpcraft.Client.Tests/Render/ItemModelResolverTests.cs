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
    public void BlockElementsResolveAutoUvAndTextureIndirection()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            // items/stone.json -> block/stone -> block/cube_all -> block/cube (elements) -> block/block.
            Write(root, "assets/minecraft/items/test_stone.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:block/test_stone"}}""");
            Write(root, "assets/minecraft/models/block/test_stone.json",
                """{"parent":"minecraft:block/test_cube_all","textures":{"all":"minecraft:block/test_stone_tex"}}""");
            Write(root, "assets/minecraft/models/block/test_cube_all.json",
                """{"parent":"minecraft:block/test_cube","textures":{"particle":"#all","down":"#all","up":"#all","north":"#all","east":"#all","south":"#all","west":"#all"}}""");
            Write(root, "assets/minecraft/models/block/test_cube.json",
                """
                {"parent":"minecraft:block/test_block","elements":[
                  {"from":[0,0,0],"to":[16,16,16],"faces":{
                    "down":{"texture":"#down","cullface":"down"},
                    "up":{"texture":"#up","cullface":"up"},
                    "north":{"texture":"#north","cullface":"north"},
                    "south":{"texture":"#south","cullface":"south"},
                    "west":{"texture":"#west","cullface":"west"},
                    "east":{"texture":"#east","cullface":"east"}}}]}
                """);
            Write(root, "assets/minecraft/models/block/test_block.json",
                """{"gui_light":"side","display":{"ground":{"translation":[0,3,0],"scale":[0.25,0.25,0.25]}}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var model = new ItemModelResolver(resources).Resolve("minecraft:test_stone");

            Assert.NotNull(model);
            var element = Assert.Single(model!.Cuboids);
            Assert.Equal(new System.Numerics.Vector3(0f, 0f, 0f), element.From);
            Assert.Equal(new System.Numerics.Vector3(16f, 16f, 16f), element.To);
            Assert.Equal(6, element.Faces.Count);

            // "#down" -> "#all" -> minecraft:block/test_stone_tex resolves to the path form item atlases use.
            Assert.Equal("block/test_stone_tex", element.Faces[ModelFaceDirection.Down].Texture);
            Assert.Equal(ModelFaceDirection.Down, element.Faces[ModelFaceDirection.Down].CullFace);
            // Full-cube auto UV for DOWN is (from.x, 16-to.z, to.x, 16-from.z) = (0,0,16,16);
            // NORTH is (16-to.x, 16-to.y, 16-from.x, 16-from.y) which also collapses to (0,0,16,16) on a full cube.
            Assert.Equal(new System.Numerics.Vector4(0f, 0f, 16f, 16f), element.Faces[ModelFaceDirection.Down].Uv);
            Assert.Equal(new System.Numerics.Vector4(0f, 0f, 16f, 16f), element.Faces[ModelFaceDirection.North].Uv);
            // A partial element proves the projection actually flips: from (2,0,1) to (10,4,9) gives NORTH
            // (16-10, 16-4, 16-2, 16-0) = (6,12,14,16).
            Assert.Equal(
                new System.Numerics.Vector4(6f, 12f, 14f, 16f),
                ItemModelElement.AutoUv(
                    ModelFaceDirection.North,
                    new System.Numerics.Vector3(2f, 0f, 1f),
                    new System.Numerics.Vector3(10f, 4f, 9f)));

            // The ground transform comes from block/block, three parents up.
            var ground = model.Display[ItemDisplayContext.Ground];
            Assert.Equal(3f * 0.0625f, ground.Translation.Y, 6);
            Assert.Equal(0.25f, ground.Scale.X, 6);
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
    public void ItemTintsAreParsedFromTheItemDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/test_leaves.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:block/test_leaves","tints":[{"type":"minecraft:constant","value":-12012264}]}}""");
            Write(root, "assets/minecraft/models/block/test_leaves.json",
                """{"parent":"minecraft:block/test_cube_all","textures":{"all":"minecraft:block/test_leaves_tex"}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var model = new ItemModelResolver(resources).Resolve("minecraft:test_leaves");

            Assert.NotNull(model);
            var tint = Assert.Single(model!.TintSources);
            Assert.Equal(ItemTintKind.Constant, tint.Kind);
            // A face with tintindex 0 gets the constant colour; a face without one stays white.
            Assert.NotEqual(System.Numerics.Vector3.One, model.TintFor(0, grass: null));
            Assert.Equal(System.Numerics.Vector3.One, model.TintFor(-1, grass: null));
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
    public void DisplayTranslationIsScaledThenClampedToFiveBlocks()
    {
        // Reference ItemTransform.Deserializer multiplies by 0.0625 and only then clamps to +/-5 blocks, so an oversized
        // translation saturates at five blocks rather than at 5/16 of a block.
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/test_far.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/far"}}""");
            Write(root, "assets/minecraft/models/item/far.json",
                """{"parent":"builtin/generated","textures":{"layer0":"minecraft:item/far"},"display":{"ground":{"translation":[100,-100,2]}}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var model = new ItemModelResolver(resources).Resolve("minecraft:test_far");

            var ground = model!.Display[ItemDisplayContext.Ground];
            Assert.Equal(5f, ground.Translation.X, 6);
            Assert.Equal(-5f, ground.Translation.Y, 6);
            Assert.Equal(2f * 0.0625f, ground.Translation.Z, 6);

            // NO_TRANSFORM still centres the model; a real transform centres first, then scales, rotates and translates,
            // so the model centre (0.5, 0.5, 0.5) lands exactly on the transform's translation.
            var centred = ItemDisplayTransform.None.ToMatrix();
            Assert.Equal(new System.Numerics.Vector3(-0.5f, -0.5f, -0.5f),
                System.Numerics.Vector3.Transform(System.Numerics.Vector3.Zero, centred));
            Assert.Equal(System.Numerics.Vector3.Zero,
                System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0.5f), centred));

            var scaled = new ItemDisplayTransform(default, new System.Numerics.Vector3(0.25f, 0f, 0f),
                new System.Numerics.Vector3(2f, 2f, 2f)).ToMatrix();
            Assert.Equal(new System.Numerics.Vector3(0.25f, 0f, 0f),
                System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0.5f), scaled));
            // A scaled corner keeps its distance from that centre: half a block becomes a quarter.
            Assert.Equal(new System.Numerics.Vector3(-0.75f, -1f, -1f),
                System.Numerics.Vector3.Transform(System.Numerics.Vector3.Zero, scaled));
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
    public void LeftHandFixNegatesTheXTranslationAndYZRotations()
    {
        var transform = new ItemDisplayTransform(
            new System.Numerics.Vector3(10f, 20f, 30f),
            new System.Numerics.Vector3(0.5f, 0.25f, 0f),
            System.Numerics.Vector3.One);

        var right = transform.ToMatrix();
        var left = transform.ToMatrix(applyLeftHandFix: true);
        Assert.NotEqual(right, left);

        // The reference negates translation.x and rotation.y/z, and leaves translation.y/z and rotation.x alone.
        var flipped = new ItemDisplayTransform(
            new System.Numerics.Vector3(10f, -20f, -30f),
            new System.Numerics.Vector3(-0.5f, 0.25f, 0f),
            System.Numerics.Vector3.One);
        Assert.Equal(flipped.ToMatrix(), left);
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
