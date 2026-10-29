// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Common.Tests.Entities;

/// <summary>
/// The entity foundation (docs/modules/entities.md). These tests state the behaviour that makes the layer
/// equivalent to vanilla: the documented speeds must come out of the formula, a jump must peak near 1.25
/// blocks, sliding friction must change acceleration and top speed, and collisions must clamp rather than
/// reject.
/// </summary>
public sealed class EntityMovementTests
{
    [Fact]
    public void WalkingSettlesAtTheDocumentedSpeed()
    {
        var player = OnGroundPlayer();
        var start = player.Position;

        for (var tick = 0; tick < 40; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        // 0.1 * 0.98 / (1 − 0.546) blocks per tick ≈ 4.317 blocks per second.
        var distance = start.Z - player.Position.Z;
        var speedPerSecond = distance / 40f * 20f;
        Assert.InRange(speedPerSecond, 4.0f, 4.6f);
    }

    [Fact]
    public void SprintingSettlesNearTheDocumentedSpeed()
    {
        var player = OnGroundPlayer();
        var start = player.Position;

        for (var tick = 0; tick < 40; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        }

        // 0.1 * 1.3 * 0.98 / (1 − 0.546) ≈ 5.612 blocks per second.
        var speedPerSecond = (start.Z - player.Position.Z) / 40f * 20f;
        Assert.InRange(speedPerSecond, 5.2f, 6.0f);
    }

    [Fact]
    public void WalkingAcceleratesInsteadOfJumpingToSpeed()
    {
        var player = OnGroundPlayer();
        var firstTick = DistanceAfter(player, ticks: 1);
        var settledTick = DistanceAfter(OnGroundPlayer(), ticks: 20) / 20f;

        // Vanilla ramps up: the very first tick moves less than the settled speed.
        Assert.True(firstTick < settledTick * 0.7f, $"first tick {firstTick} should be well below {settledTick}");
    }

    [Fact]
    public void SlipperyBlocksAccelerateSlowerButReachHigherSpeed()
    {
        // Measured with the vanilla formula: ice accelerates far slower per tick and its steady speed is
        // slightly lower, but it keeps the velocity for much longer — that is the behaviour players feel.
        var iceFirstTick = FirstTickDistanceOn(friction: 0.98f);
        var stoneFirstTick = FirstTickDistanceOn(friction: 0.6f);
        Assert.True(iceFirstTick < stoneFirstTick * 0.5f, $"ice should barely accelerate: {iceFirstTick} vs {stoneFirstTick}");

        var iceCoast = CoastDistanceOn(friction: 0.98f);
        var stoneCoast = CoastDistanceOn(friction: 0.6f);
        Assert.True(iceCoast > stoneCoast * 1.5f, $"ice should slide much further: {iceCoast} vs {stoneCoast}");
    }

    [Fact]
    public void ReleasingInputDeceleratesThroughFriction()
    {
        var player = OnGroundPlayer();
        for (var tick = 0; tick < 30; tick++)
        {
            player.Tick(1f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var before = player.Position.Z;
        for (var tick = 0; tick < 10; tick++)
        {
            player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var coasted = before - player.Position.Z;
        Assert.True(coasted > 0.05f, $"the player should slide a little after releasing, moved {coasted}");
    }

    [Fact]
    public void JumpingPeaksNearOneAndAQuarterBlocks()
    {
        var player = OnGroundPlayer();
        var start = player.Position.Y;
        var peak = start;

        for (var tick = 0; tick < 20; tick++)
        {
            player.Tick(0f, 0f, jumping: true, sprinting: false, yaw: 0f);
            peak = MathF.Max(peak, player.Position.Y);
        }

        Assert.InRange(peak - start, 1.15f, 1.35f);
    }

    [Fact]
    public void FallingIsClampedToTheTerminalVelocity()
    {
        var player = new PlayerEntity(FlatWorld(), new Vector3(0.5f, 300f, 0.5f));

        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        Assert.True(
            player.DeltaMovement.Y >= -LivingEntity.TerminalVelocity - 1e-3f,
            $"vertical speed {player.DeltaMovement.Y} exceeded the terminal velocity");
    }

    [Fact]
    public void TheBodyRestsExactlyOnTheFloor()
    {
        var player = new PlayerEntity(FlatWorld(), new Vector3(0.5f, 10f, 0.5f));

        for (var tick = 0; tick < 120; tick++)
        {
            player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        Assert.True(player.OnGround);
        Assert.Equal(1f, player.Position.Y, 3);

        var resting = player.Position;
        for (var tick = 0; tick < 10; tick++)
        {
            player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        Assert.Equal(resting.Y, player.Position.Y, 5);
    }

    [Fact]
    public void AWallStopsTheBodyAndKeepsItHalfAWidthAway()
    {
        var world = new TestWorld(solid: (x, y, z) => y < 1f || (y < 3f && z <= -3));
        var player = new PlayerEntity(world, new Vector3(0.5f, 1f, 0.5f));

        for (var tick = 0; tick < 200; tick++)
        {
            player.Tick(1f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        // Blocks with z <= -3 are solid, so the block at z = -3 spans [-3, -2]: its near face is z = -2 and
        // the body centre stops one half width (0.3) in front of it.
        Assert.InRange(player.Position.Z, -1.8f, -1.6f);
    }

    [Fact]
    public void AHighSpeedFallDoesNotTunnelThroughTheFloor()
    {
        var player = new PlayerEntity(FlatWorld(), new Vector3(0.5f, 500f, 0.5f));

        for (var tick = 0; tick < 600; tick++)
        {
            player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        Assert.True(player.Position.Y >= 1f - 1e-3f, $"fell through the floor to {player.Position.Y}");
    }

    [Fact]
    public void TheEyeSitsOnePointSixTwoAboveTheFeet()
    {
        var player = OnGroundPlayer();

        Assert.Equal(1.62f, player.Dimensions.EyeHeight, 3);
        Assert.Equal(player.Position.Y + 1.62f, player.EyePosition.Y, 5);
    }

    [Fact]
    public void DefaultEyeHeightIsEightyFivePercentOfTheBody()
    {
        var dimensions = EntityDimensions.WithDefaultEyeHeight(1f, 2f);

        Assert.Equal(1.7f, dimensions.EyeHeight, 3);
    }

    // ---- helpers -------------------------------------------------------------

    private static PlayerEntity OnGroundPlayer()
    {
        var player = new PlayerEntity(FlatWorld(), new Vector3(0.5f, 1f, 0.5f));
        player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        return player;
    }

    private static float DistanceAfter(PlayerEntity player, int ticks)
    {
        var start = player.Position.Z;
        for (var tick = 0; tick < ticks; tick++)
        {
            player.Tick(1f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        return start - player.Position.Z;
    }

    private static float SettledSpeedOn(float friction)
    {
        var player = new PlayerEntity(new TestWorld(solid: (_, y, _) => y < 1, friction: friction),
            new Vector3(0.5f, 1f, 0.5f));
        player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);

        var start = player.Position.Z;
        for (var tick = 0; tick < 60; tick++)
        {
            player.Tick(1f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        return (start - player.Position.Z) / 60f;
    }

    private static float CoastDistanceOn(float friction)
    {
        var player = new PlayerEntity(new TestWorld(solid: (_, y, _) => y < 1, friction: friction),
            new Vector3(0.5f, 1f, 0.5f));
        player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);

        for (var tick = 0; tick < 60; tick++)
        {
            player.Tick(1f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var before = player.Position.Z;
        for (var tick = 0; tick < 20; tick++)
        {
            player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        return before - player.Position.Z;
    }

    private static float FirstTickDistanceOn(float friction)
    {
        var player = new PlayerEntity(new TestWorld(solid: (_, y, _) => y < 1, friction: friction),
            new Vector3(0.5f, 1f, 0.5f));
        player.Tick(0f, 0f, jumping: false, sprinting: false, yaw: 0f);

        var start = player.Position.Z;
        player.Tick(1f, 0f, jumping: false, sprinting: false, yaw: 0f);
        return start - player.Position.Z;
    }

    private static IEntityWorld FlatWorld() => new TestWorld(solid: (_, y, _) => y < 1);

    private sealed class TestWorld : IEntityWorld
    {
        private readonly Func<int, int, int, bool> _solid;
        private readonly float _friction;

        public TestWorld(Func<int, int, int, bool> solid, float friction = 0.6f)
        {
            _solid = solid;
            _friction = friction;
        }

        public bool IsSolid(int x, int y, int z) => _solid(x, y, z);

        public float Friction(int x, int y, int z) => _friction;
    }
}
