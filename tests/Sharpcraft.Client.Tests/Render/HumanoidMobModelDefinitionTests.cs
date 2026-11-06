// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// T-672: proves the DSL-baked Zombie / Skeleton parts are value-identical to the legacy FromVanilla records, and that
/// the skeleton variants that reuse Skeleton geometry keep their shared parts and clothing pose sources.
/// </summary>
public sealed class HumanoidMobModelDefinitionTests
{
    [Fact]
    public void ZombieAndSkeletonMatchLegacyFromVanillaRecords()
    {
        var expectedZombie = new[]
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
            PlayerModelDefinition.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16)
        };
        var expectedSkeleton = new[]
        {
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -1f, 2f, 12f, 2f, 40, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -1f, -2f, -1f, 2f, 12f, 2f, 40, 16),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, 12f, 0f, -1f, 0f, -1f, 2f, 12f, 2f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 12f, 0f, -1f, 0f, -1f, 2f, 12f, 2f, 0, 16)
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
