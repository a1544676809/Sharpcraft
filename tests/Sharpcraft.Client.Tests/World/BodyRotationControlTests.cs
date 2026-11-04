// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>Reference <c>BodyRotationControl</c> behaviour on the presentation layer (docs/modules/entity-presentation-state.md).</summary>
public sealed class BodyRotationControlTests
{
    [Fact]
    public void AnIdleHeadTurnBeyondFifteenDegreesDragsTheBodyWithinSeventyFiveDegrees()
    {
        var state = new EntityPresentationState(154);
        state.InitializeRotation(0f);

        state.TickBodyRotation(entityYaw: 0f, headYaw: 100f, moving: false, maxHeadYaw: 75f);

        Assert.Equal(25f, state.BodyYaw, 3);
        Assert.Equal(100f, state.HeadYaw, 3);
    }

    [Fact]
    public void MovingSnapsTheBodyToTheEntityYawAndClampsTheHead()
    {
        var state = new EntityPresentationState(154);
        state.InitializeRotation(0f);

        state.TickBodyRotation(entityYaw: 40f, headYaw: 200f, moving: true, maxHeadYaw: 75f);

        Assert.Equal(40f, state.BodyYaw, 3);
        // rotateIfNecessary(200, 40, 75) -> 40 - clamp(wrap(40-200)) = 40 - (-75) = 115.
        Assert.Equal(115f, state.HeadYaw, 3);
    }

    [Fact]
    public void AStableIdleHeadIsReturnedToFrontRelativeToTheBody()
    {
        var state = new EntityPresentationState(154);
        state.InitializeRotation(0f);

        for (var i = 0; i < 40; i++)
        {
            state.TickBodyRotation(entityYaw: 0f, headYaw: 20f, moving: false, maxHeadYaw: 75f);
        }

        // After the head has been stable for ~10+ ticks the body rotates all the way to face the head.
        Assert.Equal(20f, state.BodyYaw, 1);
    }

    [Fact]
    public void WithoutBodyControlTheBodyNeverRotatesToTheHead()
    {
        // Guard that the control is what produces the follow behaviour: a zero-head-difference tick leaves it put.
        var state = new EntityPresentationState(154);
        state.InitializeRotation(30f);

        state.TickBodyRotation(entityYaw: 30f, headYaw: 30f, moving: false, maxHeadYaw: 75f);

        Assert.Equal(30f, state.BodyYaw, 3);
    }
}
