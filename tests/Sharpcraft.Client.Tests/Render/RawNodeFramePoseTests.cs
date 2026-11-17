// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Minimal raw source-frame pose regression: one source cube with a simultaneous PartPose position
/// and rotation, checked against the reference ModelPart.translateAndRotate formula.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java:162-171</c>
/// (translate(x/16,y/16,z/16), then rotationZYX(z,y,x), then scale). The reference <c>root.visit</c>
/// probe reports mixed units: cube vertices stay in pixels while the part translation is already
/// 1/16 block. Production raw output uses px = modelScale/16 for both, so the probe must be
/// normalised to <c>R * (v/16) + T_blocks</c> before comparison; it must not be divided as a whole.
/// </remarks>
public sealed class RawNodeFramePoseTests
{
    // Book right_pages, lectern openness=1.5 (BookRightPagesProbe.java):
    // part x=0.997491 (source pixels -> 1/16 block translation), yRot=-1.5,
    // cube local first vertex (5, -4, 0.99), source root.visit first vertex (-0.571491,-4,5.057505).
    private const float PositionX = 0.997491f;
    private const float RotationY = -1.5f;
    private const float TextureSize = 64f;
    private static readonly Vector3 SourceProbeFirstVertex = new(-0.571491f, -4f, 5.057505f);

    // Source Cube vertex order emitted by ModelPart.Cube for this single cube, then its source UV
    // intervals. This is the source polygon order, not a re-derivation from the mesher.
    private static readonly Vector3[] LocalVertices =
    [
        new(5f, -4f, 0.99f), new(0f, -4f, 0.99f), new(0f, -4f, -0.01f), new(5f, -4f, -0.01f),
        new(5f, 4f, -0.01f), new(0f, 4f, -0.01f), new(0f, 4f, 0.99f), new(5f, 4f, 0.99f),
        new(0f, -4f, -0.01f), new(0f, -4f, 0.99f), new(0f, 4f, 0.99f), new(0f, 4f, -0.01f),
        new(5f, -4f, -0.01f), new(0f, -4f, -0.01f), new(0f, 4f, -0.01f), new(5f, 4f, -0.01f),
        new(5f, -4f, 0.99f), new(5f, -4f, -0.01f), new(5f, 4f, -0.01f), new(5f, 4f, 0.99f),
        new(0f, -4f, 0.99f), new(5f, -4f, 0.99f), new(5f, 4f, 0.99f), new(0f, 4f, 0.99f)
    ];

    // Cube: texOffs(0,0), depth=1, width=5, height=8; source u0=0,u1=1,u2=6,u22=11,u3=7,u4=12;
    // v0=0,v1=1,v2=9. Polygon UV remap: (u1,v0),(u0,v0),(u0,v1),(u1,v1) for source args (u0,v0,u1,v1).
    private static readonly (float U, float V)[] SourceUvs =
    [
        (6f, 0f), (1f, 0f), (1f, 1f), (6f, 1f),
        (11f, 1f), (6f, 1f), (6f, 0f), (11f, 0f),
        (1f, 1f), (0f, 1f), (0f, 9f), (1f, 9f),
        (6f, 1f), (1f, 1f), (1f, 9f), (6f, 9f),
        (7f, 1f), (6f, 1f), (6f, 9f), (7f, 9f),
        (12f, 1f), (7f, 1f), (7f, 9f), (12f, 9f)
    ];

