// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Statue geometry in NORMALIZED MODEL SPACE against the jar source oracle, compared by the STRICT
/// source identity key part|cube|polygon|vertex. Each sample row resolves to a DSL part (multi-cube
/// source parts such as /body/head are flattened into head/nose/antenna/antenna_tip in addBox order);
/// its 24 vertices are compared by polygon/vertex index at 1e-5. No sorting, no position multiset.
/// NO renderer/world transform is applied: raw BuildModel output is compared directly, matching
/// StatueSourceProbe.java (v/16, root.zRot=PI). Source: ref://mc-26.3-analysis/vectors/StatueSourceProbe.java.
/// </summary>
public sealed class CopperGolemStatueModelSpaceTests
{
    private const float Tol = 1e-5f;
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "copper-golem-statue-modelspace.golden.txt");

    private sealed record Row(string Part, int Cube, int Poly, int Vertex, float X, float Y, float Z, float U, float V);
    // Sitting has 5 extra source cubes (body_r1, both arms_r1, both legs_r1): 11*24 = 264, legitimate.
    private static readonly (string Pose, int Expected)[] PoseCounts =
    [
        ("standing", 216), ("running", 216), ("sitting", 264), ("star", 216)
    ];
    public static IEnumerable<object[]> PoseRows => PoseCounts.Select(p => new object[] { p.Pose, p.Expected });

    [Theory]
    [MemberData(nameof(PoseRows))]
    public void RawModelSpaceMatchesSourceOracleByKey(string pose, int expectedCount)
    {
        var rows = File.ReadLines(SamplePath)
            .Where(l => !l.StartsWith('#') && l.StartsWith(pose + "|"))
            .Select(Parse).ToArray();
        // Explicit per-pose coverage: no silent expected.Length==vertexCount auto-accept.
        Assert.Equal(expectedCount, rows.Length);

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
        Assert.Equal(expectedCount, vertexCount);

        // Emission order: depth-first over the compiled tree, cube-by-cube within each node.
        // order[i] is the DSL cube name emitted at vertex block i (24 vertices per cube).
        var order = FlattenCompiledCubes(def);
        foreach (var group in rows.GroupBy(r => (r.Part, r.Cube)))
        {
            var (part, cube) = group.Key;
            var target = DslName(pose, part, cube);
            var targetIndex = Array.FindIndex(order, n => n == target);
            Assert.True(targetIndex >= 0, $"{pose}: no DSL cube for {part} cube {cube} ({target}); order=[{string.Join(",", order)}]");
            var offset = targetIndex * 24;
            Assert.Equal(24, group.Count()); // 6 polygons x 4 vertices per cube
            // Same cube contributes one polygon per face; polygon index maps directly to 4-vertex block.
            foreach (var r in group)
            {
                var index = offset + r.Poly * 4 + r.Vertex;
                var pos = VertexFieldReader.Position(g.Vertices, index);
                var uv = VertexFieldReader.Uv(g.Vertices, index);
                var eu = (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)r.U));
                var ev = (float)BitConverter.UInt16BitsToHalf(BitConverter.HalfToUInt16Bits((Half)r.V));
                Assert.True(MathF.Abs(pos.X - r.X) <= Tol && MathF.Abs(pos.Y - r.Y) <= Tol && MathF.Abs(pos.Z - r.Z) <= Tol,
                    $"{pose} {part} cube{cube} poly{r.Poly} v{r.Vertex}: pos ({pos.X:F5},{pos.Y:F5},{pos.Z:F5}) vs sample ({r.X:F5},{r.Y:F5},{r.Z:F5})");
                Assert.True(MathF.Abs(uv.U - eu) <= Tol && MathF.Abs(uv.V - ev) <= Tol,
                    $"{pose} {part} cube{cube} poly{r.Poly} v{r.Vertex} UV: ({uv.U:F5},{uv.V:F5}) vs sample ({eu:F5},{ev:F5})");
            }
        }
    }

    /// <summary>Compiled emission order of DSL cube names (depth-first, cube-by-cube per node).</summary>
    private static string[] FlattenCompiledCubes(EntityModelDefinition def)
    {
        var result = new List<string>();
        void Walk(Sharpcraft.Client.Render.EntityModelPartNode node)
        {
            foreach (var cube in node.CubeArray) result.Add(cube.Name);
            foreach (var c in node.ChildArray) Walk(c);
        }
        foreach (var child in def.Compiled!.Root.ChildArray) Walk(child);
        return result.ToArray();
    }

    /// <summary>
    /// Resolve a source (part path, cube index) to the flat DSL cube name. The source probe numbers
    /// cubes by addBox order within a source part; the DSL flattens them using the authored cube names.
    /// The map mirrors the DSL definitions (CopperGolemStatueModelDefinition.Build*Mesh), which are
    /// themselves the source PartPose/texOffs/addBox literals.
    /// </summary>
    private static string DslName(string pose, string part, int cube)
    {
        var last = part.TrimStart('/').Split('/').Last();
        if (last == "head")
        {
            // Standing/running/star head addBox order: head, nose, antenna, antenna_tip.
            if (pose == "sitting")
                return cube switch { 0 => "head", 1 => "antenna_tip", 2 => "head_main", _ => "nose" };
            return cube switch { 0 => "head", 1 => "nose", 2 => "antenna", _ => "antenna_tip" };
        }
        if (last == "body" && pose == "sitting")
            return cube == 0 ? "body" : "body_main";
        return last; // single-cube parts and explicit *_r1 child paths name the DSL cube directly
    }

    private static EntityModelDefinition PoseDefinition(string pose) => pose switch
    {
        "standing" => CopperGolemStatueModelDefinition.Standing,
        "running" => CopperGolemStatueModelDefinition.Running,
        "sitting" => CopperGolemStatueModelDefinition.Sitting,
        "star" => CopperGolemStatueModelDefinition.Star,
        _ => throw new ArgumentOutOfRangeException(nameof(pose))
    };

    private static Row Parse(string line)
    {
        var f = line.Split('|');
        return new Row(f[1], int.Parse(f[2]), int.Parse(f[3]), int.Parse(f[4]),
            PF(f[5]), PF(f[6]), PF(f[7]), PF(f[8]), PF(f[9]));
    }
    private static float PF(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
