// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// task-271: held items append an item-glint pass with the reference item scale (8.0), using the same quads as the
/// base item; non-foil items get no glint layer. The foil flag comes from ItemComponentInfo, not from the item id.
/// </summary>
public sealed class ItemGlintTests
{
    [Fact]
    public void HeldFoilItemAddsOneGlintPassWithTheItemScale()
    {
        var paperId = EntityModelResolver.Items?.GetId(Identifier.Parse("minecraft:paper")) ?? -1;
        Assert.True(paperId > 0);
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-glint-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/paper.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/paper_cube"}}""");
            Write(root, "assets/minecraft/models/item/paper_cube.json",
                """
                {"textures":{"particle":"item/paper","all":"item/paper"},"elements":[
                  {"from":[0,0,0],"to":[16,16,16],"faces":{
                    "down":{"texture":"#all"},"up":{"texture":"#all"},"north":{"texture":"#all"},
                    "south":{"texture":"#all"},"west":{"texture":"#all"},"east":{"texture":"#all"}}}]}
                """);
            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-glint-test"));
            using var resources = new ResourceManager(stack, 1);
            var oldModels = EntityModelResolver.ItemModels;
            EntityModelResolver.ItemModels = new ItemModelResolver(resources);
            try
            {
                var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
                {
                    ["item/paper"] = new(16, 16, new byte[16 * 16 * 4]),
                    [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4]),
                });
                var state = new EntityRenderState(
                    1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f,
                    MainHandItemId: paperId,
                    MainHandGlint: true);
                GlintClock.SetFixedSourceMillis(55000);
                var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
                var cutout = layers.Single(layer => layer.Name == ItemInHandMesher.LayerName);
                var glint = layers.Single(layer => layer.Name == ItemInHandMesher.LayerName + "_glint");
                var baseUvs = ReadUvs(cutout.Geometry);
                var glintUvs = ReadUvs(glint.Geometry);
                Assert.Equal(baseUvs.Count, glintUvs.Count);
                Assert.Equal(ReadPositions(cutout.Geometry), ReadPositions(glint.Geometry));
                for (var i = 0; i < baseUvs.Count; i++)
                {
                    var expected = GlintTextureTransform.Apply(
                        baseUvs[i].U, baseUvs[i].V, 55000, GlintTextureTransform.ItemScale);
                    Assert.Equal((float)(Half)expected.U, glintUvs[i].U, 3);
                    Assert.Equal((float)(Half)expected.V, glintUvs[i].V, 3);
                }

                var plain = new ItemInHandMesher().Build(
                    [state with { MainHandGlint = false }], [new EntityPose()], variants: null, atlas);
                Assert.DoesNotContain(plain, layer => layer.Name.EndsWith("_glint", StringComparison.Ordinal));
            }
            finally
            {
                EntityModelResolver.ItemModels = oldModels;
                GlintClock.SetProvider(null);
            }
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
    public void SpecialFoilTridentAndShieldGetEntityScaleGlint()
    {
        var trident = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:trident"));
        var shield = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:shield"));
        Assert.True(trident > 0);
        Assert.True(shield > 0);
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-special-foil-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(root, "assets/minecraft/items/trident.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/default","model":{"type":"minecraft:trident"}}}""");
            Write(root, "assets/minecraft/items/shield.json",
                """{"model":{"type":"minecraft:special","base":"minecraft:item/default","model":{"type":"minecraft:shield"}}}""");
            Write(root, "assets/minecraft/models/item/default.json",
                """{"parent":"builtin/generated","textures":{"layer0":"minecraft:item/default"}}""");
            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "special-foil-test"));
            using var resources = new ResourceManager(stack, 1);
            var oldModels = EntityModelResolver.ItemModels;
            EntityModelResolver.ItemModels = new ItemModelResolver(resources);
            var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
            {
                [SpecialItemModelDefinition.TridentTexture] = new(64, 64, new byte[64 * 64 * 4]),
                [SpecialItemModelDefinition.ShieldTexture] = new(64, 64, new byte[64 * 64 * 4]),
                [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4]),
            });        try
        {
            GlintClock.SetFixedSourceMillis(55000);
            var foil = new EntityRenderState(
                1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f,
                MainHandItemId: trident,
                OffHandItemId: shield,
                MainHandGlint: true,
                OffHandGlint: true);
            var mesher = new EntityModelMesher();
            var layers = mesher.BuildLayers([foil], new SpriteRect(0, 0, 64, 64, 1), 64, 64,
                variants: null, itemAtlas: atlas);
            AssertSpecialGlint(layers, "held_special_trident_right", GlintTextureTransform.EntityScale);
            AssertSpecialGlint(layers, "held_special_shield_left", GlintTextureTransform.EntityScale);

            var plain = foil with { MainHandGlint = false, OffHandGlint = false };
            var plainLayers = mesher.BuildLayers([plain], new SpriteRect(0, 0, 64, 64, 1), 64, 64,
                variants: null, itemAtlas: atlas);
            Assert.DoesNotContain(plainLayers, layer => layer.Name.EndsWith("_glint", StringComparison.Ordinal));
            }
            finally
            {
                GlintClock.SetProvider(null);
                EntityModelResolver.ItemModels = oldModels;
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void AssertSpecialGlint(IReadOnlyList<EntityGeometryLayer> layers, string baseName, float scale)
    {
        var @base = layers.Single(layer => layer.Name == baseName);
        var glint = layers.Single(layer => layer.Name == baseName + "_glint");
        var baseUvs = ReadUvs(@base.Geometry);
        var glintUvs = ReadUvs(glint.Geometry);
        Assert.Equal(baseUvs.Count, glintUvs.Count);
        Assert.Equal(ReadPositions(@base.Geometry), ReadPositions(glint.Geometry));
        for (var i = 0; i < baseUvs.Count; i++)
        {
            var expected = GlintTextureTransform.Apply(baseUvs[i].U, baseUvs[i].V, 55000, scale);
            Assert.Equal((float)(Half)expected.U, glintUvs[i].U, 3);
            Assert.Equal((float)(Half)expected.V, glintUvs[i].V, 3);
        }
    }
    [Fact]
    public void HandFoilComesFromGlintOverrideOrEnchantmentPresence()
    {
        var tracker = new EntityTracker();
        Assert.True(tracker.Add(new AddedEntity(7, Guid.Empty, EntityPlaceholderMesher.PlayerEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        var paper = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:paper"));
        Assert.True(tracker.ApplyEquipment(7, [new EquipmentItem(0, paper, 1, new ItemComponentInfo(HasEnchantments: true))]));
        Assert.True(tracker.Interpolate(0f)[0].MainHandGlint);

        Assert.True(tracker.ApplyEquipment(7, [new EquipmentItem(0, paper, 1, new ItemComponentInfo(GlintOverride: false, HasEnchantments: true))]));
        Assert.False(tracker.Interpolate(0f)[0].MainHandGlint);

        Assert.True(tracker.ApplyEquipment(7, [new EquipmentItem(0, paper, 1, new ItemComponentInfo(GlintOverride: true, HasEnchantments: false))]));
        Assert.True(tracker.Interpolate(0f)[0].MainHandGlint);
    }

    private static void Write(string root, string relative, string contents)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static List<(float U, float V)> ReadUvs(DynamicGeometry geometry)
    {
        var values = new List<(float U, float V)>();
        for (var vertex = 0; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            values.Add(VertexFieldReader.Uv(geometry.Vertices, vertex));
        }

        return values;
    }

    private static List<(float X, float Y, float Z)> ReadPositions(DynamicGeometry geometry)
    {
        var values = new List<(float X, float Y, float Z)>();
        for (var vertex = 0; vertex < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; vertex++)
        {
            values.Add(VertexFieldReader.Position(geometry.Vertices, vertex));
        }

        return values;
    }
}
