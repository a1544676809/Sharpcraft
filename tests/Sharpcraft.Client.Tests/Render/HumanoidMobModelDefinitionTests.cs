// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-672/T-673/T-675: proves the DSL-baked humanoid parts are value-identical to the legacy FromVanilla records, and
/// that the skeleton variants that reuse Skeleton geometry keep their shared parts and clothing pose sources.
/// </summary>
public sealed class HumanoidMobModelDefinitionTests
{
    [Fact]
    public void ZombieAndSkeletonMatchLegacyFromVanillaRecords()
    {
        var expectedZombie = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
            LegacyModelPartFactory.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16)
        };
        var expectedSkeleton = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -1f, 2f, 12f, 2f, 40, 16, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -1f, -2f, -1f, 2f, 12f, 2f, 40, 16),
            LegacyModelPartFactory.FromVanilla("left_leg", 2f, 12f, 0f, -1f, 0f, -1f, 2f, 12f, 2f, 0, 16, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_leg", -2f, 12f, 0f, -1f, 0f, -1f, 2f, 12f, 2f, 0, 16)
        };

        AssertPartsMatch(expectedZombie, HumanoidMobModelDefinition.Zombie.Parts);
        AssertPartsMatch(expectedSkeleton, HumanoidMobModelDefinition.Skeleton.Parts);
    }

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
            ("outer_head", "head"),
            ("outer_body", "body"),
            ("outer_left_arm", "left_arm"),
            ("outer_right_arm", "right_arm"),
            ("outer_left_leg", "left_leg"),
            ("outer_right_leg", "right_leg")
        };

        foreach (var definition in new[] { HumanoidMobModelDefinition.Stray, HumanoidMobModelDefinition.Bogged })
        {
            foreach (var (part, poseSource) in expected)
            {
                var outer = definition.Parts.Single(candidate => candidate.Name == part);
                Assert.Equal(poseSource, outer.PoseSource);
            }
        }
    }

    /// <summary>
    /// T-673: proves the DSL-baked Enderman / Drowned / Husk parts (including the DrownedOuter overlay layers and the
    /// enderman's 25f model height) are identical to the legacy FromVanilla records.
    /// </summary>
    [Fact]
    public void EndermanDrownedHuskMatchLegacyFromVanillaRecords()
    {
        var expectedEnderman = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, -13f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                overlays: [new("hat", -0.5f, 0, 16)], modelHeight: 25f),
            LegacyModelPartFactory.FromVanilla("body", 0f, -14f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 32, 16, modelHeight: 25f),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, -12f, 0f, -1f, -2f, -1f, 2f, 30f, 2f, 56, 0, mirror: true, modelHeight: 25f),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, -12f, 0f, -1f, -2f, -1f, 2f, 30f, 2f, 56, 0, modelHeight: 25f),
            LegacyModelPartFactory.FromVanilla("left_leg", 2f, -5f, 0f, -1f, 0f, -1f, 2f, 30f, 2f, 56, 0, mirror: true, modelHeight: 25f),
            LegacyModelPartFactory.FromVanilla("right_leg", -2f, -5f, 0f, -1f, 0f, -1f, 2f, 30f, 2f, 56, 0, modelHeight: 25f)
        };
        var expectedDrowned = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                overlays: [new("hat", 0.5f, 32, 0), DrownedOuter(0, 0)]),
            LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16,
                overlays: [DrownedOuter(16, 16)]),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48,
                overlays: [DrownedOuter(32, 48)]),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                overlays: [DrownedOuter(40, 16)]),
            LegacyModelPartFactory.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48,
                overlays: [DrownedOuter(16, 48)]),
            LegacyModelPartFactory.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                overlays: [DrownedOuter(0, 16)])
        };
        var expectedHusk = new[]
        {
            LegacyModelPartFactory.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                overlays: [new("hat", 0.5f, 32, 0)]),
            LegacyModelPartFactory.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            LegacyModelPartFactory.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
            LegacyModelPartFactory.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            LegacyModelPartFactory.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16)
        };

        AssertPartsMatch(expectedEnderman, HumanoidMobModelDefinition.Enderman.Parts);
        AssertPartsMatch(expectedDrowned, HumanoidMobModelDefinition.Drowned.Parts);
        AssertPartsMatch(expectedHusk, HumanoidMobModelDefinition.Husk.Parts);
    }

    /// <summary>
    /// T-675: proves the DSL-baked Stray/Bogged clothing outer parts (grow, overlay texture path, PoseSource) are
    /// value-identical to the legacy FromVanilla records, and that the six names and pose links are preserved.
    /// </summary>
    [Fact]
    public void StrayAndBoggedOuterPartsMatchLegacyRecords()
    {
        AssertOuterParts(HumanoidMobModelDefinition.Stray, "entity/skeleton/stray_overlay", 0.25f);
        AssertOuterParts(HumanoidMobModelDefinition.Bogged, "entity/skeleton/bogged_overlay", 0.2f);
    }

    private static void AssertOuterParts(EntityModelDefinition definition, string overlayPath, float grow)
    {
        var expected = new[]
        {
            LegacyModelPartFactory.FromVanilla("outer_head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                grow: grow, texturePath: overlayPath,
                overlays: [new("outer_hat", 0.5f, 32, 0, false, overlayPath)]) with { PoseSource = "head" },
            LegacyModelPartFactory.FromVanilla("outer_body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16,
                grow: grow, texturePath: overlayPath) with { PoseSource = "body" },
            LegacyModelPartFactory.FromVanilla("outer_left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                mirror: true, grow: grow, texturePath: overlayPath) with { PoseSource = "left_arm" },
            LegacyModelPartFactory.FromVanilla("outer_right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                grow: grow, texturePath: overlayPath) with { PoseSource = "right_arm" },
            LegacyModelPartFactory.FromVanilla("outer_left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                mirror: true, grow: grow, texturePath: overlayPath) with { PoseSource = "left_leg" },
            LegacyModelPartFactory.FromVanilla("outer_right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                grow: grow, texturePath: overlayPath) with { PoseSource = "right_leg" }
        };

        var baseCount = HumanoidMobModelDefinition.Skeleton.Parts.Count;
        Assert.Equal(baseCount + expected.Length, definition.Parts.Count);
        for (var i = 0; i < baseCount; i++)
        {
            ModelPartAssertions.AssertEqual(HumanoidMobModelDefinition.Skeleton.Parts[i], definition.Parts[i]);
        }

        var names = new[] { "outer_head", "outer_body", "outer_left_arm", "outer_right_arm", "outer_left_leg", "outer_right_leg" };
        var poseSources = new[] { "head", "body", "left_arm", "right_arm", "left_leg", "right_leg" };
        for (var i = 0; i < expected.Length; i++)
        {
            var outer = definition.Parts[baseCount + i];
            ModelPartAssertions.AssertEqual(expected[i], outer);
            Assert.Equal(names[i], outer.Name);
            Assert.Equal(poseSources[i], outer.PoseSource);
        }
    }

    /// <summary>Mirror of the private DrownedOuter layer factory (same values) for the legacy expectations.</summary>
    private static EntityModelLayer DrownedOuter(int textureX, int textureY) =>
        new("drowned_outer", 0.25f, textureX, textureY, false, "entity/zombie/drowned_outer_layer", EntityGeometryLayerKind.Translucent);

    /// <summary>Value-wise array comparison via the shared oracle (record equality is not valid for overlay lists).</summary>
    private static void AssertPartsMatch(EntityModelPart[] expected, IReadOnlyList<EntityModelPart> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            ModelPartAssertions.AssertEqual(expected[i], actual[i]);
        }
    }
}
