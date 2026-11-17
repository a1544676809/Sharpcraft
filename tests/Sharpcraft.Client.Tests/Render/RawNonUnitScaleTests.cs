// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Raw non-unit PartPose scale (Issue #122). Source `PartPose.scaled(factor)` multiplies the pivot
/// offset and the bind scale; `ModelPart.translateAndRotate` applies translate(pivot), rotate, then
/// scale(part.xScale...). This test drives the real compiled raw tree and compares every emitted
/// vertex against that source formula.
/// </summary>
public sealed class RawNonUnitScaleTests
{
    [Fact]
    public void RawCompiledNodeAppliesSourcePartPoseScale()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        const float factor = 0.5f;
        var pose = VanillaPartPose.Offset(1f, 2f, 3f).Scaled(factor);
        root.AddOrReplaceChild("scaled", pose,
            new VanillaCubeDefinition("scaled", 0, 0, -2f, -2f, -2f, 4f, 4f, 4f));

        var definition = EntityModelDefinition.FromTree(
            "raw_scaled_probe", 64, 64, new VanillaMeshDefinition(root),
            texturePath: "probe/raw_scaled", baselinePx: 0f, rawSourceFrame: true);
        Assert.True(definition.RawSourceFrame);
        var node = definition.Compiled!.Find("scaled")!;
        Assert.Equal(factor, node.ScaleX, 6);
        Assert.Equal(factor, node.ScaleY, 6);
        Assert.Equal(factor, node.ScaleZ, 6);

        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/raw_scaled"] = new(64, 64, new byte[64 * 64 * 4])
        });
        var state = new EntityRenderState(1, 0, 0f, 0f, 0f, 180f, 0f);
        var geometry = new EntityModelMesher().BuildModel(
            definition, state, atlas.Get("probe/raw_scaled")!, 64, 64, modelScale: 1f);

        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        var expectedCorners = new List<Vector3>();
        foreach (var x in new[] { -2f, 2f })
        foreach (var y in new[] { -2f, 2f })
        foreach (var z in new[] { -2f, 2f })
        {
            expectedCorners.Add((new Vector3(pose.X, pose.Y, pose.Z) + new Vector3(x, y, z) * factor) / 16f);
        }

        for (var i = 0; i < 24; i++)
        {
            var actual = VertexFieldReader.Position(geometry.Vertices, i);
            Assert.True(expectedCorners.Any(expected =>
                MathF.Abs(expected.X - actual.X) < 1e-5f
                && MathF.Abs(expected.Y - actual.Y) < 1e-5f
                && MathF.Abs(expected.Z - actual.Z) < 1e-5f),
                $"vertex {i}: actual=({actual.X},{actual.Y},{actual.Z}) expected=[{string.Join(";", expectedCorners)}]");
        }
    }
}
