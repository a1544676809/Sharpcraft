// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Reference values for the STAB swing offset, recomputed from the 26.3 formulas independently of the implementation.
/// </summary>
public sealed class SpearAnimationsTests
{
    [Theory]
    [InlineData(0.00f, 0.05f, 0.2f, 0f)]
    [InlineData(0.05f, 0.05f, 0.2f, 0f)]
    [InlineData(0.125f, 0.05f, 0.2f, 0.5f)]
    [InlineData(0.20f, 0.05f, 0.2f, 1f)]
    [InlineData(0.50f, 0.05f, 0.2f, 1f)]
    public void ProgressClampsTheInverseLerp(float time, float start, float end, float expected) =>
        Assert.Equal(expected, SpearAnimations.Progress(time, start, end), 6);

    [Fact]
    public void EasingMatchesTheReferenceFormulas()
    {
        Assert.Equal(0.25f, SpearAnimations.InQuad(0.5f), 6);
        Assert.Equal(0f, SpearAnimations.InOutExpo(0f), 6);
        Assert.Equal(0.5f, SpearAnimations.InOutExpo(0.5f), 6);
        Assert.Equal(1f, SpearAnimations.InOutExpo(1f), 6);
        // 0.25 < 0.5: 2^(20*0.25-10)/2 = 2^-5/2 = 1/64
        Assert.Equal(1f / 64f, SpearAnimations.InOutExpo(0.25f), 6);
        // 0.75 >= 0.5: (2 - 2^(-5))/2
        Assert.Equal((2f - (1f / 32f)) / 2f, SpearAnimations.InOutExpo(0.75f), 6);
    }

    [Fact]
    public void NoOffsetBeforeTheSwingStarts()
    {
        Assert.Equal((0f, 0f), SpearAnimations.ThirdPersonAttackItem(0f, 1f));
        Assert.Equal((0f, 0f), SpearAnimations.ThirdPersonAttackItem(-0.5f, 1f));
    }

    [Fact]
    public void PeakAttackRotationMatchesTheReference()
    {
        // At swing 0.2 the attack term saturates (inQuad(1) = 1) while retract has not started, so the
        // rotation is the full -70 degrees about X and the jet translation is at full forward reach.
        var (rotation, translation) = SpearAnimations.ThirdPersonAttackItem(0.2f, 0.7f);
        Assert.Equal(70f, rotation, 5);
        Assert.Equal(0.7f, translation, 5);
    }

    [Fact]
    public void StabSwingRotatesTheHeldItemPart()
    {
        var pose = new EntityPose();
        SpearAnimations.ApplyThirdPersonAttackItem(0.2f, SpearAnimations.StabSwingType, 0f, pose);

        var offset = pose.Get("held_item");
        // 70 degrees about -X, supplied as +70 degrees of radians because the mesher subtracts the X offset.
        Assert.Equal(70f * (MathF.PI / 180f), offset.Rotation.X, 5);
        Assert.Equal(0f, offset.Rotation.Y, 5);
        Assert.Equal(0f, offset.Rotation.Z, 5);
    }

    [Fact]
    public void WhackSwingsLeaveTheHeldItemAlone()
    {
        var pose = new EntityPose();
        SpearAnimations.ApplyThirdPersonAttackItem(0.2f, swingType: 1, 0f, pose);
        Assert.Equal(PoseOffset.Zero, pose.Get("held_item"));
    }
    [Fact]
    public void LateSwingReturnsToTheNeutralOffset()
    {
        // At swing 1.0 attack = 1 and retract = 1, so the rotation returns to zero.
        var (rotation, translation) = SpearAnimations.ThirdPersonAttackItem(1f, 0.7f);
        Assert.Equal(0f, rotation, 5);
        Assert.Equal(0f, translation, 5);
    }
}
