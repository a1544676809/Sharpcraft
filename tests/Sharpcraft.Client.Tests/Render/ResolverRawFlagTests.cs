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

    [Theory]
    [InlineData(HumanoidMobModelDefinition.ZombieEntityTypeId, "ZombieMesh")]
    [InlineData(HumanoidMobModelDefinition.SkeletonEntityTypeId, "SkeletonMesh")]
    public void RawHumanoidDecorationKeepsRawFrameForProductionResolver(int typeId, string meshField)
    {
        // The current main branch still carries legacy HumanoidMob definitions; this feeds a raw
        // definition through the real private production Decorate path so the assertion is not
        // vacuous while the raw-humanoid migration branch is being prepared.
        var mesh = (VanillaMeshDefinition)typeof(HumanoidMobModelDefinition)
            .GetField(meshField, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        var raw = EntityModelDefinition.FromTree(
            "raw_decoration", 64, 64, mesh, texturePath: "probe/raw_decoration", baselinePx: 0f, rawSourceFrame: true);
        var decorate = typeof(EntityModelResolver).GetMethod(
            "Decorate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var decorated = (EntityModelDefinition)decorate.Invoke(null, [typeId, false, raw])!;

        Assert.True(decorated.RawSourceFrame);
        Assert.Equal(0f, decorated.BaselinePx);
        Assert.Equal(raw.SourceFrameBaselinePx, decorated.SourceFrameBaselinePx);
        var hand = EntityModelMesher.LegacyHandWorldMatrix(
            decorated,
            new Sharpcraft.Client.World.EntityRenderState(1, typeId, 0f, 0f, 0f, 0f, 0f),
            "right_arm");
        Assert.NotEqual(default(System.Numerics.Matrix4x4), hand);

        // The body stays raw, but independent equipment/elytra/armor layers are still legacy models
        // with their own legacy face calibration and 24 px baseline; inheriting the body's raw flag or
        // null FaceTransforms is what broke elytra UV tiles and spear placement.
        var elytra = Assert.Single(decorated.RenderLayers!, layer => layer.Name == "elytra").Model;
        Assert.NotNull(elytra);
        Assert.False(elytra.RawSourceFrame);
        Assert.NotNull(elytra.FaceTransforms);
        Assert.Equal(24f, elytra.BaselinePx);
        var armor = decorated.RenderLayers!.First(layer => layer.Name.StartsWith("equipment_", StringComparison.Ordinal)).Model;
        Assert.NotNull(armor);
        Assert.False(armor.RawSourceFrame);
        Assert.NotNull(armor.FaceTransforms);
        Assert.Equal(24f, armor.BaselinePx);
    }
}
