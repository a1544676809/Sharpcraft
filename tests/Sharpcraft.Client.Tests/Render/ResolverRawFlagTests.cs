// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Production resolver regression for raw humanoid held-item integration: equipment decoration must not
/// silently drop the raw source-frame flags. On a raw-humanoid branch the Zombie/Skeleton/Player entries
/// are raw; on the pre-migration baseline they are legacy, so the test compares against each base definition
/// rather than hard-coding the current migration state.
/// </summary>
public sealed class ResolverRawFlagTests
{
    public static TheoryData<int, string> HumanoidTypes => new()
    {
        { HumanoidMobModelDefinition.ZombieEntityTypeId, "zombie" },
        { HumanoidMobModelDefinition.SkeletonEntityTypeId, "skeleton" },
        { EntityPlaceholderMesher.PlayerEntityTypeId, "player" }
    };

    [Theory]
    [MemberData(nameof(HumanoidTypes))]
    public void DecorationPreservesHumanoidRawSourceFrameFlags(int typeId, string name)
    {
        var baseDefinition = typeId == EntityPlaceholderMesher.PlayerEntityTypeId
            ? PlayerModelDefinition.Definition
            : typeId == HumanoidMobModelDefinition.ZombieEntityTypeId
                ? HumanoidMobModelDefinition.Zombie
                : HumanoidMobModelDefinition.Skeleton;
        var resolved = EntityModelResolver.Resolve(typeId);

        Assert.Equal(baseDefinition.RawSourceFrame, resolved.RawSourceFrame);
        Assert.Equal(baseDefinition.BaselinePx, resolved.BaselinePx);
        Assert.Equal(baseDefinition.SourceFrameBaselinePx, resolved.SourceFrameBaselinePx);
        Assert.Equal(baseDefinition.Scale, resolved.Scale);

        if (baseDefinition.RawSourceFrame)
        {
            // On the raw-humanoid branch this is the production assertion that was missing before the
            // resolver fix; on the pre-migration baseline it is vacuous by construction.
            Assert.True(resolved.RawSourceFrame, $"{name} decoration dropped RawSourceFrame");
            var hand = EntityModelMesher.LegacyHandWorldMatrix(
                resolved,
                new Sharpcraft.Client.World.EntityRenderState(1, typeId, 0f, 0f, 0f, 0f, 0f),
                "right_arm");
            Assert.NotEqual(default, hand);
        }
    }

    [Fact]
    public void RawCopperGolemDecorationKeepsRawFrame()
    {
        var baseDefinition = CopperGolemModelDefinition.Definition;
        Assert.True(baseDefinition.RawSourceFrame);
        var resolved = EntityModelResolver.Resolve(CopperGolemModelDefinition.CopperGolemEntityTypeId);
        Assert.True(resolved.RawSourceFrame);
        Assert.Equal(baseDefinition.BaselinePx, resolved.BaselinePx);
        Assert.Equal(baseDefinition.SourceFrameBaselinePx, resolved.SourceFrameBaselinePx);
    }
}
