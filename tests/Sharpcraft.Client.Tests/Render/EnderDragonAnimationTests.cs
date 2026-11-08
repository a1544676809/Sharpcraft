// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Ender Dragon <c>setupAnim</c>. The flap-clock sections (jaw, both wings with skin membranes, six leg chains) and
/// the <c>DragonFlightHistory</c>-driven neck/head/body/tail chains are pinned here; without a history the history
/// parts stay at the bind pose.
/// </summary>
public sealed class EnderDragonAnimationTests
{
    private static readonly int TypeId = EnderDragonModelDefinition.EnderDragonEntityTypeId;

    private static EntityAnimationFrame Frame(
        float partialTick = 0f,
        DragonFlightHistory? history = null,
        bool landingOrTakingOff = false,
        bool sitting = false,
        double distanceToEgg = 0) => new(
        TypeId: TypeId,
        AgeInTicks: 0f,
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
        PartialTick: partialTick,
        DragonHistory: history,
        DragonIsLandingOrTakingOff: landingOrTakingOff,
        DragonIsSitting: sitting,
        DragonDistanceToEgg: distanceToEgg);

    private static EnderDragonAnimationProvider NewProvider() => new(TypeId);

    private static EntityPose Apply(EnderDragonAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void Tick(EnderDragonAnimationProvider.State state, float horizontalDistance, float verticalDistance, int phase = 10, bool dying = false) =>
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(
            1, horizontalDistance, false, -1, verticalDistance, true, DragonPhase: phase, DragonDying: dying));

