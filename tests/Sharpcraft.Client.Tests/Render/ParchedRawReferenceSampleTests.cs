// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-257: Parched body/outer layers and reused humanoid armor samples.</summary>
public sealed class ParchedRawReferenceSampleTests
{
    [Theory]
    [InlineData("parched-raw-vectors.reference.txt", "body", 312, 64, 64)]
    [InlineData("parched-outer-layer-raw-vectors.reference.txt", "outer", 168, 64, 32)]
    [InlineData("parched-armor-helmet-raw-vectors.reference.txt", "armor", 48, 64, 32, 5)]
    [InlineData("parched-armor-chestplate-raw-vectors.reference.txt", "armor", 72, 64, 32, 4)]
    [InlineData("parched-armor-leggings-raw-vectors.reference.txt", "armor", 72, 64, 32, 3)]
    [InlineData("parched-armor-boots-raw-vectors.reference.txt", "armor", 48, 64, 32, 2)]
    public void ParchedRawGeometryMatchesSource(
        string sampleFile, string mode, int vertexCount, int textureWidth, int textureHeight, int slot = 0)
    {
        var rows = Load(sampleFile);
        Assert.Equal(vertexCount, rows.Length);
        var definition = mode switch
        {
            "body" => ParchedModelDefinition.Body,
            "outer" => ParchedModelDefinition.Outer,
            _ => PlayerArmorModelDefinition.ForSlot(slot)
        };
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/humanoid/iron"] = new(textureWidth, textureHeight, new byte[textureWidth * textureHeight * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, ParchedModelDefinition.ParchedEntityTypeId, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/humanoid/iron")!, textureWidth, textureHeight, modelScale: 1f);
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
    public void ProductionParchedUsesRawBodyAndReusedHumanoidArmor()
    {
        var parched = EntityModelResolver.Resolve(ParchedModelDefinition.ParchedEntityTypeId);
        Assert.Equal("parched", parched.Name);
        Assert.Equal(312, ParchedModelDefinition.Body.Compiled!.Cubes.Sum(cube => 24));
        Assert.NotNull(EntityAnimationRegistry.For(ParchedModelDefinition.ParchedEntityTypeId));

        foreach (var (name, vertices) in new (string Name, int Vertices)[]
                 {
                     ("equipment_HumanoidArmor_5", 48),
                     ("equipment_HumanoidArmor_4", 72),
                     ("equipment_HumanoidLeggings_3", 72),
                     ("equipment_HumanoidArmor_2", 48)
                 })
        {
            var layer = parched.RenderLayers!.Single(layer => layer.Name == name).Model!;
            Assert.True(layer.RawSourceFrame, name);
            Assert.Equal(0f, layer.BaselinePx);
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
