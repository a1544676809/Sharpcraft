// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Per-render-type entity layer split (docs/decisions/0021).</summary>
[Collection("TrimRegistry")]
public sealed class EntityLayerTests
{
    private static EntityRenderState Entity(int typeId, int variant = 0) =>
        new(1, typeId, 0f, 0f, 0f, 0f, 0f, VariantId: variant);

    private static (Vector3 Min, Vector3 Max) Bounds(DynamicGeometry geometry)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i < geometry.VertexFloats; i += 14)
        {
            var point = new Vector3(geometry.Vertices[i], geometry.Vertices[i + 1], geometry.Vertices[i + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }

    private static IReadOnlyList<EntityGeometryLayer> Layers(int typeId, int variant = 0)
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        return mesher.BuildLayers([Entity(typeId, variant)], sprite, 64, 64, variants: EntityVariantTable.Builtin);
    }

    [Fact]
    public void CaveSpiderScalesTheSpiderGeometryUniformly()
    {
        var sprite = new SpriteRect(0, 0, 64, 32, 1);
        Assert.Equal(0.7f, EntityModelResolver.Resolve(CaveSpiderModelDefinition.CaveSpiderEntityTypeId).Scale);
        var spider = new EntityModelMesher().Build([Entity(SpiderModelDefinition.SpiderEntityTypeId)], sprite, 64, 32);
        var a = Bounds(spider);
        var cave = new EntityModelMesher().Build([Entity(CaveSpiderModelDefinition.CaveSpiderEntityTypeId)], sprite, 64, 32);
        var b = Bounds(cave);

        var sizeA = a.Max - a.Min;
        var sizeB = b.Max - b.Min;

        Assert.Equal(sizeA.X * 0.7f, sizeB.X, 2);
        Assert.Equal(sizeA.Y * 0.7f, sizeB.Y, 2);
        Assert.Equal(sizeA.Z * 0.7f, sizeB.Z, 2);
    }

    [Fact]
    public void AnimationPoseOffsetsAreAppliedBeforeMeshing()
    {
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var mesher = new EntityModelMesher();
        var normal = mesher.Build([Entity(PigModelDefinition.PigEntityTypeId)], sprite, 64, 64);
        var normalBounds = Bounds(normal);

        var pose = new EntityPose();
        pose.Add("head", new PoseOffset(new Vector3(0f, 10f, 0f), Vector3.Zero, Vector3.Zero));
        var posed = mesher.Build([Entity(PigModelDefinition.PigEntityTypeId)], sprite, 64, 64, pose: pose);
        var posedBounds = Bounds(posed);

        Assert.NotEqual(normalBounds.Max.Y, posedBounds.Max.Y);
    }
    [Fact]
    public void ModelUvsAreNormalisedByTheDeclaredTextureSizeNotTheImageSize()
    {
        // Ghast declares a 64x32 texture while its PNG is 128x64; UVs must be divided by 64/32, so the widest body
        // face still reaches U = 1. Normalising by the 128x64 image would only reach 0.5 and sample the wrong texels.
        var sprite = new SpriteRect(0, 0, 128, 64, 1);
        var geometry = new EntityModelMesher().Build([Entity(GhastModelDefinition.GhastEntityTypeId)], sprite, 128, 64);
        var maxU = 0f;
        for (var i = 6; i < geometry.VertexFloats; i += 14)
        {
            maxU = MathF.Max(maxU, geometry.Vertices[i]);
        }

        Assert.True(maxU > 0.9f, $"expected U near 1, got {maxU}");
    }

    [Fact]
    public void NonHumanoidEquipmentUsesIndependentModelLayers()
    {
        var state = Entity(HorseModelDefinition.HorseEntityTypeId) with
        {
            Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, 1407, 949)
        };
        var mesher = new EntityModelMesher();
        var layers = mesher.BuildLayers([state], new SpriteRect(0, 0, 64, 64, 1), 64, 64,
            variants: EntityVariantTable.Builtin);
        Assert.Contains(layers, layer => layer.Name == "equipment_HorseBody_6");
        Assert.Contains(layers, layer => layer.Name == "equipment_HorseSaddle_7");
        Assert.Contains(layers, layer => layer.Kind == EntityGeometryLayerKind.Elytra);
    }

    [Fact]
    public void PlayerBodySelectorUsesTheProfileSkinAndFallsBackToTheUuidDefault()
    {
        var model = EntityModelResolver.Resolve(EntityPlaceholderMesher.PlayerEntityTypeId);
        Assert.NotNull(model.TextureSelector);
        // Java-sampled index 15 -> entity/player/wide/steve.
        var state = Entity(EntityPlaceholderMesher.PlayerEntityTypeId) with
        {
            Uuid = Guid.Parse("61699b2e-d327-4a01-9f1e-0ea8c3f06bc6")
        };
        Assert.Equal("entity/player/wide/steve", model.TextureSelector!(state));
        Assert.Equal("skins/from-player", model.TextureSelector!(
            state with { PlayerBodyTexture = "skins/from-player" }));
    }

    [Fact]
    public void UsePlayerTexturePrefersTheElytraThenTheCape()
    {
        var elytra = ItemRegistry.LoadEmbedded().GetId(Identifier.Parse("minecraft:elytra"));
        Assert.True(elytra >= 0, "elytra item id missing from embedded registry");
        var state = Entity(EntityPlaceholderMesher.PlayerEntityTypeId) with
        {
            Equipment = new EntityEquipment(-1, -1, -1, -1, elytra, -1, -1, -1)
        };

        Assert.Equal("entity/equipment/wings/elytra", EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.Elytra, 4));
        Assert.Equal("elytra/from-player", EntityModelResolver.ResolveEquipmentTexture(
            state with { PlayerElytraTexture = "elytra/from-player" }, EquipmentAssetKind.Elytra, 4));
        Assert.Equal("capes/from-player", EntityModelResolver.ResolveEquipmentTexture(
            state with { PlayerCapeTexture = "capes/from-player" }, EquipmentAssetKind.Elytra, 4));
        Assert.Equal("entity/equipment/wings/elytra", EntityModelResolver.ResolveEquipmentTexture(
            state with { PlayerCapeTexture = "capes/from-player", ShowCape = false }, EquipmentAssetKind.Elytra, 4));

        // Only the avatar renderer passes a player texture; another humanoid wearing elytra keeps the layer texture.
        var zombie = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId) with
        {
            Equipment = new EntityEquipment(-1, -1, -1, -1, elytra, -1, -1, -1),
            PlayerElytraTexture = "elytra/from-player"
        };
        Assert.Equal("entity/equipment/wings/elytra", EntityModelResolver.ResolveEquipmentTexture(zombie, EquipmentAssetKind.Elytra, 4));
    }

    [Fact]
    public void ModelBackedLayersGroupSameModelEntitiesWithoutChangingLayerOrder()
    {
        var helmet = ItemRegistry.LoadEmbedded().GetId(Identifier.Parse("minecraft:iron_helmet"));
        Assert.True(helmet >= 0, "iron helmet item id missing from embedded registry");
        var armored = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId) with
        {
            Equipment = new EntityEquipment(-1, -1, -1, -1, -1, helmet, -1, -1)
        };
        var unarmored = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId);
        var sprite = new SpriteRect(0, 0, 64, 64, 1);

        var single = new EntityModelMesher().BuildLayers([armored], sprite, 64, 64, variants: EntityVariantTable.Builtin);
        var pair = new EntityModelMesher().BuildLayers([armored, armored], sprite, 64, 64, variants: EntityVariantTable.Builtin);
        var mixed = new EntityModelMesher().BuildLayers([armored, unarmored], sprite, 64, 64, variants: EntityVariantTable.Builtin);

        // Grouping must not change the layer plan's name/kind/order or the returned draw order.
        Assert.Equal(
            single.Select(layer => (layer.Name, layer.Kind, layer.Order)),
            pair.Select(layer => (layer.Name, layer.Kind, layer.Order)));
        Assert.Equal(
            single.Select(layer => (layer.Name, layer.Kind, layer.Order)),
            mixed.Select(layer => (layer.Name, layer.Kind, layer.Order)));

        // Two entities sharing one layer model are built in a single batch: every layer has exactly twice the geometry.
        foreach (var (one, two) in single.Zip(pair))
        {
            Assert.Equal(one.Geometry.VertexFloats * 2, two.Geometry.VertexFloats);
            Assert.Equal(one.Geometry.IndexCount * 2, two.Geometry.IndexCount);
        }

        // VisibleWhen is still evaluated per entity: the equipment layer only receives the armored entity, while the
        // base cutout layer receives both.
        var singleByName = single.ToDictionary(layer => layer.Name, StringComparer.Ordinal);
        var mixedByName = mixed.ToDictionary(layer => layer.Name, StringComparer.Ordinal);
        Assert.Equal(singleByName["cutout"].Geometry.IndexCount * 2, mixedByName["cutout"].Geometry.IndexCount);
        Assert.Equal(
            singleByName["equipment_HumanoidArmor_5"].Geometry.IndexCount,
            mixedByName["equipment_HumanoidArmor_5"].Geometry.IndexCount);

        // A batch mixing different models still yields one plan entry per (name, kind, order), with one group per
        // effective model; the cutout geometry is the sum of the two single-entity cutouts.
        var cow = Entity(CowModelDefinition.CowEntityTypeId);
        var cowCutout = Assert.Single(
            new EntityModelMesher().BuildLayers([cow], sprite, 64, 64, variants: EntityVariantTable.Builtin),
            layer => layer.Name == "cutout");
        var mixedModels = new EntityModelMesher().BuildLayers([armored, cow], sprite, 64, 64,
            variants: EntityVariantTable.Builtin);
        var mixedCutout = Assert.Single(mixedModels, layer => layer.Name == "cutout");
        Assert.Equal(
            singleByName["cutout"].Geometry.IndexCount + cowCutout.Geometry.IndexCount,
            mixedCutout.Geometry.IndexCount);
    }

    private static EntityRenderState BabyWithHelmet(int typeId)
    {
        var helmet = ItemRegistry.LoadEmbedded().GetId(Identifier.Parse("minecraft:iron_helmet"));
        Assert.True(helmet >= 0, "iron helmet item id missing from embedded registry");
        return new EntityRenderState(1, typeId, 0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, -1, helmet, -1, -1),
            IsBaby: true);
    }

    [Fact]
    public void GlintOverrideAddsAnIndependentEquipmentLayer()
    {
        var helmet = ItemRegistry.LoadEmbedded().GetId(Identifier.Parse("minecraft:iron_helmet"));
        Assert.True(helmet >= 0, "iron helmet item id missing from embedded registry");
        var glints = new bool?[8];
        glints[5] = true;
        var state = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId) with
        {
            Equipment = new EntityEquipment(-1, -1, -1, -1, -1, helmet, -1, -1),
            EquipmentGlintOverrides = glints
        };

        Assert.True(state.EquipmentGlintOverrideAt(5));
        Assert.Contains(EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId).RenderLayers,
            layer => layer.Name == "glint_HumanoidArmor_5");
        var variantModel = EntityVariantTable.Builtin.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId, 0);
        Assert.Contains(variantModel.RenderLayers, layer => layer.Name == "glint_HumanoidArmor_5");
        var variantGlint = variantModel.RenderLayers.Single(layer => layer.Name == "glint_HumanoidArmor_5");
        Assert.True(variantGlint.VisibleWhen!(state));
        Assert.NotNull(variantGlint.Model);
        Assert.NotNull(variantGlint.Model!.Compiled);
        var glintLayer = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId).RenderLayers
            .Single(layer => layer.Name == "glint_HumanoidArmor_5");
        Assert.True(glintLayer.VisibleWhen!(state));
        Assert.NotNull(glintLayer.Model);
        Assert.NotNull(glintLayer.Model!.Compiled);
        Assert.NotEmpty(glintLayer.Model.Compiled!.Cubes);
        Assert.All(glintLayer.Model.Compiled.Cubes, cube => Assert.True(cube.IncludeInEmissive));
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 64, 1), 64, 64, variants: EntityVariantTable.Builtin);

        Assert.Contains(layers, layer => layer.Name == "glint_HumanoidArmor_5");
    }

    [Fact]
    public void WitchHeldPotionUsesTheFlatItemQuadAndVisibilityFlag()
    {
        var holding = Entity(WitchModelDefinition.WitchEntityTypeId) with { IsHoldingPotion = true };
        var idle = holding with { IsHoldingPotion = false };
        var definition = EntityModelResolver.Resolve(WitchModelDefinition.WitchEntityTypeId);
        var layer = definition.RenderLayers.Single(entry => entry.Name == "held_potion");
        var part = WitchModelDefinition.HeldPotionDefinition.Compiled!.Find("held_potion");

        Assert.NotNull(part);
        Assert.True(layer.UsesItemAtlas);
        Assert.True(layer.VisibleWhen!(holding));
        Assert.False(layer.VisibleWhen!(idle));
        Assert.Contains(WitchModelDefinition.PotionTexture, WitchModelDefinition.HeldPotionDefinition.AdditionalTexturePaths);
        var itemAtlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [WitchModelDefinition.PotionTexture] = new(16, 16, new byte[16 * 16 * 4])
        });
        var layers = new EntityModelMesher().BuildLayers(
            [holding], new SpriteRect(0, 0, 16, 16, 1), 16, 16, itemAtlas: itemAtlas);
        var itemLayer = layers.Single(entry => entry.Name == "held_potion");
        Assert.True(itemLayer.UsesItemAtlas);
        Assert.True(itemLayer.Geometry.VertexFloats >= 8 * 4);
        var geometry = new EntityModelMesher().Build(
            [holding], new SpriteRect(0, 0, 16, 16, 1), 16, 16);
        Assert.True(geometry.VertexFloats >= 8 * 4);
    }

    [Fact]
    public void AdultHumanoidHasAnItemAtlasHeldItemLayer()
    {
        var definition = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId);
        Assert.Contains(definition.RenderLayers,
            layer => layer.Name == "held_item" && layer.UsesItemAtlas);
        var part = HeldItemModelDefinition.Definition.Compiled!.Find("held_item");
        Assert.NotNull(part);
        Assert.Equal(0f, part!.Cubes.Single().Depth);
    }

    [Fact]
    public void HappyGhastRopesRequireLeashAndHarness()
    {
        var harness = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:white_harness"));
        Assert.True(harness >= 0, "white_harness item id missing from embedded registry");
        var leashed = Entity(HappyGhastModelDefinition.HappyGhastEntityTypeId) with
        {
            IsLeashed = true,
            Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, harness, -1)
        };

        var adult = EntityModelResolver.Resolve(HappyGhastModelDefinition.HappyGhastEntityTypeId);
        var ropes = adult.RenderLayers.Single(layer => layer.Name == "happy_ghast_ropes");
        Assert.True(ropes.VisibleWhen!(leashed));
        Assert.False(ropes.VisibleWhen!(leashed with { IsLeashed = false }));
        Assert.False(ropes.VisibleWhen!(leashed with { Equipment = default }));

        var baby = EntityModelResolver.Resolve(HappyGhastModelDefinition.HappyGhastEntityTypeId, baby: true);
        Assert.Contains(baby.RenderLayers, layer => layer.Name == "happy_ghast_ropes");
        Assert.Contains(HappyGhastModelDefinition.RopesDefinition.Compiled!.Cubes,
            cube => cube.Name == "body" && MathF.Abs(cube.Grow - 0.2f) < 1e-6f);
        Assert.Contains(HappyGhastModelDefinition.RopesBabyDefinition.Compiled!.Cubes,
            cube => cube.Name == "inner_body" && MathF.Abs(cube.Grow - -0.3f) < 1e-6f);
        var layers = new EntityModelMesher().BuildLayers(
            [leashed], new SpriteRect(0, 0, 64, 64, 1), 64, 64, variants: EntityVariantTable.Builtin);
        Assert.Contains(layers, layer => layer.Name == "happy_ghast_ropes");
    }

    [Fact]
    public void EnchantmentPresenceEnablesTheArmorGlintLayer()
    {
        var enchanted = new bool?[8];
        enchanted[5] = true;
        var state = Entity(HumanoidMobModelDefinition.ZombieEntityTypeId) with
        {
            EquipmentHasEnchantments = enchanted
        };

        Assert.True(EntityModelResolver.ResolveEquipmentFoil(state, 5));
        var glint = EntityModelResolver.Resolve(HumanoidMobModelDefinition.ZombieEntityTypeId).RenderLayers
            .Single(layer => layer.Name == "glint_HumanoidArmor_5");
        Assert.True(glint.VisibleWhen!(state));
    }

    [Fact]
    public void ModelGroupingPreservesInterleavedSubmissionOrder()
    {
        var states = new[]
        {
            Entity(HumanoidMobModelDefinition.DrownedEntityTypeId),
            Entity(SlimeModelDefinition.SlimeEntityTypeId),
            Entity(HumanoidMobModelDefinition.DrownedEntityTypeId)
        };
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var mesher = new EntityModelMesher();
        var combined = mesher.BuildLayers(states, sprite, 64, 64, variants: EntityVariantTable.Builtin);
        var reference = new Dictionary<string, List<float>>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            var single = new EntityModelMesher().BuildLayers([state], sprite, 64, 64, variants: EntityVariantTable.Builtin);
            foreach (var layer in single)
            {
                if (!reference.TryGetValue(layer.Name, out var values))
                {
                    values = [];
                    reference[layer.Name] = values;
                }

                for (var i = 0; i < layer.Geometry.VertexFloats; i++)
                {
                    values.Add(layer.Geometry.Vertices[i]);
                }
            }
        }

        var changed = 0;
        var translucentChanged = 0;
        foreach (var layer in combined)
        {
            if (!reference.TryGetValue(layer.Name, out var expected))
            {
                continue;
            }

            var actual = layer.Geometry.Vertices;
            var mismatches = 0;
            for (var i = 0; i < layer.Geometry.VertexFloats && i < expected.Count; i++)
            {
                if (actual[i] != expected[i])
                {
                    mismatches++;
                }
            }

            if (mismatches > 0)
            {
                changed++;
                if (layer.Kind == EntityGeometryLayerKind.Translucent)
                {
                    translucentChanged++;
                }
            }
        }

        Assert.Equal(0, changed);
        Assert.Equal(0, translucentChanged);
    }

    [Fact]
    public void BabyHumanoidArmorUsesIndependentModelLayers()
    {
        var state = BabyWithHelmet(154);
        var layers = new EntityModelMesher().BuildLayers(
            [state], new SpriteRect(0, 0, 64, 64, 1), 64, 64, variants: EntityVariantTable.Builtin);

        var armor = Assert.Single(layers, layer => layer.Name == "equipment_HumanoidArmorBaby_5");
        Assert.Equal(EntityGeometryLayerKind.Elytra, armor.Kind);
        Assert.True(armor.Geometry.IndexCount > 0);

        var unarmored = state with { Equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, -1, -1) };
        var plain = new EntityModelMesher().BuildLayers(
            [unarmored], new SpriteRect(0, 0, 64, 64, 1), 64, 64, variants: EntityVariantTable.Builtin);
        Assert.DoesNotContain(plain, layer => layer.Name == "equipment_HumanoidArmorBaby_5");
    }

    [Fact]
    public void BabyPiglinArmorUsesIndependentModelLayers()
    {
        var layers = new EntityModelMesher().BuildLayers(
            [BabyWithHelmet(102)], new SpriteRect(0, 0, 64, 64, 1), 64, 64,
            variants: EntityVariantTable.Builtin);
        Assert.Contains(layers, layer => layer.Name == "equipment_HumanoidArmorBaby_5");

        var piglinArmor = Assert.Single(EntityModelResolver.Resolve(102, true).RenderLayers,
            layer => layer.Name == "equipment_HumanoidArmorBaby_5");
        var zombieArmor = Assert.Single(EntityModelResolver.Resolve(154, true).RenderLayers,
            layer => layer.Name == "equipment_HumanoidArmorBaby_5");
        Assert.NotNull(piglinArmor.Model?.Compiled);
        Assert.NotSame(piglinArmor.Model!.Compiled, zombieArmor.Model!.Compiled);
        Assert.NotNull(piglinArmor.Model.Compiled!.Find("left_ear"));
    }

    [Fact]
    public void LayerOrderComesFromExplicitDefinitions()
    {
        var layers = Layers(HumanoidMobModelDefinition.DrownedEntityTypeId);
        var cutout = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Cutout);
        var translucent = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Translucent);
        Assert.Equal(0, cutout.Order);
        Assert.Equal(20, translucent.Order);
        Assert.Equal(4, EntityModelDefinition.DefaultRenderLayers.Count);
        Assert.Equal(0, EntityModelDefinition.DefaultRenderLayers[0].Order);
    }

    [Fact]
    public void AModelWithoutOverlaysHasOnlyTheCutoutLayer()
    {
        var layers = Layers(CowModelDefinition.CowEntityTypeId);

        var layer = Assert.Single(layers);
        Assert.Equal(EntityGeometryLayerKind.Cutout, layer.Kind);
    }

    [Fact]
    public void PlayerOverlaysBecomeATranslucentLayer()
    {
        var layers = Layers(EntityPlaceholderMesher.PlayerEntityTypeId);

        Assert.Equal(2, layers.Count);
        Assert.Equal(EntityGeometryLayerKind.Cutout, layers[0].Kind);
        Assert.Equal(EntityGeometryLayerKind.Translucent, layers[1].Kind);
    }

    [Fact]
    public void DrownedOuterAndSlimeOuterBecomeTranslucentLayers()
    {
        var drowned = Layers(HumanoidMobModelDefinition.DrownedEntityTypeId);
        var slime = Layers(SlimeModelDefinition.SlimeEntityTypeId);

        Assert.Contains(drowned, layer => layer.Kind == EntityGeometryLayerKind.Translucent);
        Assert.Contains(slime, layer => layer.Kind == EntityGeometryLayerKind.Translucent);
    }

    [Fact]
    public void AnEyesModelGetsAnEmissiveLayerUsingTheEyesTexture()
    {
        var atlas = EntitySkinAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/enderman/enderman"] = new TextureData(64, 32, new byte[64 * 32 * 4]),
            ["entity/enderman/enderman_eyes"] = new TextureData(64, 32, new byte[64 * 32 * 4])
        });

        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 32, 1);
        var layers = mesher.BuildLayers(
            [Entity(HumanoidMobModelDefinition.EndermanEntityTypeId)], sprite, 64, 32, skinAtlas: atlas);

        var eyes = Assert.Single(layers, layer => layer.Kind == EntityGeometryLayerKind.Emissive);
        Assert.True(eyes.Geometry.IndexCount > 0);
        Assert.Contains(layers, layer => layer.Kind == EntityGeometryLayerKind.Cutout);
    }

    [Fact]
    public void AModelWithoutEyesTextureHasNoEmissiveLayer()
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var layers = mesher.BuildLayers([Entity(CowModelDefinition.CowEntityTypeId)], sprite, 64, 64);

        Assert.DoesNotContain(layers, layer => layer.Kind == EntityGeometryLayerKind.Emissive);
    }

    [Fact]
    public void TheMergedBuildMatchesTheSumOfTheLayers()
    {
        var mesher = new EntityModelMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var model = Entity(EntityPlaceholderMesher.PlayerEntityTypeId);

        var layers = mesher.BuildLayers([model], sprite, 64, 64, variants: EntityVariantTable.Builtin);
        var merged = mesher.Build([model], sprite, 64, 64, variants: EntityVariantTable.Builtin);

        Assert.Equal(layers.Sum(layer => layer.Geometry.VertexFloats), merged.VertexFloats);
        Assert.Equal(layers.Sum(layer => layer.Geometry.IndexCount), merged.IndexCount);
    }
}

