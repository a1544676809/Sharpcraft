// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-249: Creeper power overlay and Wolf armor are full-creature raw layers, not humanoid slots.</summary>
public sealed class CreatureArmorRawReferenceSampleTests
{
    [Theory]
    [InlineData("creeper-armor-raw-vectors.reference.txt", 144)]
    [InlineData("wolf-armor-raw-vectors.reference.txt", 264)]
    public void CreatureArmorRawGeometryMatchesSource(string sampleFile, int vertexCount)
    {
        var rows = Load(sampleFile);
        Assert.Equal(vertexCount, rows.Length);
        var definition = sampleFile.StartsWith("creeper", StringComparison.Ordinal)
            ? CreeperArmorModelDefinition.Definition
            : WolfArmorModelDefinition.Definition;
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/humanoid/iron")!, 64, 32, modelScale: 1f);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < rows.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            var row = rows[i];
            var expectedU = QuantizeHalf(row.U);
            var expectedV = QuantizeHalf(row.V);
            Assert.True(MathF.Abs(x - row.X) <= 1e-5f, $"{sampleFile} {row.Part} v{i} x {x} vs {row.X}");
            Assert.True(MathF.Abs(y - row.Y) <= 1e-5f, $"{sampleFile} {row.Part} v{i} y {y} vs {row.Y}");
            Assert.True(MathF.Abs(z - row.Z) <= 1e-5f, $"{sampleFile} {row.Part} v{i} z {z} vs {row.Z}");
            Assert.True(MathF.Abs(u - expectedU) <= 1e-5f, $"{sampleFile} {row.Part} v{i} u {u} vs {expectedU}");
            Assert.True(MathF.Abs(v - expectedV) <= 1e-5f, $"{sampleFile} {row.Part} v{i} v {v} vs {expectedV}");
        }
    }

    [Fact]
    public void ProductionWolfArmorUsesRawBodyLayer()
    {
        var model = EntityModelResolver.Resolve(WolfModelDefinition.WolfEntityTypeId);
        var layer = model.RenderLayers!.Single(l => l.Name == "equipment_WolfBody_6").Model!;
        Assert.True(layer.RawSourceFrame);
        Assert.Equal(0f, layer.BaselinePx);
        Assert.Equal(264, layer.Compiled!.Cubes.Sum(cube => 24));
    }

    [Theory]
    [InlineData("zombie-villager-armor-helmet-raw-vectors.reference.txt", 5, 48)]
    [InlineData("zombie-villager-armor-chestplate-raw-vectors.reference.txt", 4, 72)]
    [InlineData("zombie-villager-armor-leggings-raw-vectors.reference.txt", 3, 72)]
    [InlineData("zombie-villager-armor-boots-raw-vectors.reference.txt", 2, 48)]
    public void ZombieVillagerArmorRawGeometryMatchesSource(string sampleFile, int slot, int vertexCount)
    {
        var rows = Load(sampleFile);
        Assert.Equal(vertexCount, rows.Length);
        var definition = ZombieVillagerArmorModelDefinition.ForSlot(slot);
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);
        Assert.Equal(0f, definition.SourceFrameBaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/humanoid/iron")!, 64, 32, modelScale: 1f);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < rows.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            var row = rows[i];
            Assert.True(MathF.Abs(x - row.X) <= 1e-5f, $"{sampleFile} {row.Part} v{i} x {x} vs {row.X}");
            Assert.True(MathF.Abs(y - row.Y) <= 1e-5f, $"{sampleFile} {row.Part} v{i} y {y} vs {row.Y}");
            Assert.True(MathF.Abs(z - row.Z) <= 1e-5f, $"{sampleFile} {row.Part} v{i} z {z} vs {row.Z}");
            Assert.True(MathF.Abs(u - QuantizeHalf(row.U)) <= 1e-5f, $"{sampleFile} {row.Part} v{i} u");
            Assert.True(MathF.Abs(v - QuantizeHalf(row.V)) <= 1e-5f, $"{sampleFile} {row.Part} v{i} v");
        }
    }

    [Theory]
    [InlineData(5, "equipment_HumanoidArmor_5", "zombie-villager-armor-helmet-raw-vectors.reference.txt", "minecraft:iron_helmet")]
    [InlineData(4, "equipment_HumanoidArmor_4", "zombie-villager-armor-chestplate-raw-vectors.reference.txt", "minecraft:iron_chestplate")]
    [InlineData(3, "equipment_HumanoidLeggings_3", "zombie-villager-armor-leggings-raw-vectors.reference.txt", "minecraft:iron_leggings")]
    [InlineData(2, "equipment_HumanoidArmor_2", "zombie-villager-armor-boots-raw-vectors.reference.txt", "minecraft:iron_boots")]
    public void ProductionZombieVillagerArmorLayerMatchesSourcePerKey(int slot, string layerName, string sampleFile, string itemName)
    {
        var model = EntityModelResolver.Resolve(ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId);
        var layer = model.RenderLayers!.Single(entry => entry.Name == layerName);
        var layerModel = layer.Model!;
        Assert.True(layerModel.RawSourceFrame, layerName);
        Assert.Equal(0f, layerModel.BaselinePx);
        Assert.Equal(0f, layerModel.SourceFrameBaselinePx);

        var itemId = EntityModelResolver.Items!.GetId(Sharpcraft.Common.Identifiers.Identifier.Parse(itemName));
        Assert.True(itemId >= 0, itemName);
        var equipment = new EntityEquipment(-1, -1, -1, -1, -1, -1, -1, -1);
        equipment = slot switch
        {
            2 => equipment with { Feet = itemId },
            3 => equipment with { Legs = itemId },
            4 => equipment with { Chest = itemId },
            5 => equipment with { Head = itemId },
            _ => throw new Xunit.Sdk.XunitException($"slot {slot}")
        };
        var state = new EntityRenderState(1, ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId, 0f, 0f, 0f, 180f, 0f,
            Equipment: equipment);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var rows = Load(sampleFile);
        var nodes = Flatten(layerModel.Compiled!.Root).GroupBy(node => node.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var consumed = 0;
        foreach (var group in rows.GroupBy(row => row.Part))
        {
            var nodeName = group.Key.Split('/').Last();
            var cubeName = nodes[nodeName].Cubes[0].Name;
            var ordered = group.OrderBy(row => row.Cube).ThenBy(row => row.Polygon).ThenBy(row => row.Vertex).ToArray();
            var geometry = BuildProductionCube(state, layerName, layerModel.TextureWidth, layerModel.TextureHeight, cubeName, atlas);
            Assert.Equal(ordered.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
            for (var i = 0; i < ordered.Length; i++)
            {
                var row = ordered[i];
                var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
                var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
                var actual = new System.Numerics.Vector3(-x / EntityPlaceholderMesher.ModelScale,
                    -y / EntityPlaceholderMesher.ModelScale, z / EntityPlaceholderMesher.ModelScale);
                Assert.True((actual - new System.Numerics.Vector3(row.X, row.Y, row.Z)).Length() <= 1e-5f,
                    $"{sampleFile} {group.Key} v{i} position");
                Assert.True(MathF.Abs(u - QuantizeHalf(row.U)) <= 1e-5f, $"{sampleFile} {group.Key} v{i} u");
                Assert.True(MathF.Abs(v - QuantizeHalf(row.V)) <= 1e-5f, $"{sampleFile} {group.Key} v{i} v");
            }

            consumed += ordered.Length;
        }

        Assert.Equal(rows.Length, consumed);
    }

    [Fact]
    public void ProductionZombieVillagerArmorUsesRawLayers()
    {
        var model = EntityModelResolver.Resolve(ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId);
        foreach (var (name, vertices) in new (string Name, int Vertices)[]
                 {
                     ("equipment_HumanoidArmor_5", 48),
                     ("equipment_HumanoidArmor_4", 72),
                     ("equipment_HumanoidLeggings_3", 72),
                     ("equipment_HumanoidArmor_2", 48)
                 })
        {
            var layer = model.RenderLayers!.Single(entry => entry.Name == name).Model!;
            Assert.True(layer.RawSourceFrame, name);
            Assert.Equal(0f, layer.BaselinePx);
            Assert.Equal(0f, layer.SourceFrameBaselinePx);
            Assert.Equal(vertices, layer.Compiled!.Cubes.Sum(cube => 24));
        }
    }

    private static DynamicGeometry BuildProductionCube(EntityRenderState state, string layerName, int width, int height,
        string cubeName, ItemAtlas atlas)
    {
        var layers = new EntityModelMesher().BuildLayers([state], new SpriteRect(0, 0, width, height, 1), width, height,
            onlyPart: cubeName, skinAtlas: atlas, variants: EntityVariantTable.Builtin, pose: new EntityPose());
        return layers.Single(layer => layer.Name == layerName).Geometry;
    }

    private static IEnumerable<EntityModelPartNode> Flatten(EntityModelPartNode node)
    {
        yield return node;
        foreach (var child in node.ChildArray)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }
    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));

    private static Row[] Load(string sampleFile)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", sampleFile);
        Assert.True(File.Exists(path), $"missing reference data: {path}");
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var f = line.Split('\t');
            rows.Add(new Row(f[1], f[2], f[3], f[4],
                float.Parse(f[5], CultureInfo.InvariantCulture),
                float.Parse(f[6], CultureInfo.InvariantCulture),
                float.Parse(f[7], CultureInfo.InvariantCulture),
                float.Parse(f[8], CultureInfo.InvariantCulture),
                float.Parse(f[9], CultureInfo.InvariantCulture)));
        }

        return [.. rows];
    }

    private readonly record struct Row(string Part, string Cube, string Polygon, string Vertex,
        float X, float Y, float Z, float U, float V);
}
