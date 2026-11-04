// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Fox procedural animation ported from <c>FoxModel.setupAnim</c> + <c>AdultFoxModel</c> overrides. The tests pin the
/// walking leg swing, the crouch body/leg wiggle, the sleeping and sitting absolute poses (reduced to bind deltas), the
/// head-look exclusions, and the faceplant leg-motion counter. The blocked inputs (headRollAngle, crouchAmount,
/// ageScale, leg visibility) are asserted to stay un-faked.
/// </summary>
public sealed class FoxAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float walkPos = 0f,
        float walkSpeed = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float age = 0f,
        bool crouch = false,
        bool sleep = false,
        bool sit = false,
        bool pounce = false,
        bool faceplant = false) => new(
        TypeId: FoxModelDefinition.FoxEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        IsCrouching: crouch,
        IsSleeping: sleep,
        IsSitting: sit,
        IsPouncing: pounce,
        IsFaceplanted: faceplant);

    private static EntityPose Apply(FoxProxy proxy, IEntityAnimationState state, EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        proxy.Provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    /// <summary>Tiny holder so tests can reuse a single provider/state pair without a field.</summary>
    private readonly record struct FoxProxy(FoxAnimationProvider Provider);

    [Fact]
    public void ProviderAdvertisesTheSevenClipsAndTypeId()
    {
        var provider = new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId);
        Assert.Equal(FoxModelDefinition.FoxEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "crouch", "sleep", "sit", "pounce", "faceplant" }, provider.Clips);
        Assert.IsAssignableFrom<IEntityTickAnimationState>(provider.CreateState());
    }

    [Fact]
    public void WalkingLegsSwingOppositeOnEachSide()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        var start = Apply(proxy, state, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(1.4f, start.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, start.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, start.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(1.4f, start.Get("left_front_leg").Rotation.X, 4);

        var half = Apply(proxy, state, Frame(walkPos: MathF.PI / 0.6662f, walkSpeed: 1f));
        Assert.Equal(-1.4f, half.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(1.4f, half.Get("left_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void HeadRollIsBlockedSoWalkingLeavesHeadNeutral()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        // headRollAngle is not on the frame; the walking head roll cannot be produced, and no head look is applied.
        var walking = Apply(proxy, state, Frame(walkPos: 1f, walkSpeed: 1f));
        Assert.Equal(Vector3.Zero, walking.Get("head").Rotation);
    }

    [Fact]
    public void CrouchingBumpsTheBodyAndWigglesTheLegsButBlocksTheCrouchAmountOffsets()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        // age = 0 -> cos = 1 -> wiggle = 0.05.
        var crouch = Apply(proxy, state, Frame(age: 0f, crouch: true));

        Assert.Equal(0.10471976f, crouch.Get("body").Rotation.X, 4);
        Assert.Equal(0.05f, crouch.Get("body").Rotation.Y, 4);

        Assert.Equal(0.05f, crouch.Get("right_hind_leg").Rotation.Z, 4);
        Assert.Equal(0.05f, crouch.Get("left_hind_leg").Rotation.Z, 4);
        Assert.Equal(0.025f, crouch.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(0.025f, crouch.Get("left_front_leg").Rotation.Z, 4);

        // crouchAmount * ageScale is missing: head.y offset stays 0 (not faked).
        Assert.Equal(0f, crouch.Get("head").Position.Y, 4);
        // AdultFox body.y += crouchAmount is missing: body position stays 0 (not faked).
        Assert.Equal(Vector3.Zero, crouch.Get("body").Position);
        // Head look is skipped while crouching.
        Assert.Equal(Vector3.Zero, crouch.Get("head").Rotation);
    }

    [Fact]
    public void SleepingPoseMatchesTheAdultOverride()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        // age = 0 -> cos(0) = 1 -> head zRot = 1/22.
        var sleep = Apply(proxy, state, Frame(age: 0f, sleep: true));

        Assert.Equal(0f, sleep.Get("head").Rotation.X, 4);
        Assert.Equal(-2f * MathF.PI / 3f, sleep.Get("head").Rotation.Y, 4);
        Assert.Equal(1f / 22f, sleep.Get("head").Rotation.Z, 4);

        // AdultFox: head.x += 2 (x kept), head.y += 2.99 (y-down -> +2.99 becomes -2.99 in y-up storage).
        AssertVec(new Vector3(2f, -2.99f, 0f), sleep.Get("head").Position);

        // AdultFox: body.zRot = -π/2, body.y += 5 (-> -5).
        Assert.Equal(-MathF.PI / 2f, sleep.Get("body").Rotation.Z, 4);
        AssertVec(new Vector3(0f, -5f, 0f), sleep.Get("body").Position);

        // AdultFox tail.xRot = -5π/6 absolute, reduced by the reference tail bind -0.05235988.
        Assert.Equal((-5f * MathF.PI / 6f) - (-0.05235988f), sleep.Get("tail").Rotation.X, 4);

        // head look is ignored while sleeping even with a pitch/yaw supplied.
        var looking = Apply(proxy, state, Frame(pitch: 40f, yaw: 40f, age: 0f, sleep: true));
        Assert.Equal(0f, looking.Get("head").Rotation.X, 4);
        Assert.Equal(-2f * MathF.PI / 3f, looking.Get("head").Rotation.Y, 4);
    }

    [Fact]
    public void SittingPoseMatchesTheAdultOverride()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        var sit = Apply(proxy, state, Frame(sit: true));

        // Body absolute xRot π/6 reduced by the reference body bind π/2.
        Assert.Equal((MathF.PI / 6f) - (MathF.PI / 2f), sit.Get("body").Rotation.X, 4);
        // body.y -= 7 -> +7, body.z += 3 -> +3.
        AssertVec(new Vector3(0f, 7f, 3f), sit.Get("body").Position);

        // tail.xRot = π/4 absolute (reduced by bind), tail.z-- -> -1.
        Assert.Equal((MathF.PI / 4f) - (-0.05235988f), sit.Get("tail").Rotation.X, 4);
        AssertVec(new Vector3(0f, 0f, -1f), sit.Get("tail").Position);

        // head.y -= 6.5 -> +6.5, head.z += 2.75 -> +2.75; base setSittingPose zeroes the rotation but the later
        // head-look block still applies (reference does not exclude sitting).
        AssertVec(new Vector3(0f, 6.5f, 2.75f), sit.Get("head").Position);
        Assert.Equal(0f, sit.Get("head").Rotation.X, 4);
        Assert.Equal(0f, sit.Get("head").Rotation.Y, 4);

        // Front legs -π/12, hind legs -5π/12 with the +4 / -0.25 hind offsets (y-down -> -4).
        Assert.Equal(-MathF.PI / 12f, sit.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-MathF.PI / 12f, sit.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(-5f * MathF.PI / 12f, sit.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-5f * MathF.PI / 12f, sit.Get("left_hind_leg").Rotation.X, 4);
        AssertVec(new Vector3(0f, -4f, -0.25f), sit.Get("right_hind_leg").Position);

        // Sitting is not excluded from the head look, so a supplied yaw is applied (matches setupAnim).
        var looking = Apply(proxy, state, Frame(yaw: 30f, sit: true));
        Assert.Equal(30f * ToRad, looking.Get("head").Rotation.Y, 4);
    }

    [Fact]
    public void PouncingAddsNothingBecauseCrouchAmountIsBlocked()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        var pounce = Apply(proxy, state, Frame(pitch: 10f, pounce: true));

        // AdultFox pounce offsets need crouchAmount, which is not on the frame: no body/head position change.
        Assert.Equal(Vector3.Zero, pounce.Get("body").Position);
        Assert.Equal(Vector3.Zero, pounce.Get("head").Position);
        // Legs stay at the walking pose (speed 0 -> neutral) and the head look still applies.
        Assert.Equal(0f, pounce.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(10f * ToRad, pounce.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void FaceplantLegSwingUsesTheLocalLegMotionCounter()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();
        var ticks = Assert.IsAssignableFrom<IEntityTickAnimationState>(state);

        // Arm the state's faceplanted flag (first Apply), then advance the counter three ticks: 3 * 0.67 = 2.01.
        Apply(proxy, state, Frame(faceplant: true));
        for (var i = 0; i < 3; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        var phase = 3f * 0.67f * 0.4662f;
        var moving = Apply(proxy, state, Frame(pitch: 10f, faceplant: true));

        Assert.Equal(MathF.Cos(phase) * 0.1f, moving.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(MathF.Cos(phase + MathF.PI) * 0.1f, moving.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(MathF.Cos(phase + MathF.PI) * 0.1f, moving.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(MathF.Cos(phase) * 0.1f, moving.Get("left_front_leg").Rotation.X, 4);

        // Head look is skipped while faceplanted.
        Assert.Equal(Vector3.Zero, moving.Get("head").Rotation);
    }

    [Fact]
    public void FaceplantDoesNotAdvanceWhileNotFaceplanted()
    {
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();
        var ticks = Assert.IsAssignableFrom<IEntityTickAnimationState>(state);

        // Flag false: ticks must not advance the counter, so a later faceplant starts from phase 0.
        Apply(proxy, state, Frame());
        for (var i = 0; i < 5; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        var planted = Apply(proxy, state, Frame(faceplant: true));
        Assert.Equal(0.1f, planted.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-0.1f, planted.Get("left_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyClipPreviewsEveryClip()
    {
        var provider = new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "idle", 0.5f, pose);
        Assert.Equal(0f, pose.Get("right_hind_leg").Rotation.X, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "walk", 0.2f, pose);
        Assert.Equal(MathF.Cos(2f * 0.6662f) * 1.4f, pose.Get("right_hind_leg").Rotation.X, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "crouch", 0f, pose);
        Assert.Equal(0.10471976f, pose.Get("body").Rotation.X, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "sleep", 0f, pose);
        Assert.Equal(-2f * MathF.PI / 3f, pose.Get("head").Rotation.Y, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "sit", 0f, pose);
        Assert.Equal((MathF.PI / 6f) - (MathF.PI / 2f), pose.Get("body").Rotation.X, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "pounce", 0f, pose);
        Assert.Equal(Vector3.Zero, pose.Get("body").Position);

        pose.Clear();
        provider.ApplyClip(state, in frame, "faceplant", 0f, pose);
        Assert.Equal(0.1f, pose.Get("right_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheFoxModel()
    {
        var modelParts = FoxModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var proxy = new FoxProxy(new FoxAnimationProvider(FoxModelDefinition.FoxEntityTypeId));
        var state = proxy.Provider.CreateState();

        var pose = Apply(proxy, state, Frame(age: 0f, walkPos: 1f, walkSpeed: 1f, sit: true, faceplant: false));
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }
    }
}
