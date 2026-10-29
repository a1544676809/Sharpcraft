// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Common.Tests.Entities;

/// <summary>
/// Sprint behaviour (docs/specs/network/play-movement.md 7.21). Both rules here come straight from vanilla:
/// the sprint state latches while the player keeps moving forward, and a sprint jump adds a horizontal push so
/// bunny hopping does not fall back to walking speed.
/// </summary>
public sealed class SprintBehaviourTests
{
    [Fact]
    public void AHeldSprintKeyLatchesWhileMovingForward()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0.5f, 0.5f));

        // One tick with the sprint key, then no key at all but still walking forward.
        player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var before = player.Position;
        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var perTick = Horizontal(player.Position - before) / 100f;

        // Sprint speed, not walking speed: 0.2807 rather than 0.2159.
        Assert.True(player.Sprinting, "the sprint state should still hold without the key");
        Assert.Equal(0.2807f, perTick, 3);
    }

    [Fact]
    public void ReleasingForwardEndsTheSprint()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0.5f, 0.5f));

        player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        player.Tick(forward: 0f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);

        Assert.False(player.Sprinting);
    }

    [Fact]
    public void CrouchingEndsTheSprint()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0.5f, 0.5f));

        player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f, sneaking: true);

        Assert.False(player.Sprinting);
    }

    [Fact]
    public void BunnyHoppingDoesNotFallBackToWalkingSpeed()
    {
        // The reported symptom: hopping while sprinting slowed to walking pace. With the sprint jump push and
        // the flat air impulse, the average must stay clearly above the 0.2159 walking figure.
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0.5f, 0.5f));

        for (var tick = 0; tick < 40; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: true, yaw: 0f);
        }

        var start = player.Position;
        const int ticks = 200;
        for (var tick = 0; tick < ticks; tick++)
        {
            // Jump whenever the feet are on the ground, which is what holding the key does.
            player.Tick(forward: 1f, strafe: 0f, jumping: player.OnGround, sprinting: true, yaw: 0f);
        }

        var perTick = Horizontal(player.Position - start) / ticks;

        Assert.True(perTick > 0.26f, $"bunny hopping averaged {perTick:F4} per tick, too close to walking");
    }

    private static float Horizontal(Vector3 delta) => MathF.Sqrt((delta.X * delta.X) + (delta.Z * delta.Z));

    /// <summary>Solid floor at y = 0 and nothing else.</summary>
    private sealed class FlatGround : IEntityWorld
    {
        public bool IsSolid(int x, int y, int z) => y < 0;

        public float Friction(int x, int y, int z) => 0.6f;
    }
}
