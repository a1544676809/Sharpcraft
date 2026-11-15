// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// T-850: the chest must produce the geometry the reference client produces, vertex for vertex.
/// </summary>
/// <remarks>
/// The expectation file under Data was produced by running the official 26.3 client: a harness in
/// the analysis workspace bakes the real chest model, calls its own animation setup and records
/// every vertex the model writes. It is not derived from this implementation, so a disagreement
/// means this implementation is wrong rather than the expectation.
///
/// The comparison is a multiset match on (x, y, z, u, v), not a stream order match: the two sides
/// build their quads in different orders. Positions are float32 on our side; UV is stored as half
/// floats in the vertex, so both need the same tolerance.
/// </remarks>
public sealed class Repro_T850ChestGeometryTests
{
    private const float Tolerance = 5e-4f;

    [Fact]
    public void EveryChestVertexMatchesTheReferenceClient()
    {
        var expected = Load(Path.Combine("Repro", "Data", "chest-single-vectors.golden.txt"));
        var actual = Build();

        var groups = expected.GroupBy(row => row.Group);
        var failures = new List<string>();

        foreach (var group in groups)
        {
            var want = group.ToList();
            var have = actual.Where(row => row.Group == group.Key).ToList();
            if (have.Count != want.Count)
            {
                failures.Add($"{group.Key}: reference has {want.Count} vertices, we have {have.Count}");
                continue;
            }

            var used = new bool[have.Count];
            var matched = 0;
            var worst = 0f;
            foreach (var wantRow in want)
            {
                for (var i = 0; i < have.Count; i++)
                {
                    if (used[i])
                    {
                        continue;
                    }

                    var delta = wantRow.Distance(have[i]);
                    if (delta > Tolerance)
                    {
                        continue;
                    }

                    used[i] = true;
                    matched++;
                    worst = MathF.Max(worst, delta);
                    break;
                }
            }

            if (matched != want.Count)
            {
                failures.Add($"{group.Key}: {matched}/{want.Count} vertices matched");
            }
        }

        Assert.True(failures.Count == 0, "chest geometry differs from the reference client:\n" + string.Join("\n", failures));
    }

    private static List<Row> Load(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        Assert.True(File.Exists(path), $"missing reference data: {path}");

        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('|');
            rows.Add(new Row(
                string.Join('|', parts[0..4]),
                F(parts[4]), F(parts[5]), F(parts[6]), F(parts[7]), F(parts[8])));
        }

        return rows;
    }

    private static List<Row> Build()
    {
        var mesher = new ChestMesher();
        var sprite = new SpriteRect(0, 0, 64, 64, 1);
        var rows = new List<Row>();

        foreach (var (facingName, facing) in new[]
                 {
                     ("north", (byte)4), ("south", (byte)5), ("west", (byte)0), ("east", (byte)1)
                 })
        {
            foreach (var open in new[] { 0f, 0.5f, 1f })
            {
                var state = new BlockEntityRenderState(
                    0f, 0f, 0f,
                    VanillaBlockEntityTypes.Chest,
                    Sky: 15,
                    Block: 0,
                    Open: open,
                    Facing: facing,
                    ChestType: 0,
                    ChestMaterial: 0);

                var geometry = mesher.Build(new[] { state }, sprite);
                var group = $"chest|{facingName}|{open.ToString("F6", CultureInfo.InvariantCulture)}|single";

                for (var i = 0; i < geometry.VertexFloats; i += ChunkMesher.FloatsPerVertex)
                {
                    var uvBits = BitConverter.SingleToUInt32Bits(geometry.Vertices[i + 4]);
                    rows.Add(new Row(
                        group,
                        geometry.Vertices[i],
                        geometry.Vertices[i + 1],
                        geometry.Vertices[i + 2],
                        (float)BitConverter.UInt16BitsToHalf((ushort)(uvBits & 0xFFFF)),
                        (float)BitConverter.UInt16BitsToHalf((ushort)(uvBits >> 16))));
                }
            }
        }

        return rows;
    }

    private static float F(string text) => float.Parse(text, CultureInfo.InvariantCulture);

    private readonly record struct Row(string Group, float X, float Y, float Z, float U, float V)
    {
        public float Distance(Row other)
        {
            var position = MathF.Max(
                MathF.Abs(X - other.X),
                MathF.Max(MathF.Abs(Y - other.Y), MathF.Abs(Z - other.Z)));
            var uv = MathF.Max(MathF.Abs(U - other.U), MathF.Abs(V - other.V));
            return MathF.Max(position, uv);
        }
    }
}
