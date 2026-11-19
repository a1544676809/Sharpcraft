// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// B-4 T2 (#228): trident/shield special renderers now go through the same raw hand chain as regular held items.
/// Production <see cref="ItemInHandMesher"/> output is compared against the jar held-world samples per
/// layer|part|cube|polygon|vertex, position at 1e-5 and UV half at 1e-5, for RIGHT / LEFT / RIGHT_USE and with
/// non-foil / foil glint.
/// </summary>
public sealed class HeldSpecialWorldReferenceSampleTests
{
    private const float Tolerance = 1e-5f;
    private const int FixedMillis = 55000;

    public static TheoryData<string, string, bool, bool, string> Cases => new()
    {
        { "trident-held-world.reference.txt", "TRIDENT_RIGHT", false, false, "held_special_trident_right" },
        { "trident-held-world.reference.txt", "TRIDENT_RIGHT_USE", false, true, "held_special_trident_right" },
        { "trident-held-world.reference.txt", "TRIDENT_LEFT", true, false, "held_special_trident_left" },
        { "shield-held-world.reference.txt", "SHIELD_RIGHT", false, false, "held_special_shield_right" },
        { "shield-held-world.reference.txt", "SHIELD_RIGHT_USE", false, true, "held_special_shield_right" },
        { "shield-held-world.reference.txt", "SHIELD_LEFT", true, false, "held_special_shield_left" }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ProductionHeldSpecialMatchesTheWorldSamplePerKey(string sampleFile, string sampleLayer, bool offhand,
        bool use, string layerName)
    {
        var isTrident = sampleFile.StartsWith("trident", StringComparison.Ordinal);
        var model = isTrident ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = isTrident ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        using var fixture = new ItemFixture();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4]),
            [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4])
        });
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse(isTrident ? "minecraft:trident" : "minecraft:shield"));
        Assert.True(itemId > 0);
        var state = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsUsingItem: use,
            MainHandItemId: offhand ? -1 : itemId,
            OffHandItemId: offhand ? itemId : -1);
        var layers = new ItemInHandMesher().Build([state], [new EntityPose()], variants: null, atlas);
        var layer = layers.Single(entry => entry.Name == layerName);

        var rows = Load(sampleFile).Where(row => row.Layer == sampleLayer).ToArray();
        Assert.NotEmpty(rows);
        var cubes = model.Compiled!.Cubes.Where(cube => cube.Width > 0f || cube.Height > 0f || cube.Depth > 0f)
            .Select(cube => cube.Name).ToArray();
        Assert.Equal(rows.Length, 24 * cubes.Length);
        var consumed = 0;
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var cubeName = group.Key.Part.Split('/').Last();
            var cubeIndex = Array.IndexOf(cubes, cubeName);
            Assert.True(cubeIndex >= 0, $"{layerName}: production cube '{cubeName}' missing");
            var ordered = group.OrderBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            Assert.Equal(24, ordered.Length);
            for (var i = 0; i < ordered.Length; i++)
            {
                var row = ordered[i];
                var offset = (cubeIndex * 24) + (row.Polygon * 4) + row.Vertex;
                Assert.InRange(offset, 0, (layer.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex) - 1);
                var produced = ReadVertex(layer.Geometry, offset);
                var actual = produced.Position;
                // Production held layers work in the source hand frame without the entity living flip, so the
                // world sample is mapped back through S(-1,-1,1) and T(0,-1.501,0) and scaled by the model scale.
                var expected = EntityPlaceholderMesher.ModelScale
                    * new Vector3(-row.Position.X, 1.501f - row.Position.Y, row.Position.Z);
                Assert.True((actual - expected).Length() <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: position {actual} vs {expected}");
                Assert.True(MathF.Abs(produced.U - QuantizeHalf(row.U)) <= Tolerance
                        && MathF.Abs(produced.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: UV");
            }

            consumed += ordered.Length;
        }

        Assert.Equal(rows.Length, consumed);
    }

    public static TheoryData<string, string, string, string, string, bool> PosedCases => new()
    {
        { "trident-held-world-stab.reference.txt", "TRIDENT_RIGHT_STAB", "trident", "stab", "held_special_trident_right", false },
        { "trident-held-world-throw-use.reference.txt", "TRIDENT_RIGHT_USE_REAL", "trident", "throw", "held_special_trident_right", true },
        { "shield-held-world-use.reference.txt", "SHIELD_RIGHT_USE", "shield", "use", "held_special_shield_right", true },
        { "shield-held-world-block.reference.txt", "SHIELD_RIGHT_BLOCK", "shield", "block", "held_special_shield_right", false }
    };

    [Theory]
    [MemberData(nameof(PosedCases))]
    public void ProductionHeldSpecialMatchesRealPoseSamplesPerKey(string sampleFile, string sampleLayer, string itemName,
        string poseName, string layerName, bool usingItem)
    {
        try
        {
            RunPosed(sampleFile, sampleLayer, itemName, poseName, layerName, usingItem);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(ex.ToString());
        }
    }

    private static void RunPosed(string sampleFile, string sampleLayer, string itemName,
        string poseName, string layerName, bool usingItem)
    {
        var isTrident = itemName == "trident";
        var model = isTrident ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = isTrident ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        using var fixture = new ItemFixture();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4])
        });
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        var stab = poseName == "stab";
        var frame = default(EntityAnimationFrame) with
        {
            TypeId = EntityPlaceholderMesher.PlayerEntityTypeId,
            SwingAnimation = stab ? 0.15f : 0f,
            SwingType = stab ? 2 : 0,
            SwingHand = 0,
            MainHandItemId = itemId,
            IsUsingItem = usingItem,
            TicksUsingItem = usingItem ? 10f : 0f,
            UseItemHand = 0
        };
        var rightPose = poseName switch
        {
            "stab" => HumanoidArmPose.Spear,
            "throw" => HumanoidArmPose.ThrowTrident,
            "use" or "block" => HumanoidArmPose.Block,
            _ => HumanoidArmPose.Empty
        };
        var pose = new EntityPose();
        try
        {
            HumanoidModelPose.Apply(frame, pose, HumanoidVariant.Plain, rightPose, HumanoidArmPose.Empty,
                isHoldingBow: false, rawSourceFrame: true);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(ex.ToString());
        }

        var state = new EntityRenderState(3, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            IsUsingItem: usingItem,
            TicksUsingItem: usingItem ? 10f : 0f,
            MainHandItemId: itemId,
            SwingAnimation: stab ? 0.15f : 0f,
            SwingType: stab ? 2 : 0,
            SwingHand: 0,
            UseItemHand: 0);
        IReadOnlyList<EntityGeometryLayer> layers;
        try
        {
            layers = new ItemInHandMesher().Build([state], [pose], variants: null, atlas);
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(ex.ToString());
        }
        var layer = layers.Single(entry => entry.Name == layerName);
        var rows = Load(sampleFile).Where(row => row.Layer == sampleLayer).ToArray();
        Assert.NotEmpty(rows);
        var cubes = model.Compiled!.Cubes.Select(cube => cube.Name).ToArray();
        Assert.Equal(rows.Length, 24 * cubes.Length);
        Assert.True(layer.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex == rows.Length,
            $"{sampleLayer}: production vertices {layer.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex} vs {rows.Length}");
        foreach (var group in rows.GroupBy(row => (row.Part, row.Cube)))
        {
            var cubeName = group.Key.Part.Split('/').Last();
            var cubeIndex = Array.IndexOf(cubes, cubeName);
            Assert.True(cubeIndex >= 0, $"{sampleLayer}: production cube '{cubeName}' missing");
            var ordered = group.OrderBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                var row = ordered[i];
                var offset = (cubeIndex * 24) + (row.Polygon * 4) + row.Vertex;
                Assert.InRange(offset, 0, (layer.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex) - 1);
                var produced = ReadVertex(layer.Geometry, offset);
                var expected = EntityPlaceholderMesher.ModelScale
                    * new Vector3(-row.Position.X, 1.501f - row.Position.Y, row.Position.Z);
                Assert.True((produced.Position - expected).Length() <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: position {produced.Position} vs {expected}");
                Assert.True(MathF.Abs(produced.U - QuantizeHalf(row.U)) <= Tolerance
                        && MathF.Abs(produced.V - QuantizeHalf(row.V)) <= Tolerance,
                    $"{sampleLayer}/{row.Part}#{row.Cube} {row.Polygon}/{row.Vertex}: UV");
            }
        }
    }
    [Fact]
    public void HeadSpecialLayersAreRegistered()
    {
        var layers = EntityModelResolver.Resolve(EntityPlaceholderMesher.PlayerEntityTypeId).RenderLayers!;
        foreach (var name in new[] { "held_special_head_right", "held_special_head_left" })
        {
            var layer = layers.Single(entry => entry.Name == name);
            Assert.NotNull(layer.Model);
        }
    }
    [Theory]
    [InlineData("trident", "TRIDENT_RIGHT", "held_special_trident_right")]
    [InlineData("shield", "SHIELD_RIGHT", "held_special_shield_right")]
    public void FoilAddsEntityScaleGlintOnTopOfTheHeldSpecial(string itemName, string sampleLayer, string layerName)
    {
        var isTrident = itemName == "trident";
        var model = isTrident ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = isTrident ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        using var fixture = new ItemFixture();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texture] = new(model.TextureWidth, model.TextureHeight, new byte[model.TextureWidth * model.TextureHeight * 4]),
            [ItemInHandMesher.GlintTexture] = new(64, 64, new byte[64 * 64 * 4])
        });
        var itemId = EntityModelResolver.Items!.GetId(Identifier.Parse("minecraft:" + itemName));
        GlintClock.SetFixedSourceMillis(FixedMillis);
        try
        {
            var plain = new EntityRenderState(2, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
                MainHandItemId: itemId);
            var plainLayers = new ItemInHandMesher().Build([plain], [new EntityPose()], variants: null, atlas);
            Assert.Single(plainLayers, layer => layer.Name == layerName);
            Assert.DoesNotContain(plainLayers, layer => layer.Name.EndsWith("_glint", StringComparison.Ordinal));

            var foil = plain with { MainHandFoil = ItemFoilType.Standard };
            var foilLayers = new ItemInHandMesher().Build([foil], [new EntityPose()], variants: null, atlas);
            var @base = foilLayers.Single(layer => layer.Name == layerName);
            var glint = foilLayers.Single(layer => layer.Name == layerName + "_glint");
            Assert.Equal(@base.Geometry.VertexFloats, glint.Geometry.VertexFloats);
            Assert.Equal(BasePositions(@base.Geometry), BasePositions(glint.Geometry));
            for (var i = 0; i < @base.Geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
            {
                var baseUv = ReadVertex(@base.Geometry, i);
                var glintUv = ReadVertex(glint.Geometry, i);
                var expected = GlintTextureTransform.Apply(baseUv.U, baseUv.V, FixedMillis, GlintTextureTransform.EntityScale);
                Assert.Equal((float)(Half)expected.U, glintUv.U, 3);
                Assert.Equal((float)(Half)expected.V, glintUv.V, 3);
            }

            // The world sample layer still has full geometry coverage for this hand.
            Assert.Contains(Load(isTrident ? "trident-held-world.reference.txt" : "shield-held-world.reference.txt"),
                row => row.Layer == sampleLayer);
        }
        finally
        {
            GlintClock.SetProvider(null);
        }
    }

    private static List<Vector3> BasePositions(DynamicGeometry geometry)
    {
        var positions = new List<Vector3>(geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
        {
            positions.Add(ReadVertex(geometry, i).Position);
        }

        return positions;
    }

    private static (Vector3 Position, float U, float V) ReadVertex(DynamicGeometry geometry, int vertex)
    {
        var offset = vertex * ChunkMesher.FloatsPerVertex;
        var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[offset + 4]);
        return (
            new Vector3(geometry.Vertices[offset], geometry.Vertices[offset + 1], geometry.Vertices[offset + 2]),
            (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFF)),
            (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16)));
    }

    private static float QuantizeHalf(float value) => (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)value));

    private static List<Row> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", fileName);
        var rows = new List<Row>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split('\t');
            rows.Add(new Row(fields[0], fields[1], int.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[3], CultureInfo.InvariantCulture), int.Parse(fields[4], CultureInfo.InvariantCulture),
                new Vector3(float.Parse(fields[5], CultureInfo.InvariantCulture), float.Parse(fields[6], CultureInfo.InvariantCulture),
                    float.Parse(fields[7], CultureInfo.InvariantCulture)),
                float.Parse(fields[8], CultureInfo.InvariantCulture), float.Parse(fields[9], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private readonly record struct Row(string Layer, string Part, int Cube, int Polygon, int Vertex, Vector3 Position, float U, float V);

    /// <summary>Temp resource stack with the trident/shield base item models and special wrappers.</summary>
    private sealed class ItemFixture : IDisposable
    {
        private readonly string _root;
        private readonly ItemModelResolver _previous;
        private readonly ResourceManager _resources;

        public ItemFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "sharpcraft-held-special-" + Guid.NewGuid().ToString("N"));
            Write("assets/minecraft/items/trident.json",
                """{"model":{"type":"minecraft:condition","property":"minecraft:using_item","on_false":{"type":"minecraft:special","base":"minecraft:item/trident_in_hand","model":{"type":"minecraft:trident"}},"on_true":{"type":"minecraft:special","base":"minecraft:item/trident_throwing","model":{"type":"minecraft:trident"}}}}""");
            Write("assets/minecraft/items/shield.json",
                """{"model":{"type":"minecraft:condition","property":"minecraft:using_item","on_false":{"type":"minecraft:special","base":"minecraft:item/shield","model":{"type":"minecraft:shield"}},"on_true":{"type":"minecraft:special","base":"minecraft:item/shield_blocking","model":{"type":"minecraft:shield"}}}}""");
            WriteModel("trident_in_hand", "[0,60,0]", "[11,17,-2]", "[0,60,0]", "[11,17,-2]");
            WriteModel("trident_throwing", "[0,90,180]", "[8,-17,9]", "[0,90,180]", "[8,-17,9]");
            WriteModel("shield", "[0,90,0]", "[10,6,-4]", "[0,90,0]", "[10,6,-4]");
            WriteModel("shield_blocking", "[45,155,0]", "[-3.49,11,-2]", "[45,155,0]", "[-3.49,11,-2]");
            var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(_root, "held-special-test"));
            _resources = new ResourceManager(stack, 1);
            _previous = EntityModelResolver.ItemModels!;
            EntityModelResolver.ItemModels = new ItemModelResolver(_resources);
        }

        public void Dispose()
        {
            EntityModelResolver.ItemModels = _previous;
            _resources.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private void WriteModel(string name, string rightRotation, string rightTranslation, string leftRotation, string leftTranslation) =>
            Write($"assets/minecraft/models/item/{name}.json",
                "{\"display\":{\"thirdperson_righthand\":{\"rotation\":" + rightRotation
                + ",\"translation\":" + rightTranslation
                + ",\"scale\":[1,1,1]},\"thirdperson_lefthand\":{\"rotation\":" + leftRotation
                + ",\"translation\":" + leftTranslation
                + ",\"scale\":[1,1,1]}}}");

        private void Write(string relative, string json)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
