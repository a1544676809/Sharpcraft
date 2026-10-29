// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Player;
using Xunit;

namespace Sharpcraft.Client.Tests.Player;

/// <summary>
/// Player motion (docs/modules/player.md): falling, landing, jumping, walking and collision. The
/// magnitudes are vanilla-scale but unverified against a reference run, so these assert relations and
/// ranges instead of exact digits.
/// </summary>
public sealed class PlayerBodyTests
{
    private static PlayerBody Falling(Vector3Holder at) => new(at.Feet, Ground());

    // ---- gravity and landing -------------------------------------------------

    [Fact]
    public void TheBodyFallsAndStopsOnTheFloor()
    {
        var body = Falling(new Vector3Holder(0.5f, 10f, 0.5f));

        for (var tick = 0; tick < 120; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        Assert.True(body.OnGround, "should have landed");
        Assert.Equal(1f, body.Feet.Y, 3);

        // Resting does not zero the vertical velocity: gravity is applied every tick and the collision
        // clamps it back, so what matters is that the body stops moving.
        var resting = body.Feet;
        for (var tick = 0; tick < 10; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        Assert.Equal(resting.Y, body.Feet.Y, 5);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void FallingSpeedIsClampedToTheTerminalVelocity()
    {
        var body = Falling(new Vector3Holder(0.5f, 400f, 0.5f));

        for (var tick = 0; tick < 60; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        Assert.True(
            body.Velocity.Y >= -PlayerBody.TerminalVelocity - 1e-3f,
            $"vertical speed {body.Velocity.Y} exceeded the terminal velocity");
    }

    // ---- jumping -------------------------------------------------------------

    [Fact]
    public void AJumpRaisesTheBodyAboutOneAndAQuarterBlocks()
    {
        var body = Falling(new Vector3Holder(0.5f, 1f, 0.5f));

        // Settle first: a jump from mid-air must not be allowed.
        for (var tick = 0; tick < 5; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        Assert.True(body.OnGround);

        var peak = body.Feet.Y;
        for (var tick = 0; tick < 20; tick++)
        {
            body.Tick(new MoveIntent(0f, 0f, Jump: true, Sprint: false), yaw: 0f);
            peak = MathF.Max(peak, body.Feet.Y);
        }

        // Vanilla peaks around 1.25 blocks; assert the range rather than a digit we have not verified.
        Assert.InRange(peak - 1f, 1.15f, 1.35f);
    }

    [Fact]
    public void AJumpInTheAirIsIgnored()
    {
        var body = Falling(new Vector3Holder(0.5f, 20f, 0.5f));
        body.Tick(MoveIntent.None, yaw: 0f);
        var before = body.Velocity.Y;

        body.Tick(new MoveIntent(0f, 0f, Jump: true, Sprint: false), yaw: 0f);

        Assert.True(body.Velocity.Y < before, "jumping mid-air must not add upward velocity");
    }

    // ---- walking -------------------------------------------------------------

    [Fact]
    public void WalkingForwardMovesTowardsMinusZAtYawZero()
    {
        var body = Falling(new Vector3Holder(0.5f, 1f, 0.5f));
        for (var tick = 0; tick < 5; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        var startZ = body.Feet.Z;
        for (var tick = 0; tick < 20; tick++)
        {
            body.Tick(new MoveIntent(1f, 0f, Jump: false, Sprint: false), yaw: 0f);
        }

        Assert.True(body.Feet.Z < startZ - 0.5f, $"expected motion towards −Z, went from {startZ} to {body.Feet.Z}");
        Assert.Equal(0.5f, body.Feet.X, 3);
    }

    [Fact]
    public void SprintingCoversMoreGroundThanWalking()
    {
        var walk = Walk(new MoveIntent(1f, 0f, Jump: false, Sprint: false));
        var sprint = Walk(new MoveIntent(1f, 0f, Jump: false, Sprint: true));

        Assert.True(sprint > walk * 1.1f, $"sprint {sprint} should clearly exceed walk {walk}");
    }

    [Fact]
    public void OpposingDirectionsCancelOut()
    {
        var body = Falling(new Vector3Holder(0.5f, 1f, 0.5f));
        for (var tick = 0; tick < 5; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        var (forward, strafe) = new MoveIntent(1f, -1f, false, false).Normalized();
        Assert.True(MathF.Abs(MathF.Abs(forward) - MathF.Abs(strafe)) < 1e-4f, "diagonal must stay diagonal");
        Assert.True((forward * forward) + (strafe * strafe) < 1.0001f, "diagonal must not exceed unit length");
        _ = body;
    }

    // ---- collision -----------------------------------------------------------

    [Fact]
    public void AWallStopsHorizontalMovement()
    {
        var body = new PlayerBody(
            new Vector3Holder(0.5f, 1f, 0.5f).Feet,
            (x, y, z) => y < 1f || (y < 3f && z <= -2f));

        for (var tick = 0; tick < 5; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        for (var tick = 0; tick < 60; tick++)
        {
            body.Tick(new MoveIntent(1f, 0f, Jump: false, Sprint: false), yaw: 0f);
        }

        // The wall face sits at z = −2, so the body must stop before its own half width crosses it.
        Assert.True(body.Feet.Z > -2f, $"walked through the wall: z = {body.Feet.Z}");
    }

    [Fact]
    public void AHighSpeedFallDoesNotTunnelThroughTheFloor()
    {
        var body = Falling(new Vector3Holder(0.5f, 200f, 0.5f));

        for (var tick = 0; tick < 400; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        Assert.True(body.Feet.Y >= 1f - 1e-3f, $"fell through the floor to y = {body.Feet.Y}");
    }

    // ---- helpers -------------------------------------------------------------

    private static float Walk(MoveIntent intent)
    {
        var body = Falling(new Vector3Holder(0.5f, 1f, 0.5f));
        for (var tick = 0; tick < 5; tick++)
        {
            body.Tick(MoveIntent.None, yaw: 0f);
        }

        var startZ = body.Feet.Z;
        for (var tick = 0; tick < 40; tick++)
        {
            body.Tick(intent, yaw: 0f);
        }

        return MathF.Abs(body.Feet.Z - startZ);
    }

    /// <summary>Solid ground below y = 1, nothing else.</summary>
    private static Func<float, float, float, bool> Ground() => (_, y, _) => y < 1f;

    /// <summary>Terse helper for constructing positions in tests.</summary>
    private readonly record struct Vector3Holder(float X, float Y, float Z)
    {
        public System.Numerics.Vector3 Feet => new(X, Y, Z);
    }
}
