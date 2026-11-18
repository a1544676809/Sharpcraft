// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>task-226: shulker box raw bind pose, source 48 rows, 1e-5 + UV half.</summary>
public sealed class ShulkerBoxRawReferenceSampleTests
{
    [Fact]
    public void RawBindGeometryMatchesSource48()
    {
        var rows = Load();
        Assert.Equal(48, rows.Length);
        var definition = ShulkerBoxModelDefinition.Definition;
        Assert.True(definition.RawSourceFrame);
        Assert.Equal(0f, definition.BaselinePx);
        Assert.True(definition.FaceTransforms is null || definition.FaceTransforms.Count == 0);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/shulker/shulker"] = new(64, 64, new byte[64 * 64 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/shulker/shulker")!,
            64,
            64,
            modelScale: 1f);
        Assert.Equal(48, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        // Definition part order is lid then base; source groups are sorted /base before /lid.
        AssertRows(geometry, rows.Where(r => r.Part == "/lid").ToArray(), 0);
        AssertRows(geometry, rows.Where(r => r.Part == "/base").ToArray(), 24);
    }

    private static void AssertRows(DynamicGeometry geometry, Row[] expected, int startVertex)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            var (x, y, z) = VertexFieldReader.Position(geometry.Vertices, startVertex + i);
            var (u, v) = VertexFieldReader.Uv(geometry.Vertices, startVertex + i);
            Assert.True(MathF.Abs(x - expected[i].X) <= 1e-5f, $"{expected[i].Part} v{i} x {x} vs {expected[i].X}");
            Assert.True(MathF.Abs(y - expected[i].Y) <= 1e-5f, $"{expected[i].Part} v{i} y {y} vs {expected[i].Y}");
            Assert.True(MathF.Abs(z - expected[i].Z) <= 1e-5f, $"{expected[i].Part} v{i} z {z} vs {expected[i].Z}");
            Assert.True(MathF.Abs(u - expected[i].U) <= 1e-5f, $"{expected[i].Part} v{i} u {u} vs {expected[i].U}");
            Assert.True(MathF.Abs(v - expected[i].V) <= 1e-5f, $"{expected[i].Part} v{i} v {v} vs {expected[i].V}");
        }
    }

    private static Row[] Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "shulker-box-raw-vectors.reference.txt");
        Assert.True(File.Exists(path), $"missing reference data: {path}");
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var f = line.Split('\t');
            rows.Add(new Row(f[1], f[2], f[3],
                float.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[6], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[7], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[8], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(f[9], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return [.. rows];
    }

    private readonly record struct Row(string Part, string Cube, string Polygon, float X, float Y, float Z, float U, float V);
}
