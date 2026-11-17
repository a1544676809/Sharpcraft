// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// EnderDragon raw source-frame strict sample. The jar RawRenderUnitProbe emits 1560 rows
/// (part|cube|polygon|vertex); the DSL flattens the source's multi-cube head/body/wing parts into
/// individual compiled nodes, so this test maps each source group to its flat node and compares
/// the real raw compiled-tree geometry at 1e-5 (UV half-quantized).
/// </summary>
public sealed class EnderDragonRawSampleTests
{
    [Fact]
    public void RawEnderDragonMatchesJarSourceGeometry()
    {
        var definition = EnderDragonModelDefinition.Definition;
        Assert.True(definition.RawSourceFrame);
        Assert.NotNull(definition.Compiled);

        var rows = LoadSample();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/enderdragon/dragon"] = new(256, 256, new byte[256 * 256 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(1, EnderDragonModelDefinition.EnderDragonEntityTypeId, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/enderdragon/dragon")!, 256, 256, modelScale: 1f);

        var actualCount = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        Assert.Equal(1560, actualCount);
        Assert.Equal(1560, rows.Count);
        var groups = rows.GroupBy(row => (row.Part, row.Cube)).ToArray();
        Assert.Equal(65, groups.Length);
        Assert.All(groups, group => Assert.Equal(24, group.Count()));
        Assert.Contains(groups, group => group.Key.Part.StartsWith("/head", StringComparison.Ordinal));
        Assert.Contains(groups, group => group.Key.Part.StartsWith("/tail", StringComparison.Ordinal));
        Assert.Contains(groups, group => group.Key.Part.EndsWith("/left_wing", StringComparison.Ordinal));
        Assert.Contains(groups, group => group.Key.Part.EndsWith("/right_wing_tip", StringComparison.Ordinal));

        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var offset = 0;
        void Walk(EntityModelPartNode node)
        {
            offsets[node.Name] = offset;
            offset += node.CubeArray.Length * 24;
            foreach (var child in node.ChildArray)
            {
                Walk(child);
            }
        }
        foreach (var child in definition.Compiled!.Root.ChildArray)
        {
            Walk(child);
        }
        Assert.Equal(1560, offset);

        var expected = new Dictionary<int, Sample>();
        foreach (var row in rows)
        {
            var flatName = FlatName(row.Part, row.Cube);
            Assert.True(offsets.TryGetValue(flatName, out var nodeOffset),
                $"no compiled node for source part {row.Part} cube {row.Cube} -> {flatName}");
            var actualIndex = nodeOffset + (row.Polygon * 4) + row.Vertex;
            Assert.InRange(actualIndex, 0, 1559);
            Assert.True(expected.TryAdd(actualIndex, row),
                $"duplicate actual vertex {actualIndex} for source {row.Part}|{row.Cube}|{row.Polygon}|{row.Vertex}");
        }
        Assert.Equal(1560, expected.Count);

        for (var i = 0; i < actualCount; i++)
        {
            Assert.True(expected.TryGetValue(i, out var s), $"actual vertex {i} not consumed by any source row");
            var p = VertexFieldReader.Position(geometry.Vertices, i);
            var uv = VertexFieldReader.Uv(geometry.Vertices, i);
            var su = QuantizeHalf(s.U);
            var sv = QuantizeHalf(s.V);
            Assert.True(
                MathF.Abs(s.X - p.X) < 1e-5f
                && MathF.Abs(s.Y - p.Y) < 1e-5f
                && MathF.Abs(s.Z - p.Z) < 1e-5f,
                $"vertex {i} {s.Part}|{s.Cube}|{s.Polygon}|{s.Vertex}: src=({s.X},{s.Y},{s.Z}) our=({p.X},{p.Y},{p.Z})");
            Assert.True(
                MathF.Abs(su - uv.U) < 1e-5f && MathF.Abs(sv - uv.V) < 1e-5f,
                $"vertex {i} {s.Part}|{s.Cube}|{s.Polygon}|{s.Vertex} UV: src=({su},{sv}) our=({uv.U},{uv.V})");
        }
    }

    private static string FlatName(string part, int cube)
    {
        if (part == "/head")
        {
            return cube switch
            {
                0 => "upperlip",
                1 => "upperhead",
                2 => "scale_left",
                3 => "nostril_left",
                4 => "scale_right",
                5 => "nostril_right",
                _ => throw new Xunit.Sdk.XunitException($"unexpected /head cube {cube}")
            };
        }

        if (part == "/head/jaw") return "jaw";
        if (part == "/body")
        {
            return cube == 0 ? "body" : "body_scale_" + cube;
        }

        var index = part.IndexOf("/neck", StringComparison.Ordinal);
        if (index >= 0)
        {
            var name = part[(index + 1)..];
            return cube == 0 ? name : name + "_scale";
        }

        index = part.IndexOf("/tail", StringComparison.Ordinal);
        if (index >= 0)
        {
            var name = part[(index + 1)..];
            return cube == 0 ? name : name + "_scale";
        }

        if (part.EndsWith("/left_wing", StringComparison.Ordinal))
        {
            return cube == 0 ? "left_wing" : "left_wing_skin";
        }

        if (part.EndsWith("/left_wing/left_wing_tip", StringComparison.Ordinal))
        {
            return cube == 0 ? "left_wing_tip" : "left_wing_tip_skin";
        }

        if (part.EndsWith("/right_wing", StringComparison.Ordinal))
        {
            return cube == 0 ? "right_wing" : "right_wing_skin";
        }

        if (part.EndsWith("/right_wing/right_wing_tip", StringComparison.Ordinal))
        {
            return cube == 0 ? "right_wing_tip" : "right_wing_tip_skin";
        }

        return part[(part.LastIndexOf('/') + 1)..];
    }

    private static List<Sample> LoadSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "enderdragon-raw-vectors.reference.txt");
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
