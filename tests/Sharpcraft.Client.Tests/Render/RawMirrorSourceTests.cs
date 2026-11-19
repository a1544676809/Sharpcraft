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
///
/// Comparison is by source identity: for each sample polygon (part|cube|polygon) we locate the emitted
/// polygon carrying the same 4 corner positions (source mirror reflects WEST/EAST so polygon indices
/// may differ), then match its 4 vertices one-to-one on the full (position,UV) tuple. Position is
/// compared numerically at &lt;=1e-5; UV is quantized to the half-float the geometry stores first and
/// compared at &lt;=1e-5. No rounding-to-string and no sorting are used.
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

    private readonly record struct Vertex(float X, float Y, float Z, float U, float V);

    [Theory]
    [MemberData(nameof(MirrorRows))]
    public void MirrorPartMatchesSourceByIdentity(string partName)
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
        // The flat witness used to wrap the baked part in a Compiled=null definition (flat mesher path).
        // Production is compiled-only now: rebuild the same isolated part as a root-level compiled node,
        // which reproduces the identical source frame (verified byte-for-byte against the flat output).
        var single = CompiledPartHelper.Definition("mirror", 64, 32, part, "entity/spider/spider");
        Assert.NotNull(single.Compiled);
        var g = new EntityModelMesher().BuildModel(single,
            new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/spider/spider")!, 64, 32, modelScale: 1f);
        var n = g.VertexFloats / ChunkMesher.FloatsPerVertex;
        Assert.Equal(24, n);

        var emitted = new Vertex[n];
        for (var i = 0; i < n; i++)
        {
            var p = VertexFieldReader.Position(g.Vertices, i);
            var uv = VertexFieldReader.Uv(g.Vertices, i);
            emitted[i] = new Vertex(p.X, p.Y, p.Z, uv.U, uv.V);
        }

        // Each source polygon (part|cube|polygon identity) maps to the emitted polygon with the
        // same 4 corner positions. Then its 4 vertices are matched one-to-one at 1e-5.
        for (var poly = 0; poly < 6; poly++)
        {
            var sourceVerts = rows.Where(r => int.Parse(r[3], CultureInfo.InvariantCulture) == poly)
                .Select(r => new Vertex(
                    float.Parse(r[5], CultureInfo.InvariantCulture), float.Parse(r[6], CultureInfo.InvariantCulture),
                    float.Parse(r[7], CultureInfo.InvariantCulture),
                    (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)float.Parse(r[8], CultureInfo.InvariantCulture))),
                    (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)float.Parse(r[9], CultureInfo.InvariantCulture)))))
                .ToArray();
            Assert.Equal(4, sourceVerts.Length);

            var matchedPoly = -1;
            for (var candidate = 0; candidate < 6; candidate++)
            {
                var corners = emitted.Skip(candidate * 4).Take(4).ToArray();
                var sameFace = sourceVerts.All(s => corners.Any(c => SamePos(c, s)))
                    && corners.All(c => sourceVerts.Any(s => SamePos(c, s)));
                if (sameFace) { matchedPoly = candidate; break; }
            }
            Assert.True(matchedPoly >= 0, $"{partName} polygon {poly}: no emitted polygon with the same source corners");

            // One-to-one full-tuple (position,UV) match within the face; each emitted vertex used once.
            var used = new bool[4];
            foreach (var s in sourceVerts)
            {
                var ok = false;
                for (var k = 0; k < 4; k++)
                {
                    if (used[k]) continue;
                    var c = emitted[matchedPoly * 4 + k];
                    if (SameTuple(c, s)) { used[k] = true; ok = true; break; }
                }
                Assert.True(ok, $"{partName} polygon {poly}: no emitted vertex matches source ({s.X:F5},{s.Y:F5},{s.Z:F5}) uv({s.U:F5},{s.V:F5}) at 1e-5");
            }
            Assert.All(used, u => Assert.True(u, $"{partName} polygon {poly}: an emitted vertex was not covered"));
        }
    }

    private static bool SamePos(Vertex a, Vertex b) =>
        MathF.Abs(a.X - b.X) <= Tol && MathF.Abs(a.Y - b.Y) <= Tol && MathF.Abs(a.Z - b.Z) <= Tol;

    private static bool SameTuple(Vertex a, Vertex b) =>
        SamePos(a, b) && MathF.Abs(a.U - b.U) <= Tol && MathF.Abs(a.V - b.V) <= Tol;
}