    [Fact]
    public void ProductionWorldMatchesTheNormalizedSourceProbeForAll24Vertices()
    {
        var geometry = BuildGeometry();

        Assert.Equal(24, geometry.VertexFloats / ChunkMesher.FloatsPerVertex);
        for (var i = 0; i < LocalVertices.Length; i++)
        {
            // Independent source formula: rotate vertex pixels, scale by 1/16, add block translation.
            var expected = Vector3.Transform(LocalVertices[i], Matrix4x4.CreateRotationY(RotationY)) / 16f
                + new Vector3(PositionX / 16f, 0f, 0f);
            var actual = VertexFieldReader.Position(geometry.Vertices, i);
            var uv = VertexFieldReader.Uv(geometry.Vertices, i);
            Assert.True(
                MathF.Abs(expected.X - actual.X) < 0.0001f
                    && MathF.Abs(expected.Y - actual.Y) < 0.0001f
                    && MathF.Abs(expected.Z - actual.Z) < 0.0001f,
                $"vertex {i}: expected ({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                    + $" actual ({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
            Assert.True(
                MathF.Abs(SourceUvs[i].U / TextureSize - uv.U) < 0.0001f
                    && MathF.Abs(SourceUvs[i].V / TextureSize - uv.V) < 0.0001f,
                $"vertex {i} UV: expected ({SourceUvs[i].U / TextureSize:F6},{SourceUvs[i].V / TextureSize:F6})"
                    + $" actual ({uv.U:F6},{uv.V:F6})");
        }

        // The hard-coded probe first vertex normalises to the same production value.
        var probeTranslation = new Vector3(PositionX / 16f, 0f, 0f);
        var normalizedProbe = (SourceProbeFirstVertex - probeTranslation) / 16f + probeTranslation;
        var first = VertexFieldReader.Position(geometry.Vertices, 0);
        Assert.Equal(normalizedProbe.X, first.X, 4);
        Assert.Equal(normalizedProbe.Y, first.Y, 4);
        Assert.Equal(normalizedProbe.Z, first.Z, 4);
    }

    [Fact]
    public void PartWorldMatrixPropagatesRawSourceFrameIntoWalk()
    {
        var (definition, pose) = BuildModel();
        var entity = new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 180f, Pitch: 0f);
        var matrix = EntityModelMesher.PartWorldMatrix(
            definition,
            entity,
            "cube",
            pose,
            applyRootPose: true,
            rawSourceFrame: true);

        // Same source formula as the geometry path, in this project's actual entity px
        // (ModelScale/16). PartWorldMatrix's frame applies to geometry-space block coordinates.
        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * definition.Scale;
        for (var i = 0; i < LocalVertices.Length; i++)
        {
            var localBlock = LocalVertices[i] * px;
            var expected = Vector3.Transform(LocalVertices[i], Matrix4x4.CreateRotationY(RotationY)) * px
                + new Vector3(PositionX * px, 0f, 0f);
            var actual = Vector3.Transform(localBlock, matrix);
            Assert.True(
                MathF.Abs(expected.X - actual.X) < 0.0001f
                    && MathF.Abs(expected.Y - actual.Y) < 0.0001f
                    && MathF.Abs(expected.Z - actual.Z) < 0.0001f,
                $"part vertex {i}: expected ({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                    + $" actual ({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
        }

        // A legacy frame uses the X/Y-conjugated pose and must not equal the raw frame here.
        var legacy = EntityModelMesher.PartWorldMatrix(
            definition,
            entity,
            "cube",
            pose,
            applyRootPose: true,
            rawSourceFrame: false);
        Assert.NotEqual(legacy, matrix);
    }

    [Fact]
    public void LegacyPartWorldMatrixKeepsLegacyConjugation()
    {
        var (definition, pose) = BuildModel();
        var entity = new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 180f, Pitch: 0f);
        var matrix = EntityModelMesher.PartWorldMatrix(
            definition,
            entity,
            "cube",
            pose,
            applyRootPose: true,
            rawSourceFrame: false);

        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * definition.Scale;
        for (var i = 0; i < LocalVertices.Length; i++)
        {
            var localBlock = LocalVertices[i] * px;
            // Legacy NodeFrame subtracts the pose Y rotation: 0 - (-1.5) = +1.5.
            var expected = Vector3.Transform(LocalVertices[i], Matrix4x4.CreateRotationY(-RotationY)) * px
                + new Vector3(PositionX * px, 0f, 0f);
            var actual = Vector3.Transform(localBlock, matrix);
            Assert.True(
                MathF.Abs(expected.X - actual.X) < 0.0001f
                    && MathF.Abs(expected.Y - actual.Y) < 0.0001f
                    && MathF.Abs(expected.Z - actual.Z) < 0.0001f,
                $"legacy part vertex {i}: expected ({expected.X:F6},{expected.Y:F6},{expected.Z:F6})"
                    + $" actual ({actual.X:F6},{actual.Y:F6},{actual.Z:F6})");
        }
    }

    private static DynamicGeometry BuildGeometry()
    {
        var (definition, pose) = BuildModel();
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/raw_pose"] = new TextureData(64, 64, new byte[64 * 64 * 4])
        });
        return new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 180f, Pitch: 0f),
            atlas.Get("probe/raw_pose")!,
            64,
            64,
            pose: pose,
            modelScale: 1f);
    }

    private static (EntityModelDefinition Definition, EntityPose Pose) BuildModel()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "cube",
            VanillaPartPose.Zero,
            new VanillaCubeDefinition("cube", 0, 0, 0f, -4f, -0.01f, 5f, 8f, 1f, MirrorX: false));
        var definition = EntityModelDefinition.FromTree(
            "raw_pose",
            64,
            64,
            new VanillaMeshDefinition(root),
            texturePath: "probe/raw_pose",
            baselinePx: 0f,
            rawSourceFrame: true);
        var pose = new EntityPose();
        pose.Set("cube", new PoseOffset(new Vector3(PositionX, 0f, 0f), new Vector3(0f, RotationY, 0f), Vector3.Zero));
        return (definition, pose);
    }
}
