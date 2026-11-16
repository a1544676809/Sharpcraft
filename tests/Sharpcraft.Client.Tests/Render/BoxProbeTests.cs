// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Conversion-layer probe: one box, no rotation, no deformation, pivot at the origin. The same numbers
/// go into the reference harness and into our bake, so the two vertex sets are directly comparable.
/// </summary>
/// <remarks>
/// The reference emits the box as <c>+-0.125</c> on every axis. Ours matches on x and z and is lifted
/// for no model height. The <c>24</c> pixel model height the bake adds by default is the reference renderer'
/// baseline (<c>scale(-1,-1,1)</c> then <c>translate(0,-1.501,0)</c>), so a block entity asks for zero and
/// uses the reference numbers as written. Issue 101 tracks the remaining per renderer cleanup.
/// and into the entity transform, after which this test should expect the reference values verbatim.
/// </remarks>
public sealed class BoxProbeTests
{
    /// <summary>Reference vertices for the probe box, straight from the 26.3 harness.</summary>
    private static readonly (float X, float Y, float Z)[] Reference =
    [
        (0.125f, -0.125f, 0.125f),
        (-0.125f, -0.125f, 0.125f),
        (-0.125f, -0.125f, -0.125f),
        (0.125f, -0.125f, -0.125f),
        (0.125f, 0.125f, -0.125f),
        (-0.125f, 0.125f, -0.125f),
        (-0.125f, 0.125f, 0.125f),
        (0.125f, 0.125f, 0.125f)
    ];

    /// <summary>The model height the bake maps y-down through, in blocks.</summary>
    private const float ModelHeightOffset = 1.5f;

