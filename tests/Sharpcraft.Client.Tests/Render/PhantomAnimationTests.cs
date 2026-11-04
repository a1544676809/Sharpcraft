// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Phantom procedural animation ported from <c>PhantomModel.setupAnim</c> and <c>PhantomRenderer</c>. The flap clock is
/// <c>EntityId*3 + AgeInTicks</c>; <c>anim = flapTime * 7.448451°</c>; wing z-rotation = ±cos(anim)*16° (offset from the
/// ±0.1 bind) and tail x-rotation = -(5 + cos(2·anim)*5)°.
/// </summary>
public sealed class PhantomAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    /// <summary>Degrees-to-radians of one flap-time unit (7.448451°).</summary>
    private const float FlapRadPerTick = 7.448451f * ToRad;

    private static EntityAnimationFrame Frame(float age = 0f, int entityId = 0) => new(
        TypeId: PhantomModelDefinition.PhantomEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: 0f,
        Yaw: 0f,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        EntityId: entityId);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new PhantomAnimationProvider(PhantomModelDefinition.PhantomEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTheTwoClipsAndTypeId()
    {
        var provider = new PhantomAnimationProvider(PhantomModelDefinition.PhantomEntityTypeId);
        Assert.Equal(PhantomModelDefinition.PhantomEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "flap" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void WingsAndTailAtFlapTimeZero()
    {
        // flapTime = 0 -> anim = 0 -> cos = 1: wing z = 16°, tail x = -(5 + 1*5)° = -10°.
        var pose = Apply(Frame(age: 0f, entityId: 0));
        var wingZ = 16f * ToRad;
        var tailX = -10f * ToRad;

        Assert.Equal(wingZ - 0.1f, pose.Get("left_wing_base").Rotation.Z, 5);
        Assert.Equal(wingZ - 0.1f, pose.Get("left_wing_tip").Rotation.Z, 5);
        Assert.Equal(-wingZ + 0.1f, pose.Get("right_wing_base").Rotation.Z, 5);
        Assert.Equal(-wingZ + 0.1f, pose.Get("right_wing_tip").Rotation.Z, 5);

        Assert.Equal(tailX, pose.Get("tail_base").Rotation.X, 5);
        Assert.Equal(tailX, pose.Get("tail_tip").Rotation.X, 5);
    }

    [Fact]
    public void WingsFoldAtQuarterFlapAndInvertAtHalfFlap()
    {
        // anim = π/2 -> cos = 0 -> wing z = 0 (left offset -0.1, right +0.1); tail x = -(5 + cos(π)*5)° = 0.
        var quarterTime = (MathF.PI / 2f) / FlapRadPerTick;
        var quarter = Apply(Frame(age: quarterTime, entityId: 0));
        Assert.Equal(-0.1f, quarter.Get("left_wing_base").Rotation.Z, 4);
        Assert.Equal(0.1f, quarter.Get("right_wing_base").Rotation.Z, 4);
        Assert.Equal(0f, quarter.Get("tail_base").Rotation.X, 4);

        // anim = π -> cos = -1 -> wing z = -16°; tail x = -(5 + cos(2π)*5)° = -10°.
        var halfTime = MathF.PI / FlapRadPerTick;
        var half = Apply(Frame(age: halfTime, entityId: 0));
        Assert.Equal(-16f * ToRad - 0.1f, half.Get("left_wing_base").Rotation.Z, 4);
        Assert.Equal(16f * ToRad + 0.1f, half.Get("right_wing_base").Rotation.Z, 4);
        Assert.Equal(-10f * ToRad, half.Get("tail_base").Rotation.X, 4);
    }

    [Fact]
    public void RightWingsMirrorLeftWings()
    {
        var pose = Apply(Frame(age: 3.7f, entityId: 1));
        Assert.Equal(pose.Get("left_wing_base").Rotation.Z, -pose.Get("right_wing_base").Rotation.Z, 5);
        Assert.Equal(pose.Get("left_wing_tip").Rotation.Z, -pose.Get("right_wing_tip").Rotation.Z, 5);
        Assert.Equal(pose.Get("left_wing_base").Rotation.Z, pose.Get("left_wing_tip").Rotation.Z, 5);
    }

    [Fact]
    public void EntityIdOffsetsTheFlapClock()
    {
        // EntityId * 3 shifts flapTime: (id=2, age=0) must equal (id=0, age=6).
        var byId = Apply(Frame(age: 0f, entityId: 2));
        var byAge = Apply(Frame(age: 6f, entityId: 0));

        Assert.Equal(byAge.Get("left_wing_base").Rotation.Z, byId.Get("left_wing_base").Rotation.Z, 5);
        Assert.Equal(byAge.Get("right_wing_tip").Rotation.Z, byId.Get("right_wing_tip").Rotation.Z, 5);
        Assert.Equal(byAge.Get("tail_base").Rotation.X, byId.Get("tail_base").Rotation.X, 5);
    }

    [Fact]
    public void TailFollowsTheDoubleFrequencyCosine()
    {
        // anim = π/4 -> cos(2*anim) = cos(π/2) = 0 -> tail x = -5°.
        var time = (MathF.PI / 4f) / FlapRadPerTick;
        var pose = Apply(Frame(age: time, entityId: 0));
        Assert.Equal(-5f * ToRad, pose.Get("tail_base").Rotation.X, 4);
        Assert.Equal(-5f * ToRad, pose.Get("tail_tip").Rotation.X, 4);
    }

    [Fact]
    public void ApplyClipReconstructsFlapAndIdle()
    {
        var provider = new PhantomAnimationProvider(PhantomModelDefinition.PhantomEntityTypeId);
        var state = provider.CreateState();
        var pose = new EntityPose();

        // "flap" scrubs from seconds (ageInTicks = seconds * 20); seconds 0 -> flapTime 0.
        var flapFrame = Frame(age: 999f, entityId: 0);
        provider.ApplyClip(state, in flapFrame, "flap", 0f, pose);
        Assert.Equal(16f * ToRad - 0.1f, pose.Get("left_wing_base").Rotation.Z, 5);

        // "idle" samples the live frame: age 0, id 0 also gives flapTime 0.
        pose.Clear();
        var idleFrame = Frame(age: 0f, entityId: 0);
        provider.ApplyClip(state, in idleFrame, "idle", 123f, pose);
        Assert.Equal(16f * ToRad - 0.1f, pose.Get("left_wing_base").Rotation.Z, 5);
        Assert.Equal(-10f * ToRad, pose.Get("tail_base").Rotation.X, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInThePhantomModel()
    {
        var modelParts = PhantomModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(age: 2.1f, entityId: 3));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("tail_base", modelParts);
        Assert.Contains("tail_tip", modelParts);
        Assert.Contains("left_wing_base", modelParts);
        Assert.Contains("left_wing_tip", modelParts);
        Assert.Contains("right_wing_base", modelParts);
        Assert.Contains("right_wing_tip", modelParts);
    }
}
