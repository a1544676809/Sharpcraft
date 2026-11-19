// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using System.Reflection;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Source face-order/UV regression for raw mirrored cubes (Issue #156). Sheep legs are built by
/// <c>QuadrupedModel.createLegs(..., mirrorLeftLeg: true, mirrorRightLeg: true)</c>; the reference
/// <c>ModelPart.Cube</c> reflects the cube in X and then the Polygon constructor reverses the four
/// vertex/UV pairs. The sample is the jar's <c>RawRenderUnitProbe</c> per-polygon/vertex output.
/// </summary>
public sealed class RawFaceOrderTests
{
    [Theory]
    [InlineData("right_hind_leg")]
    [InlineData("left_hind_leg")]
    [InlineData("right_front_leg")]
    [InlineData("left_front_leg")]
    public void SheepMirroredLegMatchesSourceFaceOrderAndUv(string partName)
    {
        var mesh = (VanillaMeshDefinition)typeof(SheepModelDefinition)
            .GetField("SheepMesh", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var full = EntityModelDefinition.FromTree(
            "raw_sheep_probe", 64, 32, mesh, texturePath: "entity/sheep/sheep",
            baselinePx: 0f, rawSourceFrame: true);
        var leg = full.Parts.Single(part => part.Name == partName);
        // SheepModel uses createBodyMesh(12, mirrorLeftLeg:false, mirrorRightLeg:true).
        leg = leg with { Mirror = partName.StartsWith("right_", StringComparison.Ordinal) };
        // Production is compiled-only: rebuild the isolated mirrored leg as a root-level compiled node.
        // This reproduces the former flat part-only frame byte-for-byte (jar sample stays the oracle).
        var single = CompiledPartHelper.Definition("raw_sheep_leg", 64, 32, leg, "entity/sheep/sheep");
        Assert.NotNull(single.Compiled);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["entity/sheep/sheep"] = new(64, 32, new byte[64 * 32 * 4])
        });
        var state = new EntityRenderState(1, SheepModelDefinition.SheepEntityTypeId, 0f, 0f, 0f, 180f, 0f);
        var geometry = new EntityModelMesher().BuildModel(
            single, state, atlas.Get("entity/sheep/sheep")!, 64, 32, modelScale: 1f);

        var expected = LoadSample("/" + partName);
        Assert.Equal(24, expected.Length);
        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < expected.Length; i++)
        {
            var (poly, vertex, sx, sy, sz, su, sv) = expected[i];
            var position = VertexFieldReader.Position(geometry.Vertices, i);
            var uv = VertexFieldReader.Uv(geometry.Vertices, i);
            Assert.True(
                MathF.Abs(sx - position.X) < 1e-5f
                && MathF.Abs(sy - position.Y) < 1e-5f
                && MathF.Abs(sz - position.Z) < 1e-5f,
                $"{partName} poly={poly} v={vertex} pos src=({sx},{sy},{sz}) our=({position.X},{position.Y},{position.Z})");
            Assert.True(
                MathF.Abs(QuantizeHalf(su) - uv.U) < 1e-5f && MathF.Abs(QuantizeHalf(sv) - uv.V) < 1e-5f,
                $"{partName} poly={poly} v={vertex} uv src=({su},{sv}) our=({uv.U},{uv.V})");
        }
    }

    private static (int Polygon, int Vertex, float X, float Y, float Z, float U, float V)[] LoadSample(string part)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Repro", "Data", "sheep-raw-vectors.reference.txt");
        var rows = new List<(int, int, float, float, float, float, float)>();
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.StartsWith('#') || string.IsNullOrWhiteSpace(raw)) continue;
            var f = raw.Split('\t');
            if (f[1] != part) continue;
            rows.Add((int.Parse(f[3], CultureInfo.InvariantCulture), int.Parse(f[4], CultureInfo.InvariantCulture),
                float.Parse(f[5], CultureInfo.InvariantCulture), float.Parse(f[6], CultureInfo.InvariantCulture),
                float.Parse(f[7], CultureInfo.InvariantCulture), float.Parse(f[8], CultureInfo.InvariantCulture),
                float.Parse(f[9], CultureInfo.InvariantCulture)));
        }
        return [.. rows.OrderBy(r => r.Item1).ThenBy(r => r.Item2)];
    }

    private static float QuantizeHalf(float value) =>
        (float)BitConverter.UInt16BitsToHalf((ushort)BitConverter.HalfToUInt16Bits((Half)value));
}
