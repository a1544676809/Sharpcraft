// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Assets;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Issue #221: <c>FoilType.SPECIAL</c>. A foiled compass or clock keeps the ordinary item geometry, but its glint
/// samples the reference decal coordinate (UV3) run through the glint texture matrix instead of the sprite UV, which
/// is why the same quads cannot reuse the standard glint layer.
/// </summary>
public sealed class FoilSpecialGlintTests
{
    private const long FixedMillis = 55000;

    [Fact]
    public void SourceDecalSamplesMatchForEveryFacingAndPoseScale()
    {
        var rows = ReadRows();
        Assert.Equal(96, rows.Count);
        Assert.Contains(rows, row => row.Surface == "sprite");

        foreach (var row in rows)
        {
            var uv3 = ItemFoilDecal.Uv(new System.Numerics.Vector3(row.X, row.Y, row.Z), row.Direction, row.DecalScale);
            Assert.True(MathF.Abs(row.Uv3U - uv3.X) < 2e-6f, $"uv3.u {row.Direction}/{row.Corner}: {uv3.X} vs {row.Uv3U}");
            Assert.True(MathF.Abs(row.Uv3V - uv3.Y) < 2e-6f, $"uv3.v {row.Direction}/{row.Corner}: {uv3.Y} vs {row.Uv3V}");

            var glint = GlintTextureTransform.ApplyDecal(row.Uv3U, row.Uv3V, FixedMillis, GlintTextureTransform.ItemScale);
            Assert.True(MathF.Abs(row.GlintU - glint.U) < 2e-5f, $"glint.u {row.Direction}/{row.Corner}: {glint.U} vs {row.GlintU}");
            Assert.True(MathF.Abs(row.GlintV - glint.V) < 2e-5f, $"glint.v {row.Direction}/{row.Corner}: {glint.V} vs {row.GlintV}");
        }

        // The decal pose scale is the only context-dependent input: third person keeps the vertex, the GUI halves the
        // pose (doubling the decal coordinate) and first person scales it by 0.75 (four thirds).
        Assert.Equal(1f, ItemFoilDecal.ScaleFor(ItemDisplayContext.ThirdPersonRightHand));
        Assert.Equal(1f, ItemFoilDecal.ScaleFor(ItemDisplayContext.Ground));
        Assert.Equal(2f, ItemFoilDecal.ScaleFor(ItemDisplayContext.Gui));
        Assert.Equal(1.3333334f, ItemFoilDecal.ScaleFor(ItemDisplayContext.FirstPersonLeftHand));
    }

