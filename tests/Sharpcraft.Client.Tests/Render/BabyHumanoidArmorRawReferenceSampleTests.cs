// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-241: baby humanoid armor raw bind pose, keyed per slot/family, 1e-5 + UV half.</summary>
public sealed class BabyHumanoidArmorRawReferenceSampleTests
{
    [Theory]
    [InlineData("zombie-baby-armor-helmet-raw-vectors.reference.txt", "helmet")]
    [InlineData("zombie-baby-armor-chestplate-raw-vectors.reference.txt", "chestplate")]
    [InlineData("zombie-baby-armor-leggings-raw-vectors.reference.txt", "leggings")]
    [InlineData("zombie-baby-armor-boots-raw-vectors.reference.txt", "boots")]
    [InlineData("zombie-villager-baby-armor-helmet-raw-vectors.reference.txt", "helmet")]
    [InlineData("zombie-villager-baby-armor-chestplate-raw-vectors.reference.txt", "chestplate")]
    [InlineData("zombie-villager-baby-armor-leggings-raw-vectors.reference.txt", "leggings")]
    [InlineData("zombie-villager-baby-armor-boots-raw-vectors.reference.txt", "boots")]
    public void BabyHumanoidArmorRawGeometryMatchesSource(string sampleFile, string slot)
    {
        var rows = Load(sampleFile);
        var definition = BabyHumanoidArmorModelDefinition.ForSlot(slot switch
        {
            "helmet" => 5,
            "chestplate" => 4,
            "leggings" => 3,
            _ => 2
        });
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid_baby/iron"] = new(64, 64, new byte[64 * 64 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/humanoid_baby/iron")!,
            64,
            64,
            modelScale: 1f);
        Assert.Equal(rows.Length, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < rows.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            var row = rows[i];
            Assert.True(MathF.Abs(x - row.X) <= 1e-5f, $"{sampleFile} {row.Part} v{i} x {x} vs {row.X}");
            Assert.True(MathF.Abs(y - row.Y) <= 1e-5f, $"{sampleFile} {row.Part} v{i} y {y} vs {row.Y}");
            Assert.True(MathF.Abs(z - row.Z) <= 1e-5f, $"{sampleFile} {row.Part} v{i} z {z} vs {row.Z}");
            var expectedU = QuantizeHalf(row.U);
            var expectedV = QuantizeHalf(row.V);
            Assert.True(MathF.Abs(u - expectedU) <= 1e-5f, $"{sampleFile} {row.Part} v{i} u {u} vs {expectedU}");
            Assert.True(MathF.Abs(v - expectedV) <= 1e-5f, $"{sampleFile} {row.Part} v{i} v {v} vs {expectedV}");
        }
    }

    [Fact]
    public void ProductionBabyHumanoidArmorUsesRawLayersAndLeavesBabyPiglinLegacy()
    {
        foreach (var typeId in new[]
                 {
                     Sharpcraft.Client.Render.HumanoidMobModelDefinition.ZombieEntityTypeId,
                     Sharpcraft.Client.Render.ZombieVillagerModelDefinition.ZombieVillagerEntityTypeId
                 })
        {
            var model = EntityModelResolver.Resolve(typeId, baby: true);
            var expected = new (string Name, int Vertices)[]
            {
                ("equipment_HumanoidArmorBaby_5", 24),
                ("equipment_HumanoidArmorBaby_4", 72),
                ("equipment_HumanoidArmorBaby_3", 72),
                ("equipment_HumanoidArmorBaby_2", 48)
            };
            foreach (var (name, vertices) in expected)
            {
                var layer = model.RenderLayers!.Single(layer => layer.Name == name).Model!;
                Assert.True(layer.RawSourceFrame, name);
                Assert.Equal(0f, layer.BaselinePx);
                Assert.Equal(0f, layer.SourceFrameBaselinePx);
                Assert.True(layer.FaceTransforms is null || layer.FaceTransforms.Count == 0);
                Assert.Equal(vertices, layer.Compiled!.Cubes.Sum(cube => 24));
            }
        }

        var piglin = EntityModelResolver.Resolve(Sharpcraft.Client.Render.PiglinModelDefinition.PiglinEntityTypeId, baby: true);
        var piglinArmor = piglin.RenderLayers!.First(layer => layer.Name == "equipment_HumanoidArmorBaby_5").Model!;
        Assert.False(piglinArmor.RawSourceFrame);
        Assert.Equal(24f, piglinArmor.BaselinePx);
        Assert.NotNull(piglinArmor.FaceTransforms);
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
