// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Sniffer animation coverage ported from the reference SnifferAnimation definitions and SnifferModel.setupAnim.
/// Pins the five state-clip lengths/looping, representative keyframe values (including position Y inversion,
/// scale-1 offsets and degree→radian conversion), the walk/sniff-search applyWalk mapping, the synced
/// <c>Sniffer.State</c> ordinal → clip routing, and the part-name contract with the Sniffer model.
/// </summary>
public sealed class SnifferAnimationTests
{
    private const int SnifferStateSerializerId = 35;

    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static EntityPose Apply(KeyframeAnimation animation, float seconds) =>
        Apply(animation, seconds, 1f);

    private static EntityPose Apply(KeyframeAnimation animation, float seconds, float scale)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, scale, pose);
        return pose;
    }

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float pitch = 0f,
        float yaw = 0f) =>
        new(
            SnifferModelDefinition.SnifferEntityTypeId,
            ageInTicks,
            walkPos,
            walkSpeed,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            0f);

    [Fact]
    public void ClipsDeclareReferenceLengthsAndLooping()
    {
        Assert.Equal(2f, SnifferAnimations.FeelingHappy.LengthInSeconds);
        Assert.True(SnifferAnimations.FeelingHappy.Looping);

        Assert.Equal(8f, SnifferAnimations.Scenting.LengthInSeconds);
        Assert.True(SnifferAnimations.Scenting.Looping);

        Assert.Equal(1f, SnifferAnimations.Sniffing.LengthInSeconds);
        Assert.False(SnifferAnimations.Sniffing.Looping);

        Assert.Equal(8f, SnifferAnimations.Digging.LengthInSeconds);
        Assert.False(SnifferAnimations.Digging.Looping);

        Assert.Equal(3f, SnifferAnimations.Rising.LengthInSeconds);
        Assert.False(SnifferAnimations.Rising.Looping);

        Assert.Equal(2f, SnifferAnimations.Walk.LengthInSeconds);
        Assert.True(SnifferAnimations.Walk.Looping);

        Assert.Equal(2f, SnifferAnimations.SniffSearch.LengthInSeconds);
        Assert.True(SnifferAnimations.SniffSearch.Looping);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheSnifferModel()
    {
        var modelParts = SnifferModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var definition in new[]
                 {
                     SnifferAnimations.FeelingHappy, SnifferAnimations.Scenting, SnifferAnimations.Sniffing,
                     SnifferAnimations.Digging, SnifferAnimations.Rising, SnifferAnimations.Walk,
                     SnifferAnimations.SniffSearch
                 })
        {
            foreach (var partName in definition.BoneAnimations.Keys)
            {
                Assert.Contains(partName, modelParts);
            }
        }
    }

    [Fact]
    public void FeelingHappyMatchesReferenceStartAndMidKeyframes()
    {
        var start = Apply(SnifferAnimations.FeelingHappyBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("head").Rotation);
        Assert.Equal(Vector3.Zero, start.Get("left_ear").Rotation);

        var mid = Apply(SnifferAnimations.FeelingHappyBaked, 0.5f);
        Assert.Equal(Rad(-32.00206f), mid.Get("head").Rotation.X, 4);
        Assert.Equal(Rad(19.3546f), mid.Get("head").Rotation.Y, 4);
        Assert.Equal(Rad(-11.70092f), mid.Get("head").Rotation.Z, 4);

        var earFlick = Apply(SnifferAnimations.FeelingHappyBaked, 0.75f);
        Assert.Equal(Rad(-67.5f), earFlick.Get("left_ear").Rotation.Z, 4);
        Assert.Equal(Rad(67.5f), earFlick.Get("right_ear").Rotation.Z, 4);
    }

    [Fact]
    public void FeelingHappyLoopsInsteadOfClamping()
    {
        // 2.5 s wraps to 0.5 s, which is the -32 degree head tilt of the mid keyframe.
        var wrapped = Apply(SnifferAnimations.FeelingHappyBaked, SnifferAnimations.FeelingHappy.LengthInSeconds + 0.5f);
        Assert.Equal(Rad(-32.00206f), wrapped.Get("head").Rotation.X, 3);
    }

    [Fact]
    public void ScentingMatchesReferenceNoseScaleKeyframes()
    {
        var start = Apply(SnifferAnimations.ScentingBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("nose").Scale);

        // scaleVec(1, 2.5, 1) is a +1.5 additive Y offset.
        var puff = Apply(SnifferAnimations.ScentingBaked, 0.6667f);
        Assert.Equal(1.5f, puff.Get("nose").Scale.Y, 4);

        var deep = Apply(SnifferAnimations.ScentingBaked, 1f);
        Assert.Equal(2f, deep.Get("nose").Scale.Y, 4);

        // 8.0 s loop: nine seconds wraps to the one-second keyframe again.
        var wrapped = Apply(SnifferAnimations.ScentingBaked, SnifferAnimations.Scenting.LengthInSeconds + 1f);
        Assert.Equal(2f, wrapped.Get("nose").Scale.Y, 4);
    }

    [Fact]
    public void SniffingMatchesReferenceNoseAndHeadKeyframes()
    {
        var start = Apply(SnifferAnimations.SniffingBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("nose").Scale);
        Assert.Equal(Vector3.Zero, start.Get("head").Rotation);

        // At 0.125 s the nose is scaleVec(1, 3, 1) -> +2 Y and the head is -5 degrees.
        var peak = Apply(SnifferAnimations.SniffingBaked, 0.125f);
        Assert.Equal(2f, peak.Get("nose").Scale.Y, 4);
        Assert.Equal(Rad(-5f), peak.Get("head").Rotation.X, 4);

        var longSniff = Apply(SnifferAnimations.SniffingBaked, 0.7083f);
        Assert.Equal(3f, longSniff.Get("nose").Scale.Y, 4);
    }

    [Fact]
    public void DiggingMatchesReferenceBodyHeadAndLegKeyframes()
    {
        var start = Apply(SnifferAnimations.DiggingBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("body").Position);
        Assert.Equal(Rad(-2.5f), start.Get("left_ear").Rotation.Z, 4);

        // body posVec(0, -7, 0) inverts Y to (0, 7, 0) as it drops to the ground.
        var drop = Apply(SnifferAnimations.DiggingBaked, 1.5f);
        Assert.Equal(new Vector3(0f, 7f, 0f), drop.Get("body").Position);
        Assert.Equal(Vector3.Zero, drop.Get("body").Scale);
        // scaleVec(1.04, 0.98, 1.02) is an additive (0.04, -0.02, 0.02) jolt just after landing.
        var jolt = Apply(SnifferAnimations.DiggingBaked, 1.5417f).Get("body").Scale;
        Assert.Equal(0.04f, jolt.X, 4);
        Assert.Equal(-0.02f, jolt.Y, 4);
        Assert.Equal(0.02f, jolt.Z, 4);
        Assert.Equal(Rad(90f), Apply(SnifferAnimations.DiggingBaked, 1.375f).Get("right_front_leg").Rotation.Z, 4);

        // head posVec(0, 6, 0) inverts Y to (0, -6, 0).
        var headUp = Apply(SnifferAnimations.DiggingBaked, 2.2917f);
        Assert.Equal(new Vector3(0f, -6f, 0f), headUp.Get("head").Position);
        Assert.Equal(Rad(5f), Apply(SnifferAnimations.DiggingBaked, 5.6667f).Get("body").Rotation.X, 4);
    }

    [Fact]
    public void DiggingClampsPastTheEndInsteadOfLooping()
    {
        // The last body-position keyframe (1.5 s) is (0, 7, 0); a looping clip would wrap 8.0 s back to zero.
        var clamped = Apply(SnifferAnimations.DiggingBaked, SnifferAnimations.Digging.LengthInSeconds);
        Assert.Equal(new Vector3(0f, 7f, 0f), clamped.Get("body").Position);
    }

    [Fact]
    public void RisingMatchesReferenceBodyAndLegKeyframes()
    {
        var start = Apply(SnifferAnimations.RisingBaked, 0f);
        Assert.Equal(Rad(90f), start.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(new Vector3(-4f, 5.5f, 0f), start.Get("right_front_leg").Position);
        // posVec(0, 1, 0) inverts Y to (0, -1, 0).
        Assert.Equal(new Vector3(0f, -1f, 0f), start.Get("head").Position);

        var crouched = Apply(SnifferAnimations.RisingBaked, 0.25f);
        Assert.Equal(new Vector3(0f, 7f, 0f), crouched.Get("body").Position);

        var stand = Apply(SnifferAnimations.RisingBaked, 0.2083f);
        Assert.Equal(new Vector3(6f, 5.5f, 0f), stand.Get("right_front_leg").Position);
        Assert.Equal(Rad(10f), Apply(SnifferAnimations.RisingBaked, 1f).Get("head").Rotation.X, 4);
        Assert.Equal(Vector3.Zero, Apply(SnifferAnimations.RisingBaked, 1.5f).Get("body").Position);
    }

    [Fact]
    public void ProviderLooksAlongPitchAndYawInRadians()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(pitch: 30f, yaw: -45f), pose);

        Assert.Equal(Rad(30f), pose.Get("head").Rotation.X, 4);
        Assert.Equal(Rad(-45f), pose.Get("head").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 4);
    }

    [Fact]
    public void ProviderUsesTheReferenceApplyWalkMapping()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var state = provider.CreateState();

        const float walkPos = 2f;
        const float walkSpeed = 0.5f;
        var pose = new EntityPose();
        provider.Apply(state, Frame(walkPos: walkPos, walkSpeed: walkSpeed), pose);

        // applyWalk(walkPos, walkSpeed, 9, 100): (long)(walkPos * 50 * 9) ms, scale = min(walkSpeed * 100, 1).
        var expectedSeconds = (long)(walkPos * 50f * 9f) / 1000f;
        var expectedScale = MathF.Min(walkSpeed * 100f, 1f);
        var expected = Apply(SnifferAnimations.WalkBaked, expectedSeconds, expectedScale);

        Assert.Equal(expected.Get("right_front_leg").Rotation.X, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(expected.Get("right_front_leg").Position, pose.Get("right_front_leg").Position);
        Assert.Equal(expected.Get("body").Rotation.Z, pose.Get("body").Rotation.Z, 4);
    }

    [Fact]
    public void SearchingSwitchesTheWalkCycleToTheSniffSearchCycle()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var state = provider.CreateState();

        // IDLING: the walk cycle's head keyframe at t=0 is +7.5 degrees.
        var idle = new EntityPose();
        provider.Apply(state, Frame(walkPos: 0f, walkSpeed: 1f), idle);
        Assert.Equal(Rad(7.5f), idle.Get("head").Rotation.X, 3);

        // SEARCHING (ordinal 4): the sniff-search head keyframe at t=0 is +35 degrees.
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(0, SnifferStateSerializerId, 4);
        var searching = new EntityPose();
        provider.Apply(state, Frame(walkPos: 0f, walkSpeed: 1f), searching);
        Assert.Equal(Rad(35f), searching.Get("head").Rotation.X, 3);
    }

    [Theory]
    [InlineData(1, 10, "head", 0)] // FEELING_HAPPY, 0.5 s, head X = -32.00206 deg
    [InlineData(2, 20, "nose", 1)] // SCENTING, 1.0 s, nose scale Y = +2
    [InlineData(3, 5, "nose", 1)]  // SNIFFING, 0.25 s, nose scale Y = +2
    [InlineData(5, 30, "body", 1)] // DIGGING, 1.5 s, body position Y = +7
    [InlineData(6, 5, "body", 1)]  // RISING, 0.25 s, body position Y = +7
    public void SyncedStateOrdinalRoutesToTheMatchingClip(int ordinal, int ticks, string part, int component)
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(0, SnifferStateSerializerId, ordinal);

        // First frame commits the transition at age 100; the second advances the clock by `ticks` ticks.
        provider.Apply(state, Frame(ageInTicks: 100f), new EntityPose());
        var pose = new EntityPose();
        provider.Apply(state, Frame(ageInTicks: 100f + ticks), pose);

        var offset = pose.Get(part);
        var value = component switch
        {
            0 => offset.Rotation.X,
            1 => offset.Scale.Y + offset.Position.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

        var expected = ordinal switch
        {
            1 => Rad(-32.00206f),
            2 => 2f,
            3 => 2f,
            5 => 7f,
            6 => 7f,
            _ => throw new ArgumentOutOfRangeException(nameof(ordinal))
        };

        Assert.Equal(expected, value, 3);
    }

    [Fact]
    public void ChangingStateStopsThePreviousClip()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var state = provider.CreateState();
        var synced = (IEntitySyncedDataAnimationState)state;

        synced.OnSyncedData(0, SnifferStateSerializerId, 1); // FEELING_HAPPY
        provider.Apply(state, Frame(ageInTicks: 100f), new EntityPose());
        var happy = new EntityPose();
        provider.Apply(state, Frame(ageInTicks: 110f), happy);
        Assert.True(happy.Get("head").Rotation.X < Rad(-20f), "feeling-happy should tilt the head sharply");

        synced.OnSyncedData(0, SnifferStateSerializerId, 6); // RISING
        provider.Apply(state, Frame(ageInTicks: 110f), new EntityPose());
        var rising = new EntityPose();
        provider.Apply(state, Frame(ageInTicks: 115f), rising);

        // Rising at 0.25 s drops the body; the previous head tilt must not linger.
        Assert.Equal(new Vector3(0f, 7f, 0f), rising.Get("body").Position);
        Assert.True(rising.Get("head").Rotation.X > Rad(-10f), "the feeling-happy head tilt should have stopped");
    }

    [Fact]
    public void ProviderExposesEveryStateClipAndItsTypeId()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        Assert.Equal(122, provider.TypeId);
        Assert.Equal(
            new[] { "feeling_happy", "scenting", "sniffing", "digging", "rising" },
            provider.Clips);
    }

    [Fact]
    public void ApplyClipPlaysEachNamedClipAndIgnoresUnknownOnes()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();

        foreach (var clip in provider.Clips)
        {
            var pose = new EntityPose();
            provider.ApplyClip(state, frame, clip, 0f, pose);
            Assert.NotEmpty(pose.Offsets);
        }

        var digging = new EntityPose();
        provider.ApplyClip(state, frame, "digging", 1.5f, digging);
        Assert.Equal(new Vector3(0f, 7f, 0f), digging.Get("body").Position);

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void UnrelatedSyncedDataDoesNotStartAnyClip()
    {
        var provider = new SnifferAnimationProvider(SnifferModelDefinition.SnifferEntityTypeId);
        var state = provider.CreateState();
        Assert.IsAssignableFrom<IEntitySyncedDataAnimationState>(state);

        // The BOOLEAN serializer (id 8) is not SNIFFER_STATE (35), so no clock may start.
        var synced = (IEntitySyncedDataAnimationState)state;
        synced.OnSyncedData(0, 8, 1);
        provider.Apply(state, Frame(ageInTicks: 100f), new EntityPose());
        var pose = new EntityPose();
        provider.Apply(state, Frame(ageInTicks: 120f), pose);

        Assert.Equal(0f, pose.Get("head").Rotation.X, 4);
    }
}
