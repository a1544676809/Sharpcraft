// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Copper Golem animation coverage ported from the reference <c>CopperGolemAnimation</c> definitions and
/// <c>CopperGolemModel.setupAnim</c>. Pins the reference clip lengths/looping, representative keyframe values (including
/// position Y inversion, degree→radian conversion and additive scale), the state→clip routing driven by the synced
/// <c>CopperGolemState</c> (serializer id 37), and the part-name contract with the Copper Golem model.
/// </summary>
public sealed class CopperGolemAnimationTests
{
    private static readonly int TypeId = CopperGolemModelDefinition.CopperGolemEntityTypeId;

    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static EntityPose Apply(KeyframeAnimation animation, float seconds)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, 1f, pose);
        return pose;
    }

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float pitch = 0f,
        float yaw = 0f,
        bool mainHandItem = false,
        bool offHandItem = false) =>
        new(
            TypeId,
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
            0f,
            mainHandItem,
            offHandItem);

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    /// <summary>Compares every part either pose mentions, so the extra zero-offset walk/head entries are tolerated.</summary>
    private static void AssertPoseMatches(EntityPose expected, EntityPose actual)
    {
        var parts = new HashSet<string>(expected.Offsets.Keys, StringComparer.Ordinal);
        foreach (var part in actual.Offsets.Keys)
        {
            parts.Add(part);
        }

        foreach (var part in parts)
        {
            var e = expected.Get(part);
            var a = actual.Get(part);
            Assert.True(Vector3.Distance(e.Position, a.Position) < 1e-5f, $"position mismatch on '{part}'");
            Assert.True(Vector3.Distance(e.Rotation, a.Rotation) < 1e-5f, $"rotation mismatch on '{part}'");
            Assert.True(Vector3.Distance(e.Scale, a.Scale) < 1e-5f, $"scale mismatch on '{part}'");
        }
    }

    [Fact]
    public void ClipsDeclareReferenceLengthsAndLooping()
    {
        Assert.Equal(0.8333f, CopperGolemAnimations.COPPER_GOLEM_WALK.LengthInSeconds);
        Assert.True(CopperGolemAnimations.COPPER_GOLEM_WALK.Looping);

        Assert.Equal(3.5f, CopperGolemAnimations.COPPER_GOLEM_IDLE.LengthInSeconds);
        Assert.False(CopperGolemAnimations.COPPER_GOLEM_IDLE.Looping);

        Assert.Equal(0.8333f, CopperGolemAnimations.COPPER_GOLEM_WALK_ITEM.LengthInSeconds);
        Assert.True(CopperGolemAnimations.COPPER_GOLEM_WALK_ITEM.Looping);

        Assert.Equal(3.0f, CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP.LengthInSeconds);
        Assert.True(CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP.Looping);

        Assert.Equal(3.0f, CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP.LengthInSeconds);
        Assert.True(CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP.Looping);

        Assert.Equal(3.0f, CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET.LengthInSeconds);
        Assert.True(CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET.Looping);

        Assert.Equal(3.0f, CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET.LengthInSeconds);
        Assert.True(CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET.Looping);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheCopperGolemModel()
    {
        var modelParts = CopperGolemModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var definition in new[]
                 {
                     CopperGolemAnimations.COPPER_GOLEM_WALK,
                     CopperGolemAnimations.COPPER_GOLEM_IDLE,
                     CopperGolemAnimations.COPPER_GOLEM_WALK_ITEM,
                     CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_ITEM_DROP,
                     CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_ITEM_NODROP,
                     CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_NOITEM_GET,
                     CopperGolemAnimations.COPPER_GOLEM_CHEST_INTERACTION_NOITEM_NOGET
                 })
        {
            foreach (var partName in definition.BoneAnimations.Keys)
            {
                Assert.Contains(partName, modelParts);
            }
        }
    }

    [Fact]
    public void WalkMatchesTheReferenceKeyframes()
    {
        var body = Apply(CopperGolemAnimations.WalkBaked, 0.2083f);
        AssertVec(KeyframeAnimations.DegreeVec(10f, -1.87f, -10f), body.Get("body").Rotation);

        var arm = Apply(CopperGolemAnimations.WalkBaked, 0.4167f);
        AssertVec(KeyframeAnimations.DegreeVec(-80f, 0f, 0f), arm.Get("right_arm").Rotation);
        AssertVec(KeyframeAnimations.DegreeVec(-60f, 0f, 0f), arm.Get("left_leg").Rotation);
        AssertVec(KeyframeAnimations.DegreeVec(60f, 0f, 0f), arm.Get("right_leg").Rotation);
    }

    [Fact]
    public void IdleMatchesTheReferenceKeyframes()
    {
        var sway = Apply(CopperGolemAnimations.IdleBaked, 0.625f);
        AssertVec(KeyframeAnimations.DegreeVec(0f, 35f, 0f), sway.Get("body").Rotation);

        var spin = Apply(CopperGolemAnimations.IdleBaked, 1.5f);
        AssertVec(KeyframeAnimations.DegreeVec(0f, 300f, 0f), spin.Get("head").Rotation);

        var tilt = Apply(CopperGolemAnimations.IdleBaked, 2.7083f);
        AssertVec(KeyframeAnimations.DegreeVec(-25f, 300f, 0f), tilt.Get("head").Rotation);
    }

    [Fact]
    public void WalkItemMatchesTheReferenceKeyframes()
    {
        var start = Apply(CopperGolemAnimations.WalkItemBaked, 0f);
        AssertVec(KeyframeAnimations.DegreeVec(-59.78638f, -6.49053f, -3.76613f), start.Get("right_arm").Rotation);
        // posVec(-0.21129, -0.0212, -0.07004) inverts Y.
        AssertVec(KeyframeAnimations.PosVec(-0.21129f, -0.0212f, -0.07004f), start.Get("left_arm").Position);
    }

    [Fact]
    public void ChestInteractionItemDropMatchesTheReferenceKeyframes()
    {
        var crouch = Apply(CopperGolemAnimations.ItemDropBaked, 0.2083f);
        AssertVec(KeyframeAnimations.PosVec(0f, 0.6f, 0f), crouch.Get("body").Position);

        var windup = Apply(CopperGolemAnimations.ItemDropBaked, 0.4583f);
        AssertVec(KeyframeAnimations.DegreeVec(-117.82767f, 2.94538f, 0.22703f), windup.Get("right_arm").Rotation);

        var reach = Apply(CopperGolemAnimations.ItemDropBaked, 1.6667f);
        AssertVec(KeyframeAnimations.DegreeVec(9.73588f, -1.93433f, -3.73384f), reach.Get("head").Rotation);
    }

    [Fact]
    public void ChestInteractionItemNoDropMatchesTheReferenceKeyframes()
    {
        var twirl = Apply(CopperGolemAnimations.ItemNoDropBaked, 2.9583f);
        AssertVec(KeyframeAnimations.DegreeVec(0f, -360f, 0f), twirl.Get("head").Rotation);

        var arm = Apply(CopperGolemAnimations.ItemNoDropBaked, 2.1667f);
        AssertVec(KeyframeAnimations.DegreeVec(-4.31506f, 6.54961f, 13.21388f), arm.Get("right_arm").Rotation);

        // The item_nodrop clip carries a neutral 1x scale track (additive scale is zero).
        var start = Apply(CopperGolemAnimations.ItemNoDropBaked, 0f);
        AssertVec(Vector3.Zero, start.Get("left_leg").Scale);
    }

    [Fact]
    public void ChestInteractionNoItemGetMatchesTheReferenceKeyframes()
    {
        var grab = Apply(CopperGolemAnimations.NoItemGetBaked, 0.1667f);
        AssertVec(KeyframeAnimations.DegreeVec(-7.38733f, 1.29876f, 9.91615f), grab.Get("right_arm").Rotation);

        var look = Apply(CopperGolemAnimations.NoItemGetBaked, 2.1667f);
        AssertVec(KeyframeAnimations.DegreeVec(0f, 7.5f, 0f), look.Get("head").Rotation);
    }

    [Fact]
    public void ChestInteractionNoItemNoGetMatchesTheReferenceKeyframes()
    {
        var arm = Apply(CopperGolemAnimations.NoItemNoGetBaked, 2.0f);
        AssertVec(KeyframeAnimations.DegreeVec(-56.46674f, 3.3853f, 14.45894f), arm.Get("right_arm").Rotation);

        var settle = Apply(CopperGolemAnimations.NoItemNoGetBaked, 2.5417f);
        AssertVec(KeyframeAnimations.DegreeVec(2.41f, -0.65f, -5.01f), settle.Get("left_arm").Rotation);
    }

    [Fact]
    public void LoopingClipsWrapAndIdleClampsAtItsEnd()
    {
        // Walk loops: length + 0.2083 s wraps back to the 0.2083 s pose.
        var wrapped = Apply(CopperGolemAnimations.WalkBaked, CopperGolemAnimations.COPPER_GOLEM_WALK.LengthInSeconds + 0.2083f);
        var direct = Apply(CopperGolemAnimations.WalkBaked, 0.2083f);
        AssertVec(direct.Get("body").Rotation, wrapped.Get("body").Rotation, 3);

        // Idle does not loop: past the end it clamps to the 3.5 s keyframe (head y = 360 deg), not the 0.125 s pose.
        var clamped = Apply(CopperGolemAnimations.IdleBaked, CopperGolemAnimations.COPPER_GOLEM_IDLE.LengthInSeconds + 0.125f);
        AssertVec(KeyframeAnimations.DegreeVec(0f, 360f, 0f), clamped.Get("head").Rotation);
    }

    [Fact]
    public void ProviderExposesTheCopperGolemTypeIdAndEveryClip()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);

        Assert.Equal(TypeId, provider.TypeId);
        Assert.Equal(
            new[] { "walk", "walk_item", "idle", "item_drop", "item_nodrop", "noitem_get", "noitem_noget" }
                .OrderBy(name => name, StringComparer.Ordinal),
            provider.Clips.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ApplyClipPlaysEveryNamedClipAndIgnoresUnknownOnes()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame();

        foreach (var clip in provider.Clips)
        {
            var pose = new EntityPose();
            provider.ApplyClip(state, in frame, clip, 0.5f, pose);
            Assert.NotEmpty(pose.Offsets);
        }

        var unknown = new EntityPose();
        provider.ApplyClip(state, in frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void ApplyLooksAlongPitchAndYawInRadians()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var pose = new EntityPose();
        var frame = Frame(pitch: 30f, yaw: -45f);

        provider.Apply(provider.CreateState(), in frame, pose);

        AssertVec(new Vector3(Rad(30f), Rad(-45f), 0f), pose.Get("head").Rotation);
    }

    [Fact]
    public void ApplyPlaysTheWalkLoopAtTheReferenceMapping()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        // walkPos 2 -> 0.2 s into the clip; walkSpeed 1 -> target scale min(2.5, 1) = 1.
        var movingFrame = Frame(walkPos: 2f, walkSpeed: 1f);
        var moving = new EntityPose();
        provider.Apply(state, in movingFrame, moving);

        var expected = new EntityPose();
        provider.ApplyClip(state, in movingFrame, "walk", 0.2f, expected);
        AssertPoseMatches(expected, moving);
        Assert.True(MathF.Abs(moving.Get("right_arm").Rotation.X) > 0.1f);
    }

    [Fact]
    public void WalkTargetScaleIsCappedAtOne()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        var moderate = new EntityPose();
        provider.Apply(state, Frame(walkPos: 1f, walkSpeed: 0.2f), moderate); // scale min(0.5, 1) = 0.5
        var extreme = new EntityPose();
        provider.Apply(state, Frame(walkPos: 1f, walkSpeed: 100f), extreme); // scale min(250, 1) = 1

        // The extreme case is exactly the full-scale pose; the moderate case is half of it.
        Assert.Equal(moderate.Get("right_arm").Rotation.X * 2f, extreme.Get("right_arm").Rotation.X, 4);
    }

    [Theory]
    [InlineData(1, "noitem_get", 0.5f)]   // GETTING_ITEM
    [InlineData(3, "item_drop", 0.5f)]    // DROPPING_ITEM
    [InlineData(2, "noitem_noget", 2.25f)] // GETTING_NO_ITEM
    [InlineData(4, "item_nodrop", 2.25f)] // DROPPING_NO_ITEM
    public void SyncedStateRoutesToTheMatchingClip(int ordinal, string clip, float seconds)
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        ((IEntitySyncedDataAnimationState)state).OnSyncedData(index: 0, serializerId: 37, value: ordinal);

        const int startTick = 100;
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(startTick, 0f));

        // One tick is 50 ms, so age = startTick + seconds * 20 puts the clock at `seconds` into the clip.
        var frame = Frame(ageInTicks: startTick + (seconds * 20f));
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);

        // The routed clip must reproduce the named clip's pose at the same elapsed time.
        var expectedSeconds = (long)((frame.AgeInTicks - startTick) * 50f) / 1000f;
        var expected = new EntityPose();
        provider.ApplyClip(state, in frame, clip, expectedSeconds, expected);

        AssertPoseMatches(expected, pose);
        Assert.NotEmpty(pose.Offsets);
        Assert.Contains(pose.Offsets.Values, offset => offset.Rotation != Vector3.Zero || offset.Position != Vector3.Zero);
    }

    [Fact]
    public void UnknownSyncedSerializerIsIgnoredAndDefaultsToIdle()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        // A different serializer must not change the default IDLE state.
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(index: 0, serializerId: 99, value: 1);
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(1, 0f));

        var frame = Frame(ageInTicks: 1f);
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);

        // IDLE on its first tick only schedules the next idle start; no interaction clip has started yet.
        AssertVec(Vector3.Zero, pose.Get("right_arm").Rotation);
        AssertVec(Vector3.Zero, pose.Get("left_arm").Rotation);
        AssertVec(Vector3.Zero, pose.Get("body").Rotation);
    }

    [Fact]
    public void IdleStartsAfterItsScheduledDelayAndThenDrivesTheBody()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        var started = false;
        for (var gameTime = 1; gameTime <= 320 && !started; gameTime++)
        {
            ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(gameTime, 0f));

            var frame = Frame(ageInTicks: gameTime);
            var pose = new EntityPose();
            provider.Apply(state, in frame, pose);

            if (MathF.Abs(pose.Get("body").Rotation.Y) > 0.3f)
            {
                started = true;
            }
        }

        Assert.True(started, "the idle clip should start within the 200-240 tick schedule window");
    }

    [Fact]
    public void StateArrivesFromSerializerIdThirtySevenRegardlessOfIndex()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        // Any entity-data index carrying serializer 37 must be interpreted as the Copper Golem state.
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(index: 42, serializerId: CopperGolemAnimationProvider.CopperGolemStateSerializerId, value: 3);
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(0, 0f));

        var frame = Frame(ageInTicks: 10f); // 0.5 s into the clip
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);

        var expected = new EntityPose();
        provider.ApplyClip(state, in frame, "item_drop", 0.5f, expected);
        AssertPoseMatches(expected, pose);
    }

    [Fact]
    public void HoldingAnItemSelectsTheHeldItemWalkAndClampsTheArms()
    {
        var provider = new CopperGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        var holding = new EntityPose();
        provider.Apply(state, Frame(walkPos: 3f, walkSpeed: 1f, mainHandItem: true), holding);

        // poseHeldItemArmsIfStill: right arm x <= -50deg, y <= -6.5deg; left arm y >= +6.5deg, z >= +3.7deg.
        Assert.True(holding.Get("right_arm").Rotation.X <= -0.8726f, $"right_arm.x={holding.Get("right_arm").Rotation.X}");
        Assert.True(holding.Get("right_arm").Rotation.Y <= -0.1134f, $"right_arm.y={holding.Get("right_arm").Rotation.Y}");
        Assert.True(holding.Get("left_arm").Rotation.Y >= 0.1134f, $"left_arm.y={holding.Get("left_arm").Rotation.Y}");

        var plain = new EntityPose();
        provider.Apply(state, Frame(walkPos: 3f, walkSpeed: 1f), plain);
        Assert.True(
            MathF.Abs(plain.Get("right_arm").Rotation.X - holding.Get("right_arm").Rotation.X) > 1e-3f,
            "the held-item walk should differ from the plain walk");
    }
}
