// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Common.Tests.Entities;

/// <summary>
/// The steady-state walking speed per tick. Vanilla's formula is
/// <c>speed * inputFriction / (1 - friction * 0.91)</c> = <c>0.1 * 0.98 / (1 - 0.546)</c> = 0.2159 blocks per
/// tick, which at 20 ticks per second is 4.317 blocks per second. Pinning the per-tick value separates a real
/// physics difference from a measurement artefact (docs/specs/network/play-movement.md 7.13).
/// </summary>
public sealed class WalkingSpeedPrecisionTests
{
    [Fact]
    public void SteadyStateWalkIsExactlyTheVanillaFormula()
    {
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        // Let the acceleration curve settle before measuring.
        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var before = player.Position;
        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var delta = player.Position - before;
        var horizontalPerTick = new Vector2(delta.X, delta.Z).Length() / 100f;

        // 0.1 * 0.98 / (1 - 0.6 * 0.91)
        Assert.Equal(0.2159f, horizontalPerTick, 3);
    }

    /// <summary>Solid floor at y = 0 and nothing else, so the player walks instead of falling.</summary>
    private sealed class FlatGround : IEntityWorld
    {
        public bool IsSolid(int x, int y, int z) => y < 0;

        public float Friction(int x, int y, int z) => 0.6f;
    }

    [Fact]
    public void DiagonalWalkingIsNotFasterThanStraightWalking()
    {
        // Vanilla normalises the input only when its squared length exceeds one, so pressing two directions at
        // once does not make the player faster. Losing that detail would make diagonals 41 percent quicker,
        // which is exactly the kind of silent difference an equivalence test has to catch.
        var straight = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));
        var diagonal = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        for (var tick = 0; tick < 100; tick++)
        {
            straight.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            diagonal.Tick(forward: 1f, strafe: 1f, jumping: false, sprinting: false, yaw: 0f);
        }

        var straightBefore = straight.Position;
        var diagonalBefore = diagonal.Position;
        for (var tick = 0; tick < 100; tick++)
        {
            straight.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            diagonal.Tick(forward: 1f, strafe: 1f, jumping: false, sprinting: false, yaw: 0f);
        }

        var straightSpeed = new Vector2(
            (straight.Position - straightBefore).X,
            (straight.Position - straightBefore).Z).Length() / 100f;
        var diagonalSpeed = new Vector2(
            (diagonal.Position - diagonalBefore).X,
            (diagonal.Position - diagonalBefore).Z).Length() / 100f;

        Assert.Equal(straightSpeed, diagonalSpeed, 3);
    }

    [Fact]
    public void EveryTickOfSteadyWalkingMovesTheSameDistance()
    {
        // A report that our player looks like it speeds up and slows down in a regular rhythm points at the
        // physics rather than the network, so this measures the distribution of per tick steps rather than the
        // average. Uniform steps cannot produce that appearance; alternating ones can.
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var previous = player.Position;
        var steps = new List<float>(40);

        for (var tick = 0; tick < 40; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            var position = player.Position;
            var delta = position - previous;
            steps.Add(new Vector2(delta.X, delta.Z).Length());
            previous = position;
        }

        var min = steps.Min();
        var max = steps.Max();

        Assert.True(
            max - min < 0.001f,
            $"per tick steps are not uniform: min={min:F4} max={max:F4} sequence={string.Join(",", steps.Select(s => s.ToString("F4")))}");
    }

    [Fact]
    public void GroundStateIsStableWhileWalkingOnFlatGround()
    {
        // If the contact flag or the vertical offset oscillated on flat ground, the other client would see a
        // regular bobbing rhythm even though horizontal progress is uniform, so both are pinned here.
        var player = new PlayerEntity(new FlatGround(), new Vector3(0.5f, 0f, 0.5f));

        for (var tick = 0; tick < 100; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
        }

        var previousY = player.Position.Y;
        var airborne = 0;
        var verticalSteps = new List<float>(40);

        for (var tick = 0; tick < 40; tick++)
        {
            player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: 0f);
            verticalSteps.Add(player.Position.Y - previousY);
            previousY = player.Position.Y;

            if (!player.OnGround)
            {
                airborne++;
            }
        }

        Assert.True(airborne == 0, $"{airborne} of 40 ticks were airborne on flat ground");
        Assert.True(
            verticalSteps.Max(Math.Abs) < 0.001f,
            $"vertical drift per tick: min={verticalSteps.Min():F5} max={verticalSteps.Max():F5}");
    }
}
