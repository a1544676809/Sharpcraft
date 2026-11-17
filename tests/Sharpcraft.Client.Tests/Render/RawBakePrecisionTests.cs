// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Precision regression for the raw source-frame bake: source pivot/centre floats must survive as-is.
/// The old raw branch rounded cube centres and child pivots to four decimals, which turned values such
/// as 16.692228 into 16.6922 and produced 1e-5..5e-5 block errors after rotation.
/// </summary>
/// <remarks>
/// Source formula: <c>R * (v/16) + pivot/16</c> for a root part; the expected values below are
/// computed directly from the source cube vertices and pivot, not from the production bake.
/// </remarks>
public sealed class RawBakePrecisionTests
{
    // Source Wither tail part: pivot (-2, 6.9 + cos(0.20420352)*10, -0.5 + sin(0.20420352)*10),
    // xRot 0.83252203, cube (0,0,0, 3,6,3). 16.692228 is the value the old bake turned into 16.6922.
    private const float PivotX = -2f;
    private const float PivotY = 16.692228f;
    private const float PivotZ = 1.527873f;
    private const float RotationX = 0.83252203f;
    private const float RotationY = 0f;
    private const float RotationZ = 0f;

    private const float MinX = 0f;
    private const float MinY = 0f;
    private const float MinZ = 0f;
    private const float Width = 3f;
    private const float Height = 6f;
    private const float Depth = 3f;

    // Source cube corners in the reference polygon order.
    private static readonly Vector3[] LocalVertices =
    [
        new(MinX + Width, MinY, MinZ + Depth), new(MinX, MinY, MinZ + Depth),
        new(MinX, MinY, MinZ), new(MinX + Width, MinY, MinZ),
        new(MinX + Width, MinY + Height, MinZ), new(MinX, MinY + Height, MinZ),
        new(MinX, MinY + Height, MinZ + Depth), new(MinX + Width, MinY + Height, MinZ + Depth),
        new(MinX, MinY, MinZ), new(MinX, MinY, MinZ + Depth),
        new(MinX, MinY + Height, MinZ + Depth), new(MinX, MinY + Height, MinZ),
        new(MinX + Width, MinY, MinZ), new(MinX, MinY, MinZ),
        new(MinX, MinY + Height, MinZ), new(MinX + Width, MinY + Height, MinZ),
        new(MinX + Width, MinY, MinZ + Depth), new(MinX + Width, MinY, MinZ),
        new(MinX + Width, MinY + Height, MinZ), new(MinX + Width, MinY + Height, MinZ + Depth),
        new(MinX, MinY, MinZ + Depth), new(MinX + Width, MinY, MinZ + Depth),
        new(MinX + Width, MinY + Height, MinZ + Depth), new(MinX, MinY + Height, MinZ + Depth)
    ];

    [Fact]
    public void RawBakedRootKeepsSourcePrecisionThroughRotationAndTranslation()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "cube",
            VanillaPartPose.OffsetAndRotation(PivotX, PivotY, PivotZ, RotationX, RotationY, RotationZ),
            new VanillaCubeDefinition("cube", 0, 0, MinX, MinY, MinZ, Width, Height, Depth, MirrorX: false));
        var definition = EntityModelDefinition.FromTree(
            "raw_precision",
            64,
            64,
            new VanillaMeshDefinition(root),
            texturePath: "probe/raw_precision",
            baselinePx: 0f,
            rawSourceFrame: true);
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/raw_precision"] = new TextureData(64, 64, new byte[64 * 64 * 4])
        });
        var geometry = new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 180f, Pitch: 0f),
            atlas.Get("probe/raw_precision")!,
            64,
            64,
            modelScale: 1f);

        var rotation = Matrix4x4.CreateRotationX(RotationX)
            * Matrix4x4.CreateRotationY(RotationY)
            * Matrix4x4.CreateRotationZ(RotationZ);
        var pivotBlocks = new Vector3(PivotX, PivotY, PivotZ) / 16f;
        for (var i = 0; i < LocalVertices.Length; i++)
        {
            var expected = Vector3.Transform(LocalVertices[i], rotation) / 16f + pivotBlocks;
            var actual = VertexFieldReader.Position(geometry.Vertices, i);
            Assert.True(
                MathF.Abs(expected.X - actual.X) <= 1e-5f
                    && MathF.Abs(expected.Y - actual.Y) <= 1e-5f
                    && MathF.Abs(expected.Z - actual.Z) <= 1e-5f,
                $"raw vertex {i}: expected ({expected.X:F8},{expected.Y:F8},{expected.Z:F8})"
                    + $" actual ({actual.X:F8},{actual.Y:F8},{actual.Z:F8})");
        }
    }

    [Fact]
    public void RawBakeDoesNotRoundCentreOrChildPivot()
    {
        var root = VanillaMeshBake.BakeRoot(
            "cube",
            PivotX,
            PivotY,
            PivotZ,
            MinX,
            MinY,
            MinZ,
            Width,
            Height,
            Depth,
            0,
            0,
            rawSourceFrame: true);
        AssertPrecise(PivotX + MinX + (Width * 0.5f), root.CentreX);
        AssertPrecise(PivotY + MinY, root.Bottom);
        AssertPrecise(PivotZ + MinZ + (Depth * 0.5f), root.CentreZ);

        var child = VanillaMeshBake.BakeChild(
            "child",
            "root",
            PivotX,
            PivotY,
            PivotZ,
            MinX,
            MinY,
            MinZ,
            Width,
            Height,
            Depth,
            0,
            0,
            rawSourceFrame: true);
        AssertPrecise(MinX + (Width * 0.5f), child.CentreX);
        AssertPrecise(MinY, child.Bottom);
        AssertPrecise(MinZ + (Depth * 0.5f), child.CentreZ);
        AssertPrecise(PivotX, child.PivotX);
        AssertPrecise(PivotY, child.PivotY);
        AssertPrecise(PivotZ, child.PivotZ);
    }

    private static void AssertPrecise(float expected, float actual) =>
        Assert.True(
            MathF.Abs(expected - actual) <= 1e-5f,
            $"expected {expected:F8}, actual {actual:F8}, delta {MathF.Abs(expected - actual):E3}");
}
