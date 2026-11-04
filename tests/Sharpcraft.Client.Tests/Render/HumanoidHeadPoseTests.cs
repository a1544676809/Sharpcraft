// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Logical equivalence of the humanoid head pose with the reference:
/// <c>head.xRot = state.xRot</c> (pitch, radians) and <c>head.yRot = wrapDegrees(headRot - bodyRot)</c>.
/// Combined with the entity root yaw (180 - bodyRot) and BodyRotationControl this fully determines head facing.
/// </summary>
public sealed class HumanoidHeadPoseTests
{
    private static readonly int[] HumanoidTypes =
    [
        HumanoidMobModelDefinition.ZombieEntityTypeId,
        HumanoidMobModelDefinition.HuskEntityTypeId,
        HumanoidMobModelDefinition.DrownedEntityTypeId,
        HumanoidMobModelDefinition.SkeletonEntityTypeId,
        HumanoidMobModelDefinition.StrayEntityTypeId,
        HumanoidMobModelDefinition.BoggedEntityTypeId,
        HumanoidMobModelDefinition.WitherSkeletonEntityTypeId,
        GiantModelDefinition.GiantEntityTypeId
    ];

    private static EntityAnimationFrame Frame(int typeId, float bodyYaw, float headYaw, float pitch) => new(
        TypeId: typeId, AgeInTicks: 7.5f, WalkAnimationPos: 0.3f, WalkAnimationSpeed: 0.5f,
        Pitch: pitch, Yaw: bodyYaw, Flap: 0f, FlapSpeed: 0f, TentacleAngle: 0f,
        IsResting: false, IsBaby: false, IsHidingInShell: false, HeadYaw: headYaw);

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(30f, 60f, 30f)]     // body 30, head 60 -> +30 relative
    [InlineData(200f, 10f, 10f)]    // head yaw wraps across 0
    [InlineData(350f, 10f, -20f)]   // +20 relative across the wrap
    [InlineData(45f, 45f, 80f)]     // aligned; steep pitch
    public void HeadPoseMatchesTheReferenceForEveryHumanoid(float bodyYaw, float headYaw, float pitch)
    {
        foreach (var typeId in HumanoidTypes)
        {
            var provider = EntityAnimationRegistry.For(typeId);
            Assert.NotNull(provider);

            var pose = new EntityPose();
            var frame = Frame(typeId, bodyYaw, headYaw, pitch);
            provider!.Apply(provider.CreateState(), in frame, pose);

            var head = pose.Get("head").Rotation;
            Assert.Equal(pitch * (MathF.PI / 180f), head.X, 4);
            Assert.Equal(Wrap180(headYaw - bodyYaw) * (MathF.PI / 180f), head.Y, 4);
            Assert.Equal(0f, head.Z, 4);
        }
    }

    private static float Wrap180(float degrees)
    {
        var value = degrees % 360f;
        if (value >= 180f)
        {
            value -= 360f;
        }

        if (value < -180f)
        {
            value += 360f;
        }

        return value;
    }
}
