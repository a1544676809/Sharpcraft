// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Statue geometry in NORMALIZED MODEL SPACE against the jar source oracle. Every vertex full tuple
/// (position+UV) is matched one-to-one at 1e-5, order-independent (the mesher's compiled emission
/// order differs from the probe's; a strict multiset on the full tuple is still value-exact - no
/// rounding, no per-position UV sorting, no tolerance beyond float/half-float noise).
/// NO renderer/world transform is applied: raw BuildModel output is compared directly, matching
/// StatueSourceProbe.java (v/16, root.zRot=PI). Source: ref://mc-26.3-analysis/vectors/StatueSourceProbe.java.
/// </summary>
public sealed class CopperGolemStatueModelSpaceTests
{
    private const float Tol = 1e-5f;
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "copper-golem-statue-modelspace.golden.txt");

    private readonly record struct V(float X, float Y, float Z, float U, float Vv);
    public static IEnumerable<object[]> PoseRows => new[] { "standing", "running", "sitting", "star" }
        .Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(PoseRows))]
    public void RawModelSpaceMatchesSourceOracle(string pose)
    {
        var expected = File.ReadLines(SamplePath)
            .Where(l => !l.StartsWith('#') && l.StartsWith(pose + "|"))
            .Select(l => l.Split('|'))
            .Select(f => new V(PF(f[5]), PF(f[6]), PF(f[7]), Q(PF(f[8])), Q(PF(f[9]))))
            .ToArray();
        Assert.NotEmpty(expected);

        var def = PoseDefinition(pose);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/copper_golem/copper_golem"] = new(64, 64, new byte[64 * 64 * 4])
        });
        var statuePose = new EntityPose();
        statuePose.Set("root", new PoseOffset(System.Numerics.Vector3.Zero,
            new System.Numerics.Vector3(0f, 0f, MathF.PI), System.Numerics.Vector3.Zero));
        var g = new EntityModelMesher().BuildModel(def,
            new EntityRenderState(0, 0, 0f, 0f, 0f, 180f, 0f),
            atlas.Get("entity/copper_golem/copper_golem")!, 64, 64, pose: statuePose, modelScale: 1f);
        var vertexCount = g.VertexFloats / ChunkMesher.FloatsPerVertex;
        Assert.Equal(expected.Length, vertexCount);

        var actual = new V[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var p = VertexFieldReader.Position(g.Vertices, i);
            var uv = VertexFieldReader.Uv(g.Vertices, i);
            actual[i] = new V(p.X, p.Y, p.Z, uv.U, uv.V);
        }

        // Strict one-to-one full-tuple matching: each expected vertex consumed exactly once.
        var used = new bool[actual.Length];
        foreach (var e in expected)
        {
            var found = false;
            for (var j = 0; j < actual.Length; j++)
            {
                if (used[j]) continue;
                var a = actual[j];
                if (MathF.Abs(a.X - e.X) <= Tol && MathF.Abs(a.Y - e.Y) <= Tol
                    && MathF.Abs(a.Z - e.Z) <= Tol && MathF.Abs(a.U - e.U) <= Tol
                    && MathF.Abs(a.Vv - e.Vv) <= Tol)
                {
                    used[j] = true;
                    found = true;
                    break;
                }
            }
            Assert.True(found, $"{pose}: no raw vertex matches sample ({e.X:F5},{e.Y:F5},{e.Z:F5}) uv({e.U:F5},{e.Vv:F5})");
        }
        Assert.All(used, u => Assert.True(u, $"{pose}: a raw vertex was not covered by the sample"));
    }

    private static float Q(float v) =>
        (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)v));

    private static EntityModelDefinition PoseDefinition(string pose) => pose switch
    {
        "standing" => CopperGolemStatueModelDefinition.Standing,
        "running" => CopperGolemStatueModelDefinition.Running,
        "sitting" => CopperGolemStatueModelDefinition.Sitting,
        "star" => CopperGolemStatueModelDefinition.Star,
        _ => throw new ArgumentOutOfRangeException(nameof(pose))
    };

    private static float PF(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
