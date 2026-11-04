// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Turtle animation coverage ported from <c>TurtleModel.setupAnim</c> (which runs <c>QuadrupedModel.setupAnim</c> first).
/// Pins the head pitch, the quadruped leg xRot walk swing, the land leg yRot flipper splay, the swim leg xRot/zRot, the
/// isOnLand selection, and the part-name contract. The laying-egg sub-branch is not derivable and is not faked.
/// </summary>
public sealed class TurtleAnimationTests
{
    private static readonly int TypeId = TurtleModelDefinition.TurtleEntityTypeId;

    private const float Deg = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float pos = 0f,
        float speed = 0f,
        float pitch = 0f,
        bool onGround = false,
        bool inWater = false) =>
        new(
            TypeId,
            0f,
            pos,
            speed,
            pitch,
            0f,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            IsOnGround: onGround,
            IsInWater: inWater);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static float RightHindX(float pos, float speed) => MathF.Cos(pos * 0.6662f) * 1.4f * speed;
    private static float LeftHindX(float pos, float speed) => MathF.Cos((pos * 0.6662f) + MathF.PI) * 1.4f * speed;

    [Fact]
    public void ProviderExposesClipsAndItsTypeId()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        Assert.Equal(141, provider.TypeId);
        Assert.Equal(new[] { "walk_land", "swim" }, provider.Clips);
    }

    [Fact]
    public void HeadLooksAlongThePitch()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 30f, onGround: true));

        Assert.Equal(30f * Deg, pose.Get("head").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Y, 6);
    }

    [Fact]
    public void LandBranchAddsTheFlipperSplayAndKeepsTheQuadrupedWalkSwing()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 1f, speed: 0.5f, onGround: true, inWater: false));

        var swing = MathF.Cos(1f * 5f); // 0.2836622
        var front = swing * 8f * 0.5f;
        var hind = swing * 3f * 0.5f;

        Assert.Equal(-front, pose.Get("right_front_leg").Rotation.Y, 4);
        Assert.Equal(front, pose.Get("left_front_leg").Rotation.Y, 4);
        Assert.Equal(-hind, pose.Get("right_hind_leg").Rotation.Y, 4);
        Assert.Equal(hind, pose.Get("left_hind_leg").Rotation.Y, 4);

        // Quadruped base xRot walk swing is preserved on land.
        Assert.Equal(RightHindX(1f, 0.5f), pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(LeftHindX(1f, 0.5f), pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(LeftHindX(1f, 0.5f), pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(RightHindX(1f, 0.5f), pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void SwimBranchReplacesHindXRotAndAddsFrontZRot()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 1f, speed: 0.5f, onGround: false, inWater: true));

        var swing = MathF.Cos(1f * 0.6662f * 0.6f) * 0.5f * 0.5f;

        Assert.Equal(swing, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-swing, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-swing, pose.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(swing, pose.Get("left_front_leg").Rotation.Z, 4);

        // Front legs keep the quadruped xRot walk swing; no flipper yRot in water.
        Assert.Equal(LeftHindX(1f, 0.5f), pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(RightHindX(1f, 0.5f), pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("right_front_leg").Rotation.Y, 6);
        Assert.Equal(0f, pose.Get("right_hind_leg").Rotation.Z, 6);
    }

    [Fact]
    public void IsOnLandSelectsTheLandOrSwimBranch()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        var state = provider.CreateState();

        // Grounded and dry → land (yRot flipper splay present).
        var land = Apply(state, provider, Frame(pos: 1f, speed: 0.5f, onGround: true, inWater: false));
        Assert.NotEqual(0f, land.Get("right_front_leg").Rotation.Y);

        // Airborne (not grounded), dry → swim.
        var airborne = Apply(state, provider, Frame(pos: 1f, speed: 0.5f, onGround: false, inWater: false));
        Assert.Equal(0f, airborne.Get("right_front_leg").Rotation.Y, 6);
        Assert.NotEqual(0f, airborne.Get("right_front_leg").Rotation.Z);

        // Grounded but in water → swim.
        var wet = Apply(state, provider, Frame(pos: 1f, speed: 0.5f, onGround: true, inWater: true));
        Assert.Equal(0f, wet.Get("right_front_leg").Rotation.Y, 6);
        Assert.NotEqual(0f, wet.Get("right_front_leg").Rotation.Z);
    }

    [Fact]
    public void ZeroSpeedRestsAllLegs()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 7f, speed: 0f, onGround: true));

        Assert.Equal(0f, pose.Get("right_front_leg").Rotation.X, 6);
        Assert.Equal(0f, pose.Get("right_front_leg").Rotation.Y, 6);
        Assert.Equal(0f, pose.Get("right_hind_leg").Rotation.X, 6);
        Assert.Equal(0f, pose.Get("left_hind_leg").Rotation.Y, 6);
    }

    [Fact]
    public void ApplyClipReconstructsWalkLandAndSwim()
    {
        var provider = new TurtleAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 5f);

        var land = new EntityPose();
        provider.ApplyClip(state, frame, "walk_land", 0.1f, land); // pos 1, speed 1
        Assert.Equal(MathF.Cos(1f * 5f) * 8f, land.Get("left_front_leg").Rotation.Y, 4);

        var swim = new EntityPose();
        provider.ApplyClip(state, frame, "swim", 0.1f, swim);
        Assert.Equal(MathF.Cos(1f * 0.6662f * 0.6f) * 0.5f, swim.Get("right_hind_leg").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheTurtleModelAndEggBellyIsNotFaked()
    {
        var modelParts = TurtleModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var part in new[] { "head", "right_front_leg", "left_front_leg", "right_hind_leg", "left_hind_leg" })
        {
            Assert.Contains(part, modelParts);
        }

        var provider = new TurtleAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 1f, speed: 0.5f, onGround: true));
        // The laying-egg branch (egg_belly visibility / bigger swing) is blocked; nothing is faked for it.
        Assert.False(pose.Offsets.ContainsKey("egg_belly"));
    }
}
