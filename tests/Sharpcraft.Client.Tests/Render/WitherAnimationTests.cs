// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Wither <c>setupAnim</c> feasibility: the age-driven ribcage/tail bob and the centre-head look are derivable and are
/// pinned here; the per-head aim arrays are blocked and stay at the bind pose.
/// </summary>
public sealed class WitherAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float age = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float headYaw = 0f,
        float partialTick = 0f,
        float headYaw1 = 0f,
        float headPitch1 = 0f,
        float headYaw2 = 0f,
        float headPitch2 = 0f) => new(
        TypeId: WitherBossModelDefinition.WitherEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        PartialTick: partialTick,
        HeadYaw: headYaw,
        WitherHeadYaw1: headYaw1,
        WitherHeadPitch1: headPitch1,
        WitherHeadYaw2: headYaw2,
        WitherHeadPitch2: headPitch2);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new WitherAnimationProvider(WitherBossModelDefinition.WitherEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTheIdleClipAndTypeId()
    {
        var provider = new WitherAnimationProvider(WitherBossModelDefinition.WitherEntityTypeId);
        Assert.Equal(WitherBossModelDefinition.WitherEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void RibcageAndTailBobAtAgeZero()
    {
        // anim = cos(0) = 1: ribcage delta = 0.05π, tail delta = 0.1π, and setPos moves by the reference alpha.
        var pose = Apply(Frame(age: 0f));

        Assert.Equal(0.15707963f, pose.Get("ribcage").Rotation.X, 5);
        Assert.Equal(0.31415929f, pose.Get("tail").Rotation.X, 5);
        Assert.Equal(0.43778780f, pose.Get("tail").Position.Y, 5);
        Assert.Equal(1.50687551f, pose.Get("tail").Position.Z, 5);
        Assert.Equal(0f, pose.Get("tail").Position.X, 5);
    }

    [Fact]
    public void RibcageAndTailBobAtHalfCycle()
    {
        // age·0.1 = π ⇒ anim = cos = -1: the deltas flip sign and the tail setPos follows the lower ribcage angle.
        var pose = Apply(Frame(age: 10f * MathF.PI));

        Assert.Equal(-0.15707963f, pose.Get("ribcage").Rotation.X, 5);
        Assert.Equal(-0.31415924f, pose.Get("tail").Rotation.X, 5);
        Assert.Equal(-0.19667064f, pose.Get("tail").Position.Y, 5);
        Assert.Equal(-1.55680842f, pose.Get("tail").Position.Z, 5);
    }

    [Fact]
    public void CenterHeadLooksAlongPitchAndRelativeHeadYaw()
    {
        // state.yRot = headRot - bodyRot = 50 - 20 = 30 deg; state.xRot = pitch = 10 deg.
        var pose = Apply(Frame(age: 0f, pitch: 10f, yaw: 20f, headYaw: 50f));

        Assert.Equal(30f * ToRad, pose.Get("center_head").Rotation.Y, 5);
        Assert.Equal(10f * ToRad, pose.Get("center_head").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("center_head").Rotation.Z, 5);
    }

    [Fact]
    public void CenterHeadWrapsTheRelativeHeadYaw()
    {
        // 350 - 10 = 340 deg, wrapped to -20 deg.
        var pose = Apply(Frame(age: 0f, pitch: 0f, yaw: 10f, headYaw: 350f));

        Assert.Equal(-20f * ToRad, pose.Get("center_head").Rotation.Y, 5);
    }

    [Fact]
    public void CenterHeadIsNeutralWhenHeadYawMatchesBodyYawAndPitchIsZero()
    {
        var pose = Apply(Frame(age: 3f, pitch: 0f, yaw: 42f, headYaw: 42f));

        Assert.Equal(Vector3.Zero, pose.Get("center_head").Rotation);
    }

    [Fact]
    public void SideHeadsUseTheDerivedAimRotations()
    {
        // Side-head yaw is relative to the body: (headYawN - yaw); pitch is absolute.
        var pose = Apply(Frame(age: 7f, pitch: 15f, yaw: -30f, headYaw: 60f,
            headYaw1: 40f, headPitch1: 15f, headYaw2: -70f, headPitch2: -5f));

        Assert.Equal(15f * ToRad, pose.Get("right_head").Rotation.X, 5);
        Assert.Equal(70f * ToRad, pose.Get("right_head").Rotation.Y, 5);
        Assert.Equal(-5f * ToRad, pose.Get("left_head").Rotation.X, 5);
        Assert.Equal(-40f * ToRad, pose.Get("left_head").Rotation.Y, 5);
    }

    [Fact]
    public void ApplyClipIdleUsesTwentyTicksPerSecond()
    {
        var provider = new WitherAnimationProvider(WitherBossModelDefinition.WitherEntityTypeId);
        var state = provider.CreateState();

        var direct = new EntityPose();
        provider.Apply(state, Frame(age: 40f), direct);

        var fromClip = new EntityPose();
        provider.ApplyClip(state, Frame(age: 0f), "idle", 2f, fromClip);

        Assert.Equal(direct.Get("ribcage").Rotation.X, fromClip.Get("ribcage").Rotation.X, 5);
        Assert.Equal(direct.Get("tail").Position.Y, fromClip.Get("tail").Position.Y, 5);
    }

    [Fact]
    public void PartialTickAdvancesTheAgeClock()
    {
        // AgeInTicks + PartialTick drives the cosine; a 0.5-tick partial must differ from the integer age alone.
        var whole = Apply(Frame(age: 5f, partialTick: 0f));
        var partial = Apply(Frame(age: 5f, partialTick: 0.5f));

        Assert.NotEqual(whole.Get("ribcage").Rotation.X, partial.Get("ribcage").Rotation.X);
    }
}
