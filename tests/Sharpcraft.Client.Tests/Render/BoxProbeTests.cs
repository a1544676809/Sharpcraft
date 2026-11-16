// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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
/// by exactly 1.5 blocks on y, which is the <c>24</c> pixel model height the bake maps y-down through
/// (<c>yUp = 24 - yDown</c>). That lift is what makes a living entity stand on the ground, and it is
/// why every block entity renderer subtracts 24 by hand. Issue 101 tracks moving it out of the bake
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
                MathF.Round(geometry.Vertices[vertex + 1] - ModelHeightOffset, 3),
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

    private static DynamicGeometry BuildBox()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "box",
            VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("box", 0, 0, -2f, -2f, -2f, 4f, 4f, 4f, 0f));

        var definition = EntityModelDefinition.FromTree("box", 64, 64, new VanillaMeshDefinition(root), texturePath: "probe/box");
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["probe/box"] = new TextureData(64, 64, new byte[64 * 64 * 4])
        });

        return new EntityModelMesher().BuildModel(
            definition,
            new EntityRenderState(Id: 0, TypeId: 0, X: 0f, Y: 0f, Z: 0f, Yaw: 0f, Pitch: 0f),
            atlas.Get("probe/box")!,
            64,
            64,
            modelScale: 1f);
    }
}