    [Fact]
    public void TheBoxMatchesTheReferenceApartFromTheModelHeightBaseline()
    {
        var geometry = BuildBox();

        var vertices = new List<(float X, float Y, float Z)>();
        for (var vertex = 0; vertex + 2 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            vertices.Add((
                MathF.Round(geometry.Vertices[vertex], 3),
                MathF.Round(geometry.Vertices[vertex + 1], 3),
                MathF.Round(geometry.Vertices[vertex + 2], 3)));
        }

        Assert.Equal(24, vertices.Count);

        var distinct = vertices.Distinct().OrderBy(vertex => vertex).ToArray();
        var expected = Reference.OrderBy(vertex => vertex).ToArray();
        Assert.Equal(expected.Length, distinct.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], distinct[index]);
        }
    }

    [Fact]
    public void TheRawSourceFrameBoxMatchesTheReferenceVerbatim()
    {
        var geometry = BuildBox(rawSourceFrame: true, yaw: 180f);

        var vertices = new List<(float X, float Y, float Z, float U, float V)>();
        for (var vertex = 0; vertex + 4 < geometry.VertexFloats; vertex += ChunkMesher.FloatsPerVertex)
        {
            vertices.Add((
                geometry.Vertices[vertex],
                geometry.Vertices[vertex + 1],
                geometry.Vertices[vertex + 2],
                VertexFieldReader.Uv(geometry.Vertices, vertex / ChunkMesher.FloatsPerVertex).U,
                VertexFieldReader.Uv(geometry.Vertices, vertex / ChunkMesher.FloatsPerVertex).V));
        }

        var expected = new (float X, float Y, float Z, float U, float V)[]
        {
            (0.125f, -0.125f, 0.125f, 0.125f, 0f),
            (-0.125f, -0.125f, 0.125f, 0.0625f, 0f),
            (-0.125f, -0.125f, -0.125f, 0.0625f, 0.0625f),
            (0.125f, -0.125f, -0.125f, 0.125f, 0.0625f),
            (0.125f, 0.125f, -0.125f, 0.1875f, 0.0625f),
            (-0.125f, 0.125f, -0.125f, 0.125f, 0.0625f),
            (-0.125f, 0.125f, 0.125f, 0.125f, 0f),
            (0.125f, 0.125f, 0.125f, 0.1875f, 0f),
            (-0.125f, -0.125f, -0.125f, 0.0625f, 0.0625f),
            (-0.125f, -0.125f, 0.125f, 0f, 0.0625f),
            (-0.125f, 0.125f, 0.125f, 0f, 0.125f),
            (-0.125f, 0.125f, -0.125f, 0.0625f, 0.125f),
            (0.125f, -0.125f, -0.125f, 0.125f, 0.0625f),
            (-0.125f, -0.125f, -0.125f, 0.0625f, 0.0625f),
            (-0.125f, 0.125f, -0.125f, 0.0625f, 0.125f),
            (0.125f, 0.125f, -0.125f, 0.125f, 0.125f),
            (0.125f, -0.125f, 0.125f, 0.1875f, 0.0625f),
            (0.125f, -0.125f, -0.125f, 0.125f, 0.0625f),
            (0.125f, 0.125f, -0.125f, 0.125f, 0.125f),
            (0.125f, 0.125f, 0.125f, 0.1875f, 0.125f),
            (-0.125f, -0.125f, 0.125f, 0.25f, 0.0625f),
            (0.125f, -0.125f, 0.125f, 0.1875f, 0.0625f),
            (0.125f, 0.125f, 0.125f, 0.1875f, 0.125f),
            (-0.125f, 0.125f, 0.125f, 0.25f, 0.125f)
        };

        Assert.Equal(expected.Length, vertices.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].X, vertices[index].X, 4);
            Assert.Equal(expected[index].Y, vertices[index].Y, 4);
            Assert.Equal(expected[index].Z, vertices[index].Z, 4);
            Assert.True(
                MathF.Abs(expected[index].U - vertices[index].U) <= 0.0001f
                    && MathF.Abs(expected[index].V - vertices[index].V) <= 0.0001f,
                $"raw box vertex {index}: uv ({vertices[index].U:F4},{vertices[index].V:F4}) expected ({expected[index].U:F4},{expected[index].V:F4})");
        }
    }

    [Fact]
    public void RawRootPoseUsesLiteralSourceRotation()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "box",
            VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("box", 0, 0, -2f, -2f, -2f, 4f, 4f, 4f, 0f));
        var definition = EntityModelDefinition.FromTree(
            "box",
            64,
            64,
            new VanillaMeshDefinition(root),
            texturePath: "probe/box",
            baselinePx: 0f,
            rawSourceFrame: true,
            applyRootPose: true);
        var pose = new EntityPose();
        pose.Set("root", new PoseOffset(Vector3.Zero, new Vector3(0.25f, 0.5f, 0.75f), Vector3.Zero));
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/box"] = new TextureData(64, 64, new byte[64 * 64 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 180f, Pitch: 0f),
            atlas.Get("probe/box")!,
            64,
            64,
            pose: pose,
            modelScale: 1f);

        var rawFirst = new Vector3(0.125f, -0.125f, 0.125f);
        var expected = Vector3.Transform(
            rawFirst,
            Matrix4x4.CreateRotationX(0.25f)
            * Matrix4x4.CreateRotationY(0.5f)
            * Matrix4x4.CreateRotationZ(0.75f));
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);

        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    [Fact]
    public void RawSourceFrameRejectsAModelHeightBaseline()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "box",
            VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("box", 0, 0, -2f, -2f, -2f, 4f, 4f, 4f, 0f));

        Assert.Throws<ArgumentException>(() => EntityModelDefinition.FromTree(
            "box",
            64,
            64,
            new VanillaMeshDefinition(root),
            rawSourceFrame: true));
    }

    private static DynamicGeometry BuildBox(bool rawSourceFrame = false, float yaw = 0f)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "box",
            VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("box", 0, 0, -2f, -2f, -2f, 4f, 4f, 4f, 0f));

        var definition = EntityModelDefinition.FromTree(
            "box", 64, 64, new VanillaMeshDefinition(root), texturePath: "probe/box", baselinePx: 0f, rawSourceFrame: rawSourceFrame);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/box"] = new TextureData(64, 64, new byte[64 * 64 * 4])
        });

        return new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: yaw, Pitch: 0f),
            atlas.Get("probe/box")!,
            64,
            64,
            modelScale: 1f);
    }
}
