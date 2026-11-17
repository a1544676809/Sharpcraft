// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Book geometry against the jar-truth reference sample keyed by part|cube|polygon|vertex.
/// Each row is compared strictly (position+UV) by its source face identity, with no sorting or
/// position grouping. Source: ref://mc-26.3-analysis/vectors/BookVectorProbe.java, unit-normalized
/// per issue #128 (vertex v/16 + part translation in blocks, ModelPart.render semantics).
/// </summary>
public sealed class BookRawReferenceTests
{
    private const float Tolerance = 0.002f;
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "book-raw-vectors.golden.txt");

    private sealed record Row(string Part, int Cube, int Polygon, int Vertex, float X, float Y, float Z, float U, float V);

    public static readonly string[] Groups = ["lectern_2", "lectern_3", "lectern_4", "lectern_5", "enchant"];
    public static IEnumerable<object[]> GroupRows => Groups.Select(g => new object[] { g });

    [Theory]
    [MemberData(nameof(GroupRows))]
    public void EveryVertexMatchesTheReference(string group)
    {
        var rows = Load(group);
        Assert.Equal(168, rows.Count);

        var isEnchant = group == "enchant";
        byte facing = isEnchant ? (byte)0 : byte.Parse(group[^1].ToString(), CultureInfo.InvariantCulture);
        var sprite = new SpriteRect(0, 0, 64, 32, 1) { RectId = 1, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
        var state = new BlockEntityRenderState(0f, 0f, 0f,
            isEnchant ? VanillaBlockEntityTypes.EnchantingTable : VanillaBlockEntityTypes.Lectern,
            Facing: facing, HasBook: true);
        var geometry = isEnchant
            ? new EnchantTableRenderer().Build([state], sprite)
            : new LecternRenderer().Build([state], sprite);
        Assert.Equal(168, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);

        // Emission order: one 24-vertex block per part in declaration order.
        var partOrder = new[] { "left_lid", "right_lid", "seam", "left_pages", "right_pages", "flip_page1", "flip_page2" };
        foreach (var row in rows)
        {
            var partIndex = Array.IndexOf(partOrder, row.Part.TrimStart('/'));
            Assert.True(partIndex >= 0, $"unknown sample part {row.Part}");
            var vIndex = partIndex * 24 + row.Polygon * 4 + row.Vertex;
            var p = VertexFieldReader.Position(geometry.Vertices, vIndex);
            var uv = VertexFieldReader.Uv(geometry.Vertices, vIndex);
            Assert.True(
                MathF.Abs(p.X - row.X) <= Tolerance && MathF.Abs(p.Y - row.Y) <= Tolerance
                    && MathF.Abs(p.Z - row.Z) <= Tolerance
                    && MathF.Abs(uv.U - row.U) <= Tolerance && MathF.Abs(uv.V - row.V) <= Tolerance,
                $"{group} {row.Part} cube{row.Cube} poly{row.Polygon} v{row.Vertex}:"
                    + $" ours ({p.X:F4},{p.Y:F4},{p.Z:F4}) uv ({uv.U:F4},{uv.V:F4})"
                    + $" but sample says ({row.X:F4},{row.Y:F4},{row.Z:F4}) uv ({row.U:F4},{row.V:F4})");
        }
    }

    private static List<Row> Load(string group)
    {
        var result = new List<Row>();
        foreach (var line in File.ReadLines(SamplePath))
        {
            if (line.StartsWith('#') || line.Length == 0) continue;
            var p = line.Split('|');
            if (p[0] != group) continue;
            result.Add(new Row(p[1], int.Parse(p[2], CultureInfo.InvariantCulture),
                int.Parse(p[3], CultureInfo.InvariantCulture), int.Parse(p[4], CultureInfo.InvariantCulture),
                Parse(p[5]), Parse(p[6]), Parse(p[7]), Parse(p[8]), Parse(p[9])));
        }
        return result;
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
}
