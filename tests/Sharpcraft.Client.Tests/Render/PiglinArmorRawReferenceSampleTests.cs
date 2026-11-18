// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-243: Piglin / baby Piglin armor raw bind pose, keyed per slot/family, 1e-5 + UV half.</summary>
public sealed class PiglinArmorRawReferenceSampleTests
{
    [Theory]
    [InlineData("piglin-armor-helmet-raw-vectors.reference.txt", "helmet", false)]
    [InlineData("piglin-armor-chestplate-raw-vectors.reference.txt", "chestplate", false)]
    [InlineData("piglin-armor-leggings-raw-vectors.reference.txt", "leggings", false)]
    [InlineData("piglin-armor-boots-raw-vectors.reference.txt", "boots", false)]
    [InlineData("zombified-piglin-armor-helmet-raw-vectors.reference.txt", "helmet", false)]
    [InlineData("zombified-piglin-armor-chestplate-raw-vectors.reference.txt", "chestplate", false)]
    [InlineData("zombified-piglin-armor-leggings-raw-vectors.reference.txt", "leggings", false)]
    [InlineData("zombified-piglin-armor-boots-raw-vectors.reference.txt", "boots", false)]
    [InlineData("piglin-baby-armor-helmet-raw-vectors.reference.txt", "helmet", true)]
    [InlineData("piglin-baby-armor-chestplate-raw-vectors.reference.txt", "chestplate", true)]
    [InlineData("piglin-baby-armor-leggings-raw-vectors.reference.txt", "leggings", true)]
    [InlineData("piglin-baby-armor-boots-raw-vectors.reference.txt", "boots", true)]
    [InlineData("zombified-piglin-baby-armor-helmet-raw-vectors.reference.txt", "helmet", true)]
    [InlineData("zombified-piglin-baby-armor-chestplate-raw-vectors.reference.txt", "chestplate", true)]
    [InlineData("zombified-piglin-baby-armor-leggings-raw-vectors.reference.txt", "leggings", true)]
    [InlineData("zombified-piglin-baby-armor-boots-raw-vectors.reference.txt", "boots", true)]
    public void PiglinArmorRawGeometryMatchesSource(string sampleFile, string slot, bool baby)
    {
        var rows = Load(sampleFile);
        var definition = PiglinArmorModelDefinition.ForSlot(slot switch
        {
            "helmet" => 5,
            "chestplate" => 4,
            "leggings" => 3,
            _ => 2
        }, baby);
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var texturePath = baby ? "entity/equipment/piglin_baby/iron" : "entity/equipment/piglin/iron";
        var textureSize = baby ? 64 : 32;
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath] = new(64, textureSize, new byte[64 * textureSize * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get(texturePath)!,
            64,
            textureSize,
            modelScale: 1f);
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

    [Theory]
    [InlineData(Sharpcraft.Client.Render.PiglinModelDefinition.PiglinEntityTypeId, false)]
    [InlineData(Sharpcraft.Client.Render.PiglinModelDefinition.PiglinBruteEntityTypeId, false)]
    [InlineData(Sharpcraft.Client.Render.PiglinModelDefinition.ZombifiedPiglinEntityTypeId, false)]
    [InlineData(Sharpcraft.Client.Render.PiglinModelDefinition.PiglinEntityTypeId, true)]
    [InlineData(Sharpcraft.Client.Render.PiglinModelDefinition.ZombifiedPiglinEntityTypeId, true)]
    public void ProductionPiglinArmorUsesRawLayers(int typeId, bool baby)
    {
        var model = EntityModelResolver.Resolve(typeId, baby);
        var expected = baby
            ? new (string Name, int Vertices)[]
            {
                ("equipment_HumanoidArmorBaby_5", 24),
                ("equipment_HumanoidArmorBaby_4", 72),
                ("equipment_HumanoidArmorBaby_3", 72),
                ("equipment_HumanoidArmorBaby_2", 48)
            }
            : new (string Name, int Vertices)[]
            {
                ("equipment_HumanoidArmor_5", 48),
                ("equipment_HumanoidArmor_4", 72),
                ("equipment_HumanoidLeggings_3", 72),
                ("equipment_HumanoidArmor_2", 48)
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
