// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Parrot procedural animation ported from <c>ParrotModel.setupAnim</c> (with <c>prepare</c>/<c>getPose</c>). The tests
/// pin the flapAngle → wing roll, the shared body bob, the head look, the tail/leg walk phase, pose selection and the
/// part-name contract. SITTING/PARTY are blocked (not selectable from the frame) and are not tested.
/// </summary>
public sealed class ParrotAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float flap = 0f,
        float flapSpeed = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float age = 0f,
        bool onGround = true,
        bool passenger = false) => new(
        TypeId: ParrotModelDefinition.ParrotEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: flap,
        FlapSpeed: flapSpeed,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        IsOnGround: onGround,
        IsPassenger: passenger);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new ParrotAnimationProvider(ParrotModelDefinition.ParrotEntityTypeId);
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
    public void ProviderAdvertisesTheThreeClipsAndTypeId()
    {
        var provider = new ParrotAnimationProvider(ParrotModelDefinition.ParrotEntityTypeId);
        Assert.Equal(ParrotModelDefinition.ParrotEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "flying", "standing", "on_shoulder" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void FlapAngleDrivesWingRollAndBodyBob()
    {
        // flap = π/2 -> sin = 1; flapSpeed = 1 -> flapAngle = (1 + 1) * 1 = 2; bob = 2 * 0.3 = 0.6.
        var pose = Apply(Frame(flap: MathF.PI / 2f, flapSpeed: 1f));

        Assert.Equal(-0.0873f - 2f, pose.Get("left_wing").Rotation.Z, 5);
        Assert.Equal(0.0873f + 2f, pose.Get("right_wing").Rotation.Z, 5);

        // Reference adds +0.6 to each part's y (y-down); storage is y-up so the offset is -0.6.
        Assert.Equal(-0.6f, pose.Get("head").Position.Y, 5);
        Assert.Equal(-0.6f, pose.Get("tail").Position.Y, 5);
        Assert.Equal(-0.6f, pose.Get("body").Position.Y, 5);
        Assert.Equal(-0.6f, pose.Get("left_wing").Position.Y, 5);
        Assert.Equal(-0.6f, pose.Get("right_wing").Position.Y, 5);
        Assert.Equal(-0.6f, pose.Get("left_leg").Position.Y, 5);
        Assert.Equal(-0.6f, pose.Get("right_leg").Position.Y, 5);

        // Without flapping (flapSpeed = 0) the wings rest at the baseline roll and nothing bobs.
        var resting = Apply(Frame(flap: MathF.PI / 2f, flapSpeed: 0f));
        Assert.Equal(-0.0873f, resting.Get("left_wing").Rotation.Z, 5);
        Assert.Equal(0.0873f, resting.Get("right_wing").Rotation.Z, 5);
        Assert.Equal(0f, resting.Get("body").Position.Y, 5);
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
    public void TailSweepsWithTheWalkPhase()
    {
        var start = Apply(Frame(walkPos: 0f, walkSpeed: 1f, onGround: true));
        Assert.Equal(0.3f, start.Get("tail").Rotation.X, 5);

        var halfPhasePos = MathF.PI / 0.6662f;
        var half = Apply(Frame(walkPos: halfPhasePos, walkSpeed: 1f, onGround: true));
        Assert.Equal(-0.3f, half.Get("tail").Rotation.X, 4);
    }

    [Fact]
    public void PoseSelectionPicksFlyingStandingOrOnShoulder()
    {
        // FLYING: !onGround and not a passenger -> prepare lifts both legs by 2π/9.
        var flying = Apply(Frame(walkPos: 0f, walkSpeed: 1f, onGround: false, passenger: false));
        Assert.Equal(2f * MathF.PI / 9f, flying.Get("left_leg").Rotation.X, 5);
        Assert.Equal(2f * MathF.PI / 9f, flying.Get("right_leg").Rotation.X, 5);

        // STANDING: on the ground -> leg walk swing (no prepare lift).
        var standing = Apply(Frame(walkPos: 0f, walkSpeed: 1f, onGround: true, passenger: false));
        Assert.Equal(1.4f, standing.Get("left_leg").Rotation.X, 5);
        Assert.Equal(-1.4f, standing.Get("right_leg").Rotation.X, 5);

        // ON_SHOULDER: passenger -> neither the flying lift nor the walk swing.
        var shoulder = Apply(Frame(walkPos: 0f, walkSpeed: 1f, onGround: false, passenger: true));
        Assert.Equal(0f, shoulder.Get("left_leg").Rotation.X, 5);
        Assert.Equal(0f, shoulder.Get("right_leg").Rotation.X, 5);

        // Passenger wins even when nominally on the ground.
        var ride = Apply(Frame(onGround: true, passenger: true));
        Assert.Equal(0f, ride.Get("left_leg").Rotation.X, 5);
    }

    [Fact]
    public void StandingLegPhaseIsOppositeOnEachSide()
    {
        var start = Apply(Frame(walkPos: 0f, walkSpeed: 1f, onGround: true));
        Assert.Equal(1.4f, start.Get("left_leg").Rotation.X, 5);
        Assert.Equal(-1.4f, start.Get("right_leg").Rotation.X, 5);

        var halfPhasePos = MathF.PI / 0.6662f;
        var half = Apply(Frame(walkPos: halfPhasePos, walkSpeed: 1f, onGround: true));
        Assert.Equal(-1.4f, half.Get("left_leg").Rotation.X, 4);
        Assert.Equal(1.4f, half.Get("right_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyClipReconstructsTheThreePoses()
    {
        var provider = new ParrotAnimationProvider(ParrotModelDefinition.ParrotEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "flying", 0f, pose);
        Assert.Equal(2f * MathF.PI / 9f, pose.Get("left_leg").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "standing", 0f, pose);
        Assert.Equal(1.4f, pose.Get("left_leg").Rotation.X, 5);
        Assert.Equal(-1.4f, pose.Get("right_leg").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "on_shoulder", 0f, pose);
        Assert.Equal(0f, pose.Get("left_leg").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("right_leg").Rotation.X, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheParrotModel()
    {
        var modelParts = ParrotModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame());

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }
    }
}
