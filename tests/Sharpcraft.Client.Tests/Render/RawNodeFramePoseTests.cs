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
    private static readonly Vector3 LocalFirstVertex = new(5f, -4f, 0.99f);
    private static readonly Vector3 SourceProbeFirstVertex = new(-0.571491f, -4f, 5.057505f);

    [Fact]
    public void ProductionWorldMatchesTheNormalizedSourceProbe()
    {
        var geometry = Build();

        // Source probe is mixed: R * v_pixels + T_blocks. Production uses px on the rotated vertex
        // and on the translation, i.e. R * (v/16) + T_blocks; normalise only the rotated term.
        var translation = new Vector3(PositionX / 16f, 0f, 0f);
        var normalized = (SourceProbeFirstVertex - translation) / 16f + translation;
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);

        Assert.Equal(-0.571491f, SourceProbeFirstVertex.X, 4);
        Assert.Equal(normalized.X, actual.X, 4);
        Assert.Equal(normalized.Y, actual.Y, 4);
        Assert.Equal(normalized.Z, actual.Z, 4);
    }

    [Fact]
    public void PositionAndRotationUseSourceTranslateThenRotate()
    {
        var geometry = Build();
        var source = (Vector3.Transform(LocalFirstVertex, Matrix4x4.CreateRotationY(RotationY))
            + new Vector3(PositionX, 0f, 0f)) / 16f;
        var actual = VertexFieldReader.Position(geometry.Vertices, 0);

        Assert.Equal(source.X, actual.X, 4);
        Assert.Equal(source.Y, actual.Y, 4);
        Assert.Equal(source.Z, actual.Z, 4);
    }

    private static DynamicGeometry Build()
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
}
