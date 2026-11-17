// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Strict source-oracle regression for raw CubeListBuilder.mirror parts (Issue #121). The sample is
/// produced by ref://mc-26.3-analysis/vectors/RawRenderUnitProbe.java from the baked jar layer, so
/// mirror's minX/maxX swap, polygon vertex reversal and UV assignment are already applied at source.
/// Each mirror part's 24 (position,UV) tuples are compared one-to-one at 1e-5 (UV quantized to the
/// half-float the geometry stores). Comparison is a full-tuple multiset: source bakes faces in a
/// different order than our mesher, but each source vertex must match exactly one emitted vertex.
/// </summary>
public sealed class RawMirrorSourceTests
{
    private const float Tol = 1e-5f;
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "spider-raw-vectors.reference.txt");

    // Real Mirror parts of the spider (baked Mirror=true), not a name heuristic.
    public static readonly string[] MirrorParts =
        ["left_front_leg", "left_middle_front_leg", "left_hind_leg", "left_middle_hind_leg"];
    public static IEnumerable<object[]> MirrorRows => MirrorParts.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(MirrorRows))]
    public void MirrorPartMatchesSourceTuples(string partName)
    {
        var def = SpiderModelDefinition.Definition;
        var part = def.Parts.Single(p => p.Name == partName);
        Assert.True(part.Mirror, $"{partName} must be a real Mirror part");

        var rows = File.ReadLines(SamplePath)
            .Where(l => l.StartsWith("SPIDER\t/" + partName + "\t"))
            .Select(l => l.Split('\t')).ToArray();
        Assert.Equal(24, rows.Length);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/spider/spider"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var single = new EntityModelDefinition("mirror", 64, 32, [part],
            texturePath: "entity/spider/spider", baselinePx: 0f, rawSourceFrame: true);
        var g = new EntityModelMesher().BuildModel(single,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/spider/spider")!, 64, 32, modelScale: 1f);
        var n = g.VertexFloats / ChunkMesher.FloatsPerVertex;
        Assert.Equal(24, n);

        string Tuple(float x, float y, float z, float u, float v) =>
            $"{x:F4}|{y:F4}|{z:F4}|{u:F4}|{v:F4}";
        var expected = rows.Select(r => Tuple(
            float.Parse(r[5], CultureInfo.InvariantCulture), float.Parse(r[6], CultureInfo.InvariantCulture),
            float.Parse(r[7], CultureInfo.InvariantCulture),
            (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)float.Parse(r[8], CultureInfo.InvariantCulture))),
            (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)float.Parse(r[9], CultureInfo.InvariantCulture)))))
            .ToList();
        var actual = new List<string>();
        for (var i = 0; i < n; i++)
        {
            var p = VertexFieldReader.Position(g.Vertices, i);
            var uv = VertexFieldReader.Uv(g.Vertices, i);
            actual.Add(Tuple(p.X, p.Y, p.Z, uv.U, uv.V));
        }
        Assert.Equal(expected.OrderBy(x => x).ToArray(), actual.OrderBy(x => x).ToArray());
    }
}
