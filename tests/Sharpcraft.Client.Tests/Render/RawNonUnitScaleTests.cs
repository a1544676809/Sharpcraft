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

        var geometry = BuildModelSpace(definition, "probe/raw_scaled");
        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        var expectedCorners = new List<Vector3>();
        foreach (var x in new[] { -2f, 2f })
        foreach (var y in new[] { -2f, 2f })
        foreach (var z in new[] { -2f, 2f })
        {
            expectedCorners.Add((new Vector3(pose.X, pose.Y, pose.Z) + new Vector3(x, y, z) * factor) / 16f);
        }
        AssertVerticesMatch(geometry, expectedCorners);
    }

    [Fact]
    public void RawScaledParentChildAppliesTranslateRotateScaleOrder()
    {
        const float factor = 0.5f;
        const float rx = 0.35f;
        const float ry = -0.45f;
        const float rz = 0.25f;
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var parentPose = VanillaPartPose.OffsetAndRotation(1f, 2f, 3f, rx, ry, rz).Scaled(factor);
        var parent = root.AddOrReplaceChild("parent", parentPose,
            new VanillaCubeDefinition("parent", 0, 0, -2f, -2f, -2f, 4f, 4f, 4f));
        var childPose = VanillaPartPose.Offset(2f, 0f, 0f);
        parent.AddOrReplaceChild("child", childPose,
            new VanillaCubeDefinition("child", 16, 16, -1f, -1f, -1f, 2f, 2f, 2f));

        var definition = EntityModelDefinition.FromTree(
            "raw_scaled_parent_child", 64, 64, new VanillaMeshDefinition(root),
            texturePath: "probe/raw_scaled_parent_child", baselinePx: 0f, rawSourceFrame: true);
        Assert.Equal(factor, definition.Compiled!.Find("parent")!.ScaleX, 6);
        Assert.Equal(1f, definition.Compiled.Find("child")!.ScaleX, 6);

        var geometry = BuildModelSpace(definition, "probe/raw_scaled_parent_child");
        Assert.Equal(48, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);

        var rotation = Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateRotationZ(rz);
        var parentPivot = new Vector3(parentPose.X, parentPose.Y, parentPose.Z);
        var expectedCorners = new List<Vector3>();
        foreach (var x in new[] { -2f, 2f })
        foreach (var y in new[] { -2f, 2f })
        foreach (var z in new[] { -2f, 2f })
        {
            expectedCorners.Add((parentPivot + Vector3.Transform(new Vector3(x, y, z) * factor, rotation)) / 16f);
        }
        var childPivot = new Vector3(childPose.X, childPose.Y, childPose.Z);
        foreach (var x in new[] { -1f, 1f })
        foreach (var y in new[] { -1f, 1f })
        foreach (var z in new[] { -1f, 1f })
        {
            expectedCorners.Add((parentPivot
                + Vector3.Transform((childPivot + new Vector3(x, y, z)) * factor, rotation)) / 16f);
        }
        AssertVerticesMatch(geometry, expectedCorners);

        // Parent/child frame oracle: the child part origin is T(parent) + R(parent)*(childPivot*factor).
        var childFrame = EntityModelMesher.PartWorldMatrix(definition, State(180f), "child");
        var childOrigin = Vector3.Transform(Vector3.Zero, childFrame);
        var expectedChildOrigin = (parentPivot + Vector3.Transform(childPivot * factor, rotation))
            * EntityPlaceholderMesher.ModelScale / 16f;
        Assert.True(
            MathF.Abs(childOrigin.X - expectedChildOrigin.X) < 1e-5f
            && MathF.Abs(childOrigin.Y - expectedChildOrigin.Y) < 1e-5f
            && MathF.Abs(childOrigin.Z - expectedChildOrigin.Z) < 1e-5f,
            $"child frame origin actual=({childOrigin.X},{childOrigin.Y},{childOrigin.Z}) expected=({expectedChildOrigin.X},{expectedChildOrigin.Y},{expectedChildOrigin.Z})");
    }

    private static DynamicGeometry BuildModelSpace(EntityModelDefinition definition, string texturePath)
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [texturePath] = new(64, 64, new byte[64 * 64 * 4])
        });
        return new EntityModelMesher().BuildModel(
            definition, State(180f), atlas.Get(texturePath)!, 64, 64, modelScale: 1f);
    }

    private static EntityRenderState State(float yaw) => new(1, 0, 0f, 0f, 0f, yaw, 0f);

    private static void AssertVerticesMatch(DynamicGeometry geometry, List<Vector3> expectedCorners)
    {
        for (var i = 0; i < geometry.VertexFloats / ChunkMesher.FloatsPerVertex; i++)
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