    [Fact]
    public void ProviderAdvertisesTypeIdAndIdleClip()
    {
        var provider = NewProvider();
        Assert.Equal(EnderDragonModelDefinition.EnderDragonEntityTypeId, provider.TypeId);
        Assert.True(EnderDragonModelDefinition.Definition.ApplyRootPose);
        Assert.Equal(new[] { "idle" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
        Assert.IsType<EnderDragonAnimationProvider.State>(provider.CreateState());
    }

    [Fact]
    public void RestFlapPoseMatchesTheReference()
    {
        var provider = NewProvider();
        var pose = Apply(provider, provider.CreateState(), Frame());

        Assert.Equal(0.2f, pose.Get("jaw").Rotation.X, 5);

        Assert.Equal(-0.075f, pose.Get("left_wing").Rotation.X, 5);
        Assert.Equal(-0.25f, pose.Get("left_wing").Rotation.Y, 5);
        Assert.Equal(-0.1f, pose.Get("left_wing").Rotation.Z, 5);
        Assert.Equal(1.0569731f, pose.Get("left_wing_tip").Rotation.Z, 5);

        // The skin membrane rides with its wing bone.
        Assert.Equal(pose.Get("left_wing").Rotation, pose.Get("left_wing_skin").Rotation);
        Assert.Equal(pose.Get("left_wing_tip").Rotation, pose.Get("left_wing_tip_skin").Rotation);

        // Right wing mirrors X stays, Y and Z flip; the tip mirrors Z.
        Assert.Equal(-0.075f, pose.Get("right_wing").Rotation.X, 5);
        Assert.Equal(0.25f, pose.Get("right_wing").Rotation.Y, 5);
        Assert.Equal(0.1f, pose.Get("right_wing").Rotation.Z, 5);
        Assert.Equal(-1.0569731f, pose.Get("right_wing_tip").Rotation.Z, 5);

        // poseLimbs: front-foot/tip and all hind segments are +0.1*bounce, the front-leg tip is -0.1*bounce.
        Assert.Equal(0.0017109f, pose.Get("left_front_leg").Rotation.X, 5);
        Assert.Equal(-0.0017109f, pose.Get("left_front_leg_tip").Rotation.X, 5);
        Assert.Equal(0.0017109f, pose.Get("left_front_foot").Rotation.X, 5);
        Assert.Equal(0.0017109f, pose.Get("left_hind_leg").Rotation.X, 5);
        Assert.Equal(0.0017109f, pose.Get("left_hind_leg_tip").Rotation.X, 5);
        Assert.Equal(0.0017109f, pose.Get("left_hind_foot").Rotation.X, 5);
        Assert.Equal(pose.Get("left_hind_leg").Rotation, pose.Get("right_hind_leg").Rotation);

        // Root group: z = -48 and a non-zero flap bounce offset.
        Assert.Equal(-48f, pose.Get("root").Position.Z, 5);
        Assert.NotEqual(0f, pose.Get("root").Position.Y);
    }

    [Fact]
    public void OneFlightTickAdvancesFlapTimeAndTheFlapPose()
    {
        var provider = NewProvider();
        var state = (EnderDragonAnimationProvider.State)provider.CreateState();
        Tick(state, horizontalDistance: 0f, verticalDistance: 0f); // flapSpeed = 0.2
        Assert.Equal(0.2f, state.FlapTime, 5);

        var pose = Apply(provider, state, Frame(partialTick: 1f));
        Assert.Equal(0.3902113f, pose.Get("jaw").Rotation.X, 5);
        Assert.Equal(0.0631966f, pose.Get("left_wing").Rotation.X, 5);
        Assert.Equal(-0.8608452f, pose.Get("left_wing").Rotation.Z, 5);
        Assert.Equal(0.2889069f, pose.Get("left_wing_tip").Rotation.Z, 5);
        Assert.Equal(0.0203987f, pose.Get("left_front_leg").Rotation.X, 5);
    }

    [Fact]
    public void PartialTickInterpolatesBetweenThePreviousAndCurrentFlapTime()
    {
        var provider = NewProvider();
        var state = (EnderDragonAnimationProvider.State)provider.CreateState();
        Tick(state, 0f, 0f); // previous 0 -> current 0.2

        var atZero = Apply(provider, state, Frame(partialTick: 0f));
        var atHalf = Apply(provider, state, Frame(partialTick: 0.5f));
        var atOne = Apply(provider, state, Frame(partialTick: 1f));

        Assert.Equal(0.2f, atZero.Get("jaw").Rotation.X, 5);      // previous tick value (flapTime 0)
        Assert.Equal(0.3902113f, atOne.Get("jaw").Rotation.X, 5); // current tick value (flapTime 0.2)
        // Half is flapTime 0.1, evaluated through the same sine curve (not a linear jaw blend).
        Assert.Equal(0.3175571f, atHalf.Get("jaw").Rotation.X, 5);
    }

    [Fact]
    public void FlapSpeedUsesTheHorizontalAndVerticalDisplacement()
    {
        var provider = NewProvider();

        var vertical = (EnderDragonAnimationProvider.State)provider.CreateState();
        Tick(vertical, horizontalDistance: 0f, verticalDistance: 1f); // 0.2 * 2^1 = 0.4
        Assert.Equal(0.4f, vertical.FlapTime, 5);

        var horizontal = (EnderDragonAnimationProvider.State)provider.CreateState();
        Tick(horizontal, horizontalDistance: 0.5f, verticalDistance: 0f); // 0.2 / (0.5*10 + 1) = 0.03333..
        Assert.Equal(0.0333333f, horizontal.FlapTime, 5);
    }

    [Fact]
    public void SittingAndDyingPhasesChangeTheFlapClock()
    {
        var provider = NewProvider();
        var sitting = (EnderDragonAnimationProvider.State)provider.CreateState();
        Tick(sitting, horizontalDistance: 0f, verticalDistance: 0f, phase: 5);
        Assert.Equal(0.1f, sitting.FlapTime, 5);

        var dying = (EnderDragonAnimationProvider.State)provider.CreateState();
        Tick(dying, horizontalDistance: 0f, verticalDistance: 0f, phase: 9, dying: true);
        Assert.Equal(0f, dying.FlapTime, 5);
    }

    [Fact]
    public void DragonFlightHistoryKeepsSixtyFourSamplesAndInterpolates()
    {
        var history = new DragonFlightHistory();
        for (var i = 0; i < 64; i++)
        {
            history.Record(i, i * 10f);
        }

        Assert.Equal(63d, history.Get(0).Y);
        Assert.Equal(0d, history.Get(63).Y);
        var interpolated = history.Get(0, 0.5f);
        Assert.Equal(62.5d, interpolated.Y);
        Assert.Equal(625f, interpolated.YRot, 3);
    }

    [Fact]
    public void FlightHistoryDrivesNeckHeadBodyAndTail()
    {
        var history = new DragonFlightHistory();
        for (var i = 0; i < 48; i++)
        {
            history.Record(y: i * 0.5, yRot: i * 3f);
        }

        var provider = NewProvider();
        var pose = Apply(provider, provider.CreateState(), Frame(partialTick: 1f, history: history));

        Assert.NotEqual(Vector3.Zero, pose.Get("neck0").Rotation);
        Assert.NotEqual(Vector3.Zero, pose.Get("neck4").Position);
        Assert.NotEqual(Vector3.Zero, pose.Get("head").Rotation);
        Assert.NotEqual(0f, pose.Get("body").Rotation.Z);
        Assert.NotEqual(Vector3.Zero, pose.Get("tail0").Rotation);
        Assert.NotEqual(Vector3.Zero, pose.Get("tail11").Position);
    }

    [Fact]
    public void WithoutFlightHistoryTheHistoryDependentPartsRemainAtTheBindPose()
    {
        var provider = NewProvider();
        var state = (EnderDragonAnimationProvider.State)provider.CreateState();
        for (var i = 0; i < 8; i++)
        {
            Tick(state, 0.3f, 0.05f);
        }

        var pose = Apply(provider, state, Frame(partialTick: 1f));

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(Vector3.Zero, pose.Get("neck" + i).Rotation);
            Assert.Equal(Vector3.Zero, pose.Get("neck" + i).Position);
        }

        Assert.Equal(Vector3.Zero, pose.Get("head").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("head").Position);
        Assert.Equal(Vector3.Zero, pose.Get("body").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("body").Position);
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(Vector3.Zero, pose.Get("tail" + i).Rotation);
            Assert.Equal(Vector3.Zero, pose.Get("tail" + i).Position);
        }
    }

    [Fact]
    public void ApplyClipIdlePreviewsTheFlapClock()
    {
        var provider = NewProvider();
        var state = provider.CreateState();

        var rest = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0f, rest);
        Assert.Equal(0.2f, rest.Get("jaw").Rotation.X, 5);

        // 0.0625 s -> 0.25 flap-time ticks -> flap = pi/2 -> jaw = (1 + 1) * 0.2 = 0.4.
        var quarter = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0.0625f, quarter);
        Assert.Equal(0.4f, quarter.Get("jaw").Rotation.X, 5);
        Assert.Equal(0.125f, quarter.Get("left_wing").Rotation.X, 5);
    }
}
