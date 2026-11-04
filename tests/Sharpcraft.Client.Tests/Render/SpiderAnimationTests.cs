// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Spider procedural animation ported from <c>SpiderModel.setupAnim</c>. Covers the head look, the four phase-shifted
/// leg pairs (yaw swing + z lift, right positive / left negative), the walk-phase dependency, clip reconstruction,
/// amplitude scaling and part coverage for both the Spider and the shared Cave Spider model.
/// </summary>
public sealed class SpiderAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f) => new(
        TypeId: SpiderModelDefinition.SpiderEntityTypeId,
        AgeInTicks: 0f,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new SpiderAnimationProvider(SpiderModelDefinition.SpiderEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderAdvertisesTheTwoClipsAndTypeId()
    {
        var provider = new SpiderAnimationProvider(SpiderModelDefinition.SpiderEntityTypeId);
        Assert.Equal(SpiderModelDefinition.SpiderEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var pose = Apply(Frame(pitch: 15f, yaw: -30f));
        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-30f * ToRad, pose.Get("head").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 5);
    }

    [Fact]
    public void LegsSwingAtWalkPosZeroWithFourPhasePairs()
    {
        // animationPos = 0, speed = 1.
        var pose = Apply(Frame(walkPos: 0f, walkSpeed: 1f));

        // swing = -cos(phase) * 0.4; step = |sin(phase)| * 0.4.
        AssertVec(new Vector3(0f, -0.4f, 0f), pose.Get("right_hind_leg").Rotation);
        AssertVec(new Vector3(0f, 0.4f, 0f), pose.Get("left_hind_leg").Rotation);

        AssertVec(new Vector3(0f, 0.4f, 0f), pose.Get("right_middle_hind_leg").Rotation);
        AssertVec(new Vector3(0f, -0.4f, 0f), pose.Get("left_middle_hind_leg").Rotation);

        // middle-front: swing -cos(π/2) ≈ 0, step |sin(π/2)| * 0.4 = 0.4.
        Assert.Equal(0f, pose.Get("right_middle_front_leg").Rotation.Y, 4);
        Assert.Equal(0.4f, pose.Get("right_middle_front_leg").Rotation.Z, 4);
        Assert.Equal(0f, pose.Get("left_middle_front_leg").Rotation.Y, 4);
        Assert.Equal(-0.4f, pose.Get("left_middle_front_leg").Rotation.Z, 4);

        // front: swing -cos(3π/2) ≈ 0, step |sin(3π/2)| * 0.4 = 0.4.
        Assert.Equal(0f, pose.Get("right_front_leg").Rotation.Y, 4);
        Assert.Equal(0.4f, pose.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(-0.4f, pose.Get("left_front_leg").Rotation.Z, 4);
    }

    [Fact]
    public void LeftLegsMirrorRightLegs()
    {
        var pose = Apply(Frame(walkPos: 0.9f, walkSpeed: 0.8f));
        foreach (var (right, left) in new[]
                 {
                     ("right_hind_leg", "left_hind_leg"),
                     ("right_middle_hind_leg", "left_middle_hind_leg"),
                     ("right_middle_front_leg", "left_middle_front_leg"),
                     ("right_front_leg", "left_front_leg")
                 })
        {
            Assert.Equal(pose.Get(right).Rotation.Y, -pose.Get(left).Rotation.Y, 5);
            Assert.Equal(pose.Get(right).Rotation.Z, -pose.Get(left).Rotation.Z, 5);
        }
    }

    [Fact]
    public void WalkPhaseShiftsFromTheWalkPosition()
    {
        // animationPos = π/2: doublePos = π, so hind swing = -cos(π)*0.4 = +0.4 and hind step = |sin(π/2)|*0.4 = 0.4.
        var walkPos = (MathF.PI / 2f) / 0.6662f;
        var pose = Apply(Frame(walkPos: walkPos, walkSpeed: 1f));

        Assert.Equal(0.4f, pose.Get("right_hind_leg").Rotation.Y, 4);
        Assert.Equal(0.4f, pose.Get("right_hind_leg").Rotation.Z, 4);
        // middle-hind is one half-cycle behind: swing = -cos(2π)*0.4 = -0.4.
        Assert.Equal(-0.4f, pose.Get("right_middle_hind_leg").Rotation.Y, 4);
        // middle-front/front have zero step at this phase.
        Assert.Equal(0f, pose.Get("right_middle_front_leg").Rotation.Z, 4);
        Assert.Equal(0f, pose.Get("right_front_leg").Rotation.Z, 4);
    }

    [Fact]
    public void AmplitudeScalesWithWalkSpeed()
    {
        var half = Apply(Frame(walkPos: 0f, walkSpeed: 0.5f));
        Assert.Equal(-0.2f, half.Get("right_hind_leg").Rotation.Y, 5);
        Assert.Equal(0.2f, half.Get("right_middle_front_leg").Rotation.Z, 5);

        var still = Apply(Frame(walkPos: 1.3f, walkSpeed: 0f));
        foreach (var leg in new[]
                 {
                     "right_hind_leg", "left_hind_leg", "right_middle_hind_leg", "left_middle_hind_leg",
                     "right_middle_front_leg", "left_middle_front_leg", "right_front_leg", "left_front_leg"
                 })
        {
            AssertVec(Vector3.Zero, still.Get(leg).Rotation, 5);
        }
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndWalk()
    {
        var provider = new SpiderAnimationProvider(SpiderModelDefinition.SpiderEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 10f, yaw: 20f, walkPos: 3f, walkSpeed: 1f);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "walk", 0f, pose);
        // seconds 0 -> walkPos 0 -> hind swing -0.4.
        Assert.Equal(-0.4f, pose.Get("right_hind_leg").Rotation.Y, 5);
        Assert.Equal(10f * ToRad, pose.Get("head").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        // Idle still looks around but does not swing the legs.
        Assert.Equal(10f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(20f * ToRad, pose.Get("head").Rotation.Y, 5);
        AssertVec(Vector3.Zero, pose.Get("right_hind_leg").Rotation, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheSpiderAndCaveSpiderModels()
    {
        var spiderParts = SpiderModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var caveParts = CaveSpiderModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(walkPos: 0.7f, walkSpeed: 1f));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, spiderParts);
            Assert.Contains(partName, caveParts);
        }

        Assert.Contains("head", spiderParts);
        Assert.Contains("right_hind_leg", spiderParts);
        Assert.Contains("left_front_leg", spiderParts);

        // Cave Spider reuses exactly the Spider part set.
        Assert.Equal(spiderParts, caveParts);
    }
}
