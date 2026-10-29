// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Common.Tests.Entities;

/// <summary>
/// Vertical movement against vanilla's numbers: jump impulse 0.42 with gravity 0.08 reaches about 1.2522
/// blocks, and falling clamps at 3.28 blocks per tick. The measured vanilla client apex on a live server was
/// 1.2492 blocks (forty millisecond sampling misses the exact peak)
/// (docs/specs/network/play-movement.md 7.15).
/// </summary>
public sealed class VerticalMovementTests
{
    [Fact]
    public void AStandingJumpReachesTheVanillaApex()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        // Settle on the ground first so the jump starts from rest.
        for (var tick = 0; tick < 20; tick++)
        {
            player.Tick(forward: 0f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        Assert.True(player.OnGround, "player should be standing before the jump");

        var restY = player.Position.Y;
        var apex = restY;

        // One jump pulse, then let the arc complete.
        player.Tick(forward: 0f, strafe: 0f, jumping: true, sprinting: false, yaw: 0f);
        for (var tick = 0; tick < 40; tick++)
        {
            player.Tick(forward: 0f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            apex = MathF.Max(apex, player.Position.Y);
        }

        Assert.Equal(1.2522f, apex - restY, 2);
        Assert.True(player.OnGround, "the player must land again");
        Assert.Equal(restY, player.Position.Y, 3);
    }

    [Fact]
    public void FallingApproachesTheVanillaTerminalVelocity()
    {
        // Terminal velocity is not a constant in vanilla: it is the fixed point of v = (v - 0.08) * 0.98, which
        // is 3.92 blocks per tick, or 78.4 blocks per second. Reaching it takes a long fall, because the speed
        // approaches it asymptotically: a two hundred block drop only gets to about 3.33.
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 1200f, 0.5f));

        var fastestFall = 0f;
        var ticks = 0;
        for (; ticks < 900 && !player.OnGround; ticks++)
        {
            player.Tick(forward: 0f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            fastestFall = MathF.Max(fastestFall, -player.DeltaMovement.Y);
        }

        Assert.True(fastestFall > 3.90f, $"fall speed {fastestFall} never approached the terminal velocity");
        Assert.True(fastestFall <= 3.921f, $"fall speed {fastestFall} exceeded the terminal velocity");
        Assert.True(player.OnGround, "the fall must end on the ground");
        Assert.Equal(0f, player.Position.Y, 3);
    }

    [Fact]
    public void AShortFallStaysBelowTerminalVelocity()
    {
        // The same asymptotic curve seen from the other end: two hundred blocks is not enough to reach terminal,
        // which is why the first measurement read 3.32 and looked wrong.
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 200f, 0.5f));

        var fastestFall = 0f;
        for (var tick = 0; tick < 400 && !player.OnGround; tick++)
        {
            player.Tick(forward: 0f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            fastestFall = MathF.Max(fastestFall, -player.DeltaMovement.Y);
        }

        Assert.InRange(fastestFall, 3.25f, 3.40f);
        Assert.Equal(0f, player.Position.Y, 3);
    }

    [Fact]
    public void AFastRiseIsNotSlowedByJumping()
    {
        // Vanilla sets the vertical speed to Math.max(jumpPower, current), so an entity already moving up
        // faster than 0.42 keeps its speed instead of being reset down to the jump impulse.
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));
        for (var tick = 0; tick < 20; tick++)
        {
            player.Tick(forward: 0f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        Assert.True(player.OnGround, "needs to stand before jumping");

        // Launch it upward well beyond the jump impulse, then jump on the next tick.
        player.ApplyServerSnapshot(player.Position, new Vector3(0f, 0.9f, 0f));
        player.Tick(forward: 0f, strafe: 0f, jumping: true, sprinting: false, yaw: 0f);

        Assert.True(player.DeltaMovement.Y > 0.42f, $"jump must not cap a faster rise, got {player.DeltaMovement.Y}");
    }

    [Fact]
    public void SneakingIsThirtyPercentSpeedWithACrouchedBox()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f, sneaking: true);
        }

        var before = player.Position;
        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f, sneaking: true);
        }

        var delta = player.Position - before;
        var perTick = new Vector2(delta.X, delta.Z).Length() / 100f;

        // Vanilla's SNEAKING_SPEED attribute is 0.3, so 0.2159 * 0.3 = 0.0648 blocks per tick, 1.296 per second.
        Assert.Equal(0.0648f, perTick, 4);

        // The crouched box is shorter and the eye drops to 1.27, which changes the camera height too.
        Assert.Equal(1.5f, player.Dimensions.Height, 3);
        Assert.Equal(1.27f, player.Dimensions.EyeHeight, 3);
        Assert.Equal(0.6f, player.Dimensions.Width, 3);
    }

    [Fact]
    public void SprintingIsOnePointThreeTimesWalking()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        }

        var before = player.Position;
        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        }

        var delta = player.Position - before;
        var perTick = new Vector2(delta.X, delta.Z).Length() / 100f;

        // 0.2159 * 1.3 = 0.2807 per tick, 5.614 per second; the vanilla client measured 5.623.
        Assert.Equal(0.2807f, perTick, 3);
    }

    /// <summary>Solid floor at y = 0 and nothing else.</summary>
    private sealed class FlatGround : IEntityWorld
    {
        public bool IsSolid(int x, int y, int z) => y < 0;

        public float Friction(int x, int y, int z) => 0.6f;
    }
}