    [Fact]
    public void HeldCompassAndClockUseTheSpecialDecalGlint()
    {
        var compassId = EntityModelResolver.Items?.GetId(Identifier.Parse("minecraft:compass")) ?? -1;
        var clockId = EntityModelResolver.Items?.GetId(Identifier.Parse("minecraft:clock")) ?? -1;
        Assert.True(compassId > 0);
        Assert.True(clockId > 0);

        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-foil-special-" + Guid.NewGuid().ToString("N"));
        try
        {
            // The shipped definitions resolve through condition/select/range_dispatch down to the generated 16x16
            // sprite model; the fixture keeps that shape with a single range entry.
            Write(root, "assets/minecraft/items/compass.json",
                """
                {"model":{"type":"minecraft:condition","property":"minecraft:has_component","component":"minecraft:lodestone_tracker",
                "on_false":{"type":"minecraft:range_dispatch","property":"minecraft:compass","scale":32.0,"target":"spawn",
                "entries":[{"model":{"type":"minecraft:model","model":"minecraft:item/compass_16"},"threshold":0.0}]},
                "on_true":{"type":"minecraft:range_dispatch","property":"minecraft:compass","scale":32.0,"target":"lodestone",
                "entries":[{"model":{"type":"minecraft:model","model":"minecraft:item/compass_16"},"threshold":0.0}]}}}
                """);
            Write(root, "assets/minecraft/items/clock.json",
                """
                {"model":{"type":"minecraft:select","property":"minecraft:context_dimension","cases":[],
                "fallback":{"type":"minecraft:range_dispatch","property":"minecraft:time","scale":64.0,"source":"random",
                "entries":[{"model":{"type":"minecraft:model","model":"minecraft:item/clock_00"},"threshold":0.0}]}}}
                """);
            Write(root, "assets/minecraft/items/paper.json",
                """{"model":{"type":"minecraft:model","model":"minecraft:item/paper"}}""");
            Write(root, "assets/minecraft/models/item/generated.json", """{"parent":"builtin/generated"}""");
            Write(root, "assets/minecraft/models/item/compass_16.json",
                """{"parent":"minecraft:item/generated","textures":{"layer0":"minecraft:item/compass_16"}}""");
            Write(root, "assets/minecraft/models/item/clock_00.json",
                """{"parent":"minecraft:item/generated","textures":{"layer0":"minecraft:item/clock_00"}}""");
            Write(root, "assets/minecraft/models/item/paper.json",
                """{"parent":"minecraft:item/generated","textures":{"layer0":"minecraft:item/paper"}}""");

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "foil-special-test"));
            using var resources = new ResourceManager(stack, 1);
            var oldModels = EntityModelResolver.ItemModels;
            EntityModelResolver.ItemModels = new ItemModelResolver(resources);
            try
            {
                var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
                {
                    ["item/compass_16"] = new(16, 16, new byte[16 * 16 * 4]),
                    ["item/clock_00"] = new(16, 16, new byte[16 * 16 * 4]),
                    ["item/paper"] = new(16, 16, new byte[16 * 16 * 4]),
                    [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4]),
                });
                GlintClock.SetFixedSourceMillis(FixedMillis);
                try
                {
                    foreach (var itemId in new[] { compassId, clockId })
                    {
                        var state = new EntityRenderState(
                            1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f,
                            MainHandItemId: itemId,
                            MainHandFoil: ItemFoilType.Special);
                        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
                        AssertSpecialGlint(state, atlas, layers);
                    }

                    // A standard foil item keeps the ordinary glint pass: the special path is selected by the item,
                    // not by the foil flag alone.
                    var paperId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:paper"));
                    var plain = new EntityRenderState(
                        2, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f,
                        MainHandItemId: paperId,
                        MainHandFoil: ItemFoilType.Standard);
                    var plainLayers = new ItemInHandMesher().Build([plain], [new EntityPose()], variants: null, atlas);
                    var standardGlint = plainLayers.Single(layer => layer.Name == ItemInHandMesher.LayerName + "_glint");
                    Assert.Equal(EntityGeometryLayerKind.Glint, standardGlint.Kind);
                    Assert.DoesNotContain(plainLayers, layer =>
                        layer.Name == ItemInHandMesher.LayerName + "_glint_special");

                    // No foil at all: neither glint pass exists.
                    var unfoiled = plain with { MainHandFoil = ItemFoilType.None };
                    var unfoiledLayers = new ItemInHandMesher().Build([unfoiled], [new EntityPose()], variants: null, atlas);
                    Assert.DoesNotContain(unfoiledLayers, layer => layer.Name.EndsWith("_glint", StringComparison.Ordinal)
                        || layer.Name.EndsWith("_glint_special", StringComparison.Ordinal));
                }
                finally
                {
                    GlintClock.SetProvider(null);
                }
            }
            finally
            {
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

    [Fact]
    public void FoilTypeComesFromComponentsAndTheCompassClockItemSet()
    {
        var compassId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:compass"));
        var recoveryCompassId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:recovery_compass"));
        var clockId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:clock"));
        var paperId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:paper"));
        Assert.True(compassId > 0 && recoveryCompassId > 0 && clockId > 0 && paperId > 0);

        var oldTags = EntityModelResolver.ItemTags;
        EntityModelResolver.ItemTags = ItemTagRegistry.Parse(
        [
            (ItemFoil.CompassesTag,
                """{"values":["minecraft:compass","minecraft:recovery_compass"]}""")
        ]);
        try
        {
            Assert.Equal(ItemFoilType.Special, ItemFoil.Resolve(new ItemComponentInfo(HasEnchantments: true), compassId));
            Assert.Equal(ItemFoilType.Special, ItemFoil.Resolve(new ItemComponentInfo(GlintOverride: true), recoveryCompassId));
            Assert.Equal(ItemFoilType.Special, ItemFoil.Resolve(new ItemComponentInfo(HasEnchantments: true), clockId));
            Assert.Equal(ItemFoilType.Standard, ItemFoil.Resolve(new ItemComponentInfo(HasEnchantments: true), paperId));
            Assert.Equal(ItemFoilType.None, ItemFoil.Resolve(new ItemComponentInfo(), compassId));
            Assert.Equal(ItemFoilType.None, ItemFoil.Resolve(new ItemComponentInfo(GlintOverride: false, HasEnchantments: true), clockId));

            // The render state carries the three-state foil, not a bool.
            var tracker = new EntityTracker();
            Assert.True(tracker.Add(new AddedEntity(7, Guid.Empty, EntityPlaceholderMesher.PlayerEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
            Assert.True(tracker.ApplyEquipment(7, [new EquipmentItem(0, compassId, 1, new ItemComponentInfo(HasEnchantments: true))]));
            Assert.Equal(ItemFoilType.Special, tracker.Interpolate(0f)[0].MainHandFoil);
            Assert.True(tracker.ApplyEquipment(7, [new EquipmentItem(0, paperId, 1, new ItemComponentInfo(HasEnchantments: true))]));
            Assert.Equal(ItemFoilType.Standard, tracker.Interpolate(0f)[0].MainHandFoil);
            Assert.True(tracker.ApplyEquipment(7, [new EquipmentItem(0, paperId, 1, new ItemComponentInfo())]));
            Assert.Equal(ItemFoilType.None, tracker.Interpolate(0f)[0].MainHandFoil);
        }
        finally
        {
            EntityModelResolver.ItemTags = oldTags;
        }
    }

    /// <summary>
    /// Compares the production special glint layer against the sample's sprite rows, keyed by
    /// <c>surface|direction|corner</c>: every key binds the base position, the base sprite UV and the glint UV, so a
    /// changed vertex order cannot silently pass. The world-space base positions are rebased onto the sample's
    /// model-space positions through the single measured model scale, which also proves the decal input positions.
    /// </summary>
    private static void AssertSpecialGlint(EntityRenderState state, ISpriteAtlas atlas, IReadOnlyList<EntityGeometryLayer> layers)
    {
        var cutout = layers.Single(layer => layer.Name == ItemInHandMesher.LayerName);
        var glint = layers.Single(layer => layer.Name == ItemInHandMesher.LayerName + "_glint_special");
        Assert.Equal(EntityGeometryLayerKind.SpecialGlint, glint.Kind);
        Assert.DoesNotContain(layers, layer => layer.Name == ItemInHandMesher.LayerName + "_glint");

        var sample = ReadRows()
            .Where(row => row.Surface == "sprite" && row.PoseScale == 1f)
            .ToDictionary(row => (row.Direction, row.Corner), row => row);
        Assert.Equal(8, sample.Count);

        var placements = EntityModelResolver.ResolveHeldItemPlacements(state, offhand: false);
        Assert.Single(placements);
        var quads = new List<ItemModelQuad>();
        ItemEntityMesher.CollectQuads(quads, placements[0].Model);
        var spriteQuads = quads.ToDictionary(quad => quad.Direction, quad => quad);

        var basePositions = ReadPositions(cutout.Geometry);
        var glintPositions = ReadPositions(glint.Geometry);
        var baseUvs = ReadUvs(cutout.Geometry);
        var glintUvs = ReadUvs(glint.Geometry);
        Assert.Equal(8, basePositions.Count);
        Assert.Equal(basePositions, glintPositions);

        var bound = new Dictionary<(ModelFaceDirection Direction, int Corner), (System.Numerics.Vector3 Model, System.Numerics.Vector3 World)>();
        for (var vertex = 0; vertex < 8; vertex++)
        {
            // The generated sprite submits south then north, four corners each, in the reference FaceInfo order.
            var direction = vertex < 4 ? ModelFaceDirection.South : ModelFaceDirection.North;
            var corner = vertex % 4;
            Assert.True(sample.TryGetValue((direction, corner), out var row), $"sample misses {direction}|{corner}");
            Assert.True(spriteQuads.ContainsKey(direction), $"model has no {direction} quad");

            var spriteUv = spriteQuads[direction].Uv(corner);
            Assert.Equal(spriteUv.X, baseUvs[vertex].U, 4);
            Assert.Equal(spriteUv.Y, baseUvs[vertex].V, 4);

            Assert.Equal(row.GlintU, glintUvs[vertex].U, 3);
            Assert.Equal(row.GlintV, glintUvs[vertex].V, 3);

            bound[(direction, corner)] = (
                new System.Numerics.Vector3(row.X, row.Y, row.Z),
                new System.Numerics.Vector3(basePositions[vertex].X, basePositions[vertex].Y, basePositions[vertex].Z));
        }

        Assert.Equal(8, bound.Count);

        // Same model x/y corner on the south and north face gives the model scale the item transform applies; every other
        // pair must then match scale * model distance, which binds all eight world positions to their sample keys.
        var southOne = bound[(ModelFaceDirection.South, 1)];
        var northTwo = bound[(ModelFaceDirection.North, 2)];
        var scale = Distance(southOne.World, northTwo.World) / Distance(southOne.Model, northTwo.Model);
        Assert.True(scale > 0f);
        var keys = bound.Keys.ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            for (var j = i + 1; j < keys.Length; j++)
            {
                var modelDistance = Distance(bound[keys[i]].Model, bound[keys[j]].Model);
                var worldDistance = Distance(bound[keys[i]].World, bound[keys[j]].World);
                Assert.True(
                    MathF.Abs(worldDistance - (scale * modelDistance)) < 1e-4f,
                    $"position mismatch {keys[i]}..{keys[j]}: world {worldDistance} vs model {scale * modelDistance}");
            }
        }
    }

    private static float Distance(System.Numerics.Vector3 left, System.Numerics.Vector3 right) => (left - right).Length();

    private static IReadOnlyList<DecalRow> ReadRows()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "glint-special-decal.reference.txt");
        Assert.True(File.Exists(path), $"missing reference sample: {path}");
        var rows = new List<DecalRow>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var columns = line.Split('\t');
            Assert.Equal(12, columns.Length);
            rows.Add(new DecalRow(
                columns[0],
                Enum.Parse<ModelFaceDirection>(columns[1], ignoreCase: true),
                int.Parse(columns[2], CultureInfo.InvariantCulture),
                Float(columns[3]),
                Float(columns[4]),
                Float(columns[5]),
                Float(columns[6]),
                Float(columns[7]),
                Float(columns[8]),
                Float(columns[9]),
                Float(columns[10]),
                Float(columns[11])));
        }

        return rows;
    }

    private static float Float(string value) => float.Parse(value, CultureInfo.InvariantCulture);

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

    private sealed record DecalRow(
        string Surface,
        ModelFaceDirection Direction,
        int Corner,
        float X,
        float Y,
        float Z,
        float PoseScale,
        float DecalScale,
        float Uv3U,
        float Uv3V,
        float GlintU,
        float GlintV);
}
