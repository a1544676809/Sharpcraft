// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>Equivalence checks for <see cref="ElytraAnimationState"/> against the reference formulas.</summary>
public sealed class ElytraAnimationStateTests
{
    [Fact]
    public void StandingConvergesToPiOver12AndMinusPiOver12()
    {
        var state = new ElytraAnimationState();
        for (var i = 0; i < 200; i++)
        {
            state.Tick(fallFlying: false, crouching: false, velocityX: 0f, velocityY: 0f, velocityZ: 0f);
        }

        Assert.Equal(MathF.PI / 12f, state.GetRotX(1f), 3);
        Assert.Equal(0f, state.GetRotY(1f), 3);
        Assert.Equal(-MathF.PI / 12f, state.GetRotZ(1f), 3);
    }

    [Fact]
    public void CrouchingConvergesToTwoPiOver9AndMinusPiOver4()
    {
        var state = new ElytraAnimationState();
        for (var i = 0; i < 300; i++)
        {
            state.Tick(fallFlying: false, crouching: true, velocityX: 0f, velocityY: 0f, velocityZ: 0f);
        }

        Assert.Equal(MathF.PI * 2f / 9f, state.GetRotX(1f), 3);
        Assert.Equal(0.08726646f, state.GetRotY(1f), 3);
        Assert.Equal(-MathF.PI / 4f, state.GetRotZ(1f), 3);
    }

    [Fact]
    public void FallFlyingWithHorizontalMotionTucksWings()
    {
        // Velocity almost horizontal (vy ~ 0) -> ratio ~ 1 -> zRot target -pi/2 (tucked).
        var state = new ElytraAnimationState();
        for (var i = 0; i < 300; i++)
        {
            state.Tick(fallFlying: true, crouching: false, velocityX: 1f, velocityY: -1e-9f, velocityZ: 0f);
        }

        Assert.Equal(MathF.PI / 9f, state.GetRotX(1f), 3);
        Assert.Equal(-MathF.PI / 2f, state.GetRotZ(1f), 3);
    }

    [Fact]
    public void PartialTickInterpolatesBetweenOldAndCurrent()
    {
        var state = new ElytraAnimationState();
        state.Tick(false, false, 0f, 0f, 0f); // rot 0 -> 0.3*(pi/12); Old = 0
        var atZero = state.GetRotX(0f);
        var atOne = state.GetRotX(1f);
        var atHalf = state.GetRotX(0.5f);
        Assert.Equal(0f, atZero, 4);
        Assert.True(atOne > atZero);
        Assert.Equal((atZero + atOne) * 0.5f, atHalf, 4);
    }
}
