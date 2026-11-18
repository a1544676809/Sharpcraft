// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-222 chest raw geometry: source bind-pose samples, single 72 / double half 60 vertices.</summary>
public sealed class ChestRawReferenceSampleTests
{
    [Theory]
    [InlineData("chest-single-bone-raw-vectors.reference.txt", "chest_single")]
    [InlineData("chest-double-left-bone-raw-vectors.reference.txt", "chest_double_left")]
    [InlineData("chest-double-right-bone-raw-vectors.reference.txt", "chest_double_right")]
    public void RawGeometryMatchesSourceBindPose(string sampleFile, string definitionName)
    {
        var definition = definitionName switch
        {
            "chest_single" => ChestModelDefinition.Single,
            "chest_double_left" => ChestModelDefinition.DoubleLeft,
            _ => ChestModelDefinition.DoubleRight
        };
        var rows = Load(sampleFile);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/chest/normal"] = new(64, 64, new byte[64 * 64 * 4])
        });
        var sprite = atlas.Get("entity/chest/normal")!;
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            sprite,
            64,
            64,
            modelScale: 1f);
        var vertices = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        Assert.Equal(rows.Count, vertices);
        for (var i = 0; i < rows.Count; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, i);
            Assert.True(MathF.Abs(x - rows[i].X) <= 1e-5f, $"{sampleFile} vertex {i} x: {x} vs {rows[i].X}");
            Assert.True(MathF.Abs(y - rows[i].Y) <= 1e-5f, $"{sampleFile} vertex {i} y: {y} vs {rows[i].Y}");
            Assert.True(MathF.Abs(z - rows[i].Z) <= 1e-5f, $"{sampleFile} vertex {i} z: {z} vs {rows[i].Z}");
            Assert.True(MathF.Abs(u - rows[i].U) <= 1e-5f, $"{sampleFile} vertex {i} u: {u} vs {rows[i].U}");
            Assert.True(MathF.Abs(v - rows[i].V) <= 1e-5f, $"{sampleFile} vertex {i} v: {v} vs {rows[i].V}");
        }
    }

    [Fact]
    public void DoubleHalvesDropTheJoinFace()
    {
        Assert.All(ChestModelDefinition.DoubleLeft.Parts,
            part => Assert.NotEqual(EntityModelVisibleSides.All, part.VisibleSides));
        Assert.All(ChestModelDefinition.DoubleLeft.Parts,
            part => Assert.Equal(0, (int)(part.VisibleSides & EntityModelVisibleSides.West)));
        Assert.All(ChestModelDefinition.DoubleRight.Parts,
            part => Assert.Equal(0, (int)(part.VisibleSides & EntityModelVisibleSides.East)));
    }

    private static List<Row> Load(string sampleFile)
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

            var fields = line.Split('\t');
            rows.Add(new Row(
                fields[1],
                float.Parse(fields[5], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[6], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[7], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[8], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(fields[9], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private readonly record struct Row(string Part, float X, float Y, float Z, float U, float V);
}
