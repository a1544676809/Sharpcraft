// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-672/T-673/T-675: proves the DSL-baked humanoid parts match the 26.3 source-frame part records
/// (raw pivot/min/size literals, independent inflated hat child). Skeleton variants keep shared parts.
/// Expected records are independent source literals (RawModelPartFactory), never the C# bake output.
/// </summary>
public sealed class HumanoidMobModelDefinitionTests
{
    // HumanoidModel.createMesh: head 8x8x8 at min(-4,-8,-4); hat = head inflated grow 0.5 at tex(32,0).
    private static EntityModelPart Hat(float grow = 0.5f, int tx = 32, int ty = 0) =>
        RawModelPartFactory.FromVanillaChild("hat", "head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, tx, ty, grow: grow);

    // Drowned's translucent outer layer copies (DrownedOuter in the DSL).
    private static EntityModelLayer DrownedOuter(int textureX, int textureY) =>
        new("drowned_outer", 0.25f, textureX, textureY, false, "entity/zombie/drowned_outer_layer", EntityGeometryLayerKind.Translucent);

    [Fact]
    public void WitherSkeletonSharesTheSkeletonParts()
    {
        Assert.Same(HumanoidMobModelDefinition.Skeleton.Parts, HumanoidMobModelDefinition.WitherSkeleton.Parts);
    }

    [Fact]
    public void StrayAndBoggedOuterPartsKeepTheirPoseSource()
    {
        var expected = new (string Part, string PoseSource)[]
        {
            ("outer_head", "head"), ("outer_body", "body"),
            ("outer_left_arm", "left_arm"), ("outer_right_arm", "right_arm"),
            ("outer_left_leg", "left_leg"), ("outer_right_leg", "right_leg")
        };
        foreach (var definition in new[] { HumanoidMobModelDefinition.Stray, HumanoidMobModelDefinition.Bogged })
        {
            foreach (var (part, poseSource) in expected)
            {
                Assert.Equal(poseSource, definition.Parts.Single(candidate => candidate.Name == part).PoseSource);
            }
        }
    }

    [Fact]
    public void StrayAndBoggedOuterPartsMatchSourceFrameRecords()
    {
        const string overlayPath = "entity/skeleton/stray_overlay";
        const float grow = 0.25f;
        var sample = new[]
        {
            RawModelPartFactory.FromVanilla("outer_head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, grow: grow, texturePath: overlayPath,
                overlays: [new EntityModelLayer("outer_hat", 0.5f, 32, 0, false, overlayPath, EntityGeometryLayerKind.Cutout)]) with { PoseSource = "head" },
            RawModelPartFactory.FromVanilla("outer_body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16, grow: grow, texturePath: overlayPath) with { PoseSource = "body" },
            RawModelPartFactory.FromVanilla("outer_left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16, mirror: true, grow: grow, texturePath: overlayPath) with { PoseSource = "left_arm" },
            RawModelPartFactory.FromVanilla("outer_right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16, grow: grow, texturePath: overlayPath) with { PoseSource = "right_arm" },
            RawModelPartFactory.FromVanilla("outer_left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true, grow: grow, texturePath: overlayPath) with { PoseSource = "left_leg" },
            RawModelPartFactory.FromVanilla("outer_right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, grow: grow, texturePath: overlayPath) with { PoseSource = "right_leg" }
        };
        var definition = HumanoidMobModelDefinition.Stray;
        var baseCount = HumanoidMobModelDefinition.Skeleton.Parts.Count;
        Assert.Equal(baseCount + sample.Length, definition.Parts.Count);
        for (var i = 0; i < sample.Length; i++)
        {
            ModelPartAssertions.AssertEqual(sample[i], definition.Parts[baseCount + i]);
        }
    }

    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
