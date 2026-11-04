// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Warden animation coverage ported from the reference WardenAnimation definitions and WardenModel.setupAnim. Pins the
/// reference clip lengths/looping, representative keyframe values (including position Y inversion and degree→radian
/// conversion), the generic walk/head-look signs, and the part-name contract with the Warden model.
/// </summary>
public sealed class WardenAnimationTests
{
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
        float yaw = 0f) =>
        new(
            WardenModelDefinition.WardenEntityTypeId,
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
            false);

    [Fact]
    public void ClipsDeclareReferenceLengthsAndAreOneShot()
    {
        Assert.Equal(0.33333f, WardenAnimations.Attack.LengthInSeconds);
        Assert.False(WardenAnimations.Attack.Looping);

        Assert.Equal(3f, WardenAnimations.SonicBoom.LengthInSeconds);
        Assert.False(WardenAnimations.SonicBoom.Looping);

        Assert.Equal(5f, WardenAnimations.Dig.LengthInSeconds);
        Assert.False(WardenAnimations.Dig.Looping);

        Assert.Equal(6.68f, WardenAnimations.Emerge.LengthInSeconds);
        Assert.False(WardenAnimations.Emerge.Looping);

        Assert.Equal(4.2f, WardenAnimations.Roar.LengthInSeconds);
        Assert.False(WardenAnimations.Roar.Looping);

        Assert.Equal(4.16f, WardenAnimations.Sniff.LengthInSeconds);
        Assert.False(WardenAnimations.Sniff.Looping);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheWardenModel()
    {
        var modelParts = WardenModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var definition in new[]
                 {
                     WardenAnimations.Attack, WardenAnimations.SonicBoom, WardenAnimations.Dig,
                     WardenAnimations.Emerge, WardenAnimations.Roar, WardenAnimations.Sniff
                 })
        {
            foreach (var partName in definition.BoneAnimations.Keys)
            {
                Assert.Contains(partName, modelParts);
            }
        }
    }

    [Fact]
    public void EmergeMatchesTheReferenceStartAndMidKeyframes()
    {
        var start = Apply(WardenAnimations.EmergeBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("body").Rotation);
        // posVec(0, -63, 0) inverts Y: the body starts deeply underground.
        Assert.Equal(new Vector3(0f, 63f, 0f), start.Get("body").Position);
        Assert.Equal(Vector3.Zero, start.Get("head").Position);

        var early = Apply(WardenAnimations.EmergeBaked, 0.52f);
        Assert.Equal(Rad(-22.5f), early.Get("body").Rotation.Z, 4);
        Assert.Equal(new Vector3(0f, 56f, 0f), early.Get("body").Position);
        Assert.Equal(new Vector3(-8f, 11f, 0f), early.Get("head").Position);

        var mid = Apply(WardenAnimations.EmergeBaked, 3.92f);
        Assert.Equal(Rad(35f), mid.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(-7.5f), mid.Get("body").Rotation.Z, 4);
        Assert.Equal(new Vector3(0f, 11f, 0f), mid.Get("body").Position);
        Assert.Equal(new Vector3(0f, 9.28f, 2.48f), mid.Get("right_leg").Position);
    }

    [Fact]
    public void SonicBoomMatchesTheReferenceMidKeyframes()
    {
        var start = Apply(WardenAnimations.SonicBoomBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("body").Rotation);
        Assert.Equal(Vector3.Zero, start.Get("right_ribcage").Rotation);

        var windUp = Apply(WardenAnimations.SonicBoomBaked, 1.625f);
        Assert.Equal(Rad(55f), windUp.Get("body").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, 4f, -1f), windUp.Get("body").Position);
        Assert.Equal(Rad(80f), Apply(WardenAnimations.SonicBoomBaked, 1.75f).Get("head").Rotation.X, 4);

        var openRibs = Apply(WardenAnimations.SonicBoomBaked, 1.875f);
        Assert.Equal(Rad(125f), openRibs.Get("right_ribcage").Rotation.Y, 4);
        Assert.Equal(Rad(-125f), openRibs.Get("left_ribcage").Rotation.Y, 4);
    }

    [Fact]
    public void AttackMatchesTheReferenceKeyframes()
    {
        var strike = Apply(WardenAnimations.AttackBaked, 0.0417f);
        Assert.Equal(Rad(-22.5f), strike.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(22.5f), strike.Get("head").Rotation.X, 4);
        Assert.Equal(Rad(-120.36119f), strike.Get("right_arm").Rotation.X, 3);
        Assert.Equal(Rad(40.78947f), strike.Get("right_arm").Rotation.Y, 3);
        Assert.Equal(Rad(-20.94102f), strike.Get("right_arm").Rotation.Z, 3);
        Assert.Equal(new Vector3(4f, 0f, 5f), strike.Get("right_arm").Position);

        var followThrough = Apply(WardenAnimations.AttackBaked, 0.1667f);
        Assert.Equal(Rad(-90f), followThrough.Get("right_arm").Rotation.X, 4);
        Assert.Equal(Rad(-45f), followThrough.Get("right_arm").Rotation.Y, 4);

        var recoil = Apply(WardenAnimations.AttackBaked, 0.25f);
        Assert.Equal(Rad(-30.17493f), recoil.Get("head").Rotation.X, 3);
        // posVec(0, -2, -2) inverts Y only.
        Assert.Equal(new Vector3(0f, 2f, -2f), recoil.Get("head").Position);
    }

    [Fact]
    public void DigMatchesTheReferenceKeyframesIncludingLinearTail()
    {
        var bite = Apply(WardenAnimations.DigBaked, 0.5f);
        Assert.Equal(Rad(50f), bite.Get("body").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, 16.48454f, -6.5784f), bite.Get("body").Position);
        Assert.Equal(Rad(113.27f), bite.Get("right_leg").Rotation.X, 3);
        Assert.Equal(Rad(114.98f), bite.Get("left_leg").Rotation.X, 3);

        var finish = Apply(WardenAnimations.DigBaked, 4.5f);
        Assert.Equal(Rad(147.5f), finish.Get("body").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, 40f, -8f), finish.Get("body").Position);
        Assert.Equal(Rad(90f), finish.Get("right_leg").Rotation.X, 3);
        Assert.Equal(Rad(90f), finish.Get("left_leg").Rotation.X, 3);
    }

    [Fact]
    public void RoarAndSniffMatchTheReferenceKeyframes()
    {
        var roar = Apply(WardenAnimations.RoarBaked, 1.24f);
        Assert.Equal(Rad(-25f), roar.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(-32.5f), roar.Get("head").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, 1f, 3f), roar.Get("body").Position);

        var headShake = Apply(WardenAnimations.RoarBaked, 2.44f);
        Assert.Equal(Rad(26f), headShake.Get("head").Rotation.Z, 4);

        var sniffBody = Apply(WardenAnimations.SniffBaked, 0.56f);
        Assert.Equal(Rad(17.5f), sniffBody.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(32.5f), sniffBody.Get("body").Rotation.Y, 4);

        var sniffHead = Apply(WardenAnimations.SniffBaked, 0.68f);
        Assert.Equal(Rad(40f), sniffHead.Get("head").Rotation.Y, 4);

        var sniffTurn = Apply(WardenAnimations.SniffBaked, 2.8f);
        Assert.Equal(Rad(-30f), sniffTurn.Get("body").Rotation.Y, 4);
    }

    [Fact]
    public void WalkDrivesLegsInOppositePhaseAndMovesTheHeadBackwards()
    {
        const float steps = 1f;
        const float speed = 0.1f;
        var pose = new EntityPose();
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        provider.Apply(provider.CreateState(), Frame(walkPos: steps, walkSpeed: speed), pose);

        // adjustedPos = 1 * 0.8662 -> cos > 0, sin > 0; speedModifier = min(0.5, 3 * 0.1) = 0.3.
        var leftLeg = pose.Get("left_leg").Rotation.X;
        var rightLeg = pose.Get("right_leg").Rotation.X;
        Assert.True(leftLeg > 0f, $"left leg should swing forward, was {leftLeg}");
        Assert.True(rightLeg < 0f, $"right leg should swing backward, was {rightLeg}");
        Assert.Equal(-leftLeg, rightLeg, 4);

        // The head counter-rotates through the same sine term, so its walk lean is positive at this phase.
        Assert.True(pose.Get("head").Rotation.Z > 0f);
    }

    [Fact]
    public void WalkSpeedIsCappedAtOneHalf()
    {
        var fast = new EntityPose();
        var faster = new EntityPose();
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        var state = provider.CreateState();
        // 3 * 0.2 = 0.6 and 3 * 10 = 30 both clamp to 0.5, so the leg pose is identical.
        provider.Apply(state, Frame(walkPos: 1f, walkSpeed: 0.2f), fast);
        provider.Apply(state, Frame(walkPos: 1f, walkSpeed: 10f), faster);
        Assert.Equal(fast.Get("left_leg").Rotation.X, faster.Get("left_leg").Rotation.X, 4);
        Assert.Equal(fast.Get("right_leg").Rotation.X, faster.Get("right_leg").Rotation.X, 4);
    }

    [Fact]
    public void HeadLookUsesPitchForXAndYawForYInRadians()
    {
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        var state = provider.CreateState();

        var looking = new EntityPose();
        provider.Apply(state, Frame(pitch: 30f, yaw: 45f), looking);
        Assert.Equal(Rad(30f), looking.Get("head").Rotation.X, 4);
        Assert.Equal(Rad(45f), looking.Get("head").Rotation.Y, 4);

        var downcast = new EntityPose();
        provider.Apply(state, Frame(pitch: -20f, yaw: -30f), downcast);
        Assert.Equal(Rad(-20f), downcast.Get("head").Rotation.X, 4);
        Assert.Equal(Rad(-30f), downcast.Get("head").Rotation.Y, 4);
    }

    [Fact]
    public void IdleWobbleIsDrivenByAgeInTicks()
    {
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        var state = provider.CreateState();

        // age = 0 -> cos = 1, sin = 0: head.zRot and body.xRot get the cosine term.
        var atZero = new EntityPose();
        provider.Apply(state, Frame(ageInTicks: 0f), atZero);
        Assert.Equal(0.06f, atZero.Get("head").Rotation.Z, 4);
        Assert.Equal(0.025f, atZero.Get("body").Rotation.X, 4);
        Assert.Equal(0f, atZero.Get("head").Rotation.X, 4);

        // scaledAge = pi/6 ≈ 0.5236 when age = pi/6 / 0.1; sin is 0.5, so head.xRot ≈ 0.06 * 0.5.
        var scaled = (MathF.PI / 6f) / 0.1f;
        var later = new EntityPose();
        provider.Apply(state, Frame(ageInTicks: scaled), later);
        Assert.Equal(0.06f * MathF.Sin(scaled * 0.1f), later.Get("head").Rotation.X, 4);
        Assert.Equal(0.025f * MathF.Sin(scaled * 0.1f), later.Get("body").Rotation.Z, 4);
    }

    [Fact]
    public void TendrilsRestAtZeroUntilTheRenderStateSuppliesAFactor()
    {
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 7f), pose);

        Assert.Equal(Vector3.Zero, pose.Get("left_tendril").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("right_tendril").Rotation);
    }

    [Fact]
    public void ProviderExposesEveryReferenceClipAndItsTypeId()
    {
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        Assert.Equal(146, provider.TypeId);
        Assert.Equal(
            new[] { "attack", "sonic_boom", "dig", "emerge", "roar", "sniff" }.OrderBy(name => name, StringComparer.Ordinal),
            provider.Clips.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ApplyClipPlaysEachNamedClipAndIgnoresUnknownOnes()
    {
        var provider = new WardenAnimationProvider(WardenModelDefinition.WardenEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();

        foreach (var clip in provider.Clips)
        {
            var pose = new EntityPose();
            provider.ApplyClip(state, frame, clip, 0f, pose);
            Assert.NotEmpty(pose.Offsets);
        }

        var emergePose = new EntityPose();
        provider.ApplyClip(state, frame, "emerge", 0.52f, emergePose);
        Assert.Equal(Rad(-22.5f), emergePose.Get("body").Rotation.Z, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }
}
