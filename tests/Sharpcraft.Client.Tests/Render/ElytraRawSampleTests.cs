// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Raw Elytra equipment layer source sample (`ElytraModel.createLayer`, adult and ELYTRA_BABY).
/// The compiled layer keeps the source bind rotations, grow and cube UVs; keyed by source
/// part|cube|polygon|vertex every emitted vertex must be consumed once.
/// </summary>
public sealed class ElytraRawSampleTests
{
    [Theory]
    [InlineData(false, "elytra-raw-vectors.reference.txt")]
    [InlineData(true, "elytra-baby-raw-vectors.reference.txt")]
    public void RawElytraLayerMatchesJarSourceGeometry(bool baby, string sampleFile)
    {
        var rows = LoadSample(sampleFile);
        Assert.Equal(48, rows.Count);
        var groups = rows.GroupBy(row => (row.Part, row.Cube)).ToArray();
        Assert.Equal(2, groups.Length);
        Assert.All(groups, group => Assert.Equal(24, group.Count()));

        var layer = EntityModelLayerHelpers.BuildElytraSourceLayer(baby);
        var definition = new EntityModelDefinition(baby ? "elytra_raw_baby" : "elytra_raw_adult", 64, 32, texturePath: "entity/equipment/wings/elytra", compiled: layer, baselinePx: 0f, rawSourceFrame: true);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/equipment/wings/elytra"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/equipment/wings/elytra")!, 64, 32, modelScale: 1f);
        var actualCount = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        Assert.Equal(48, actualCount);

        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var offset = 0;
        foreach (var child in layer.Root.ChildArray)
        {
            offsets[child.Name] = offset;
            offset += child.CubeArray.Length * 24;
        }
        Assert.Equal(48, offset);

        var expected = new Dictionary<int, Sample>();
        foreach (var row in rows)
        {
            var nodeName = row.Part[(row.Part.LastIndexOf('/') + 1)..];
            Assert.True(offsets.TryGetValue(nodeName, out var nodeOffset), $"no node for {row.Part}");
            var index = nodeOffset + (row.Polygon * 4) + row.Vertex;
            Assert.InRange(index, 0, 47);
            Assert.True(expected.TryAdd(index, row),
                $"duplicate {index} for {row.Part}|{row.Cube}|{row.Polygon}|{row.Vertex}");
        }
        Assert.Equal(48, expected.Count);

        for (var i = 0; i < actualCount; i++)
        {
            Assert.True(expected.TryGetValue(i, out var s), $"vertex {i} not consumed");
            var p = VertexFieldReader.Position(geometry.Vertices, i);
            var uv = VertexFieldReader.Uv(geometry.Vertices, i);
            var su = QuantizeHalf(s.U);
            var sv = QuantizeHalf(s.V);
            Assert.True(
                MathF.Abs(s.X - p.X) < 1e-5f
                && MathF.Abs(s.Y - p.Y) < 1e-5f
                && MathF.Abs(s.Z - p.Z) < 1e-5f,
                $"vertex {i} {s.Part}|{s.Polygon}|{s.Vertex}: src=({s.X},{s.Y},{s.Z}) our=({p.X},{p.Y},{p.Z})");
            Assert.True(
                MathF.Abs(su - uv.U) < 1e-5f && MathF.Abs(sv - uv.V) < 1e-5f,
                $"vertex {i} UV: src=({su},{sv}) our=({uv.U},{uv.V})");
        }
    }

    private static List<Sample> LoadSample(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", file);
        var rows = new List<Sample>();
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.StartsWith('#') || string.IsNullOrWhiteSpace(raw)) continue;
            var f = raw.Split('\t');
            rows.Add(new Sample(f[1], int.Parse(f[2], CultureInfo.InvariantCulture),
                int.Parse(f[3], CultureInfo.InvariantCulture), int.Parse(f[4], CultureInfo.InvariantCulture),
                float.Parse(f[5], CultureInfo.InvariantCulture), float.Parse(f[6], CultureInfo.InvariantCulture),
                float.Parse(f[7], CultureInfo.InvariantCulture), float.Parse(f[8], CultureInfo.InvariantCulture),
                float.Parse(f[9], CultureInfo.InvariantCulture)));
        }
        return rows;
    }

    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));

    private sealed record Sample(string Part, int Cube, int Polygon, int Vertex, float X, float Y, float Z, float U, float V);
}
