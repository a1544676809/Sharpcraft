// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Client.Input;
using Sharpcraft.Common.Entities;
using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Client.Tests.Game;

/// <summary>
/// The fixed-timestep loop and the first-person camera (docs/modules/input.md 5-6): ticks are whole and
/// bounded, the interpolation factor tracks the leftover, yaw follows the mouse, and pitch cannot flip.
/// </summary>
public sealed class TickLoopAndCameraTests
{
    [Fact]
    public void TicksRunAtAFixedRateRegardlessOfFrameTime()
    {
        var loop = new TickLoop();
        var ticks = 0;

        // 60 frames of 16 ms: slightly under a second, so 4 ticks of 50 ms can complete each 250 ms.
        for (var frame = 0; frame < 60; frame++)
        {
            loop.Advance(TimeSpan.FromMilliseconds(16), () => ticks++);
        }

        Assert.InRange(ticks, 18, 20);
    }

    [Fact]
    public void ASlowFrameRunsSeveralTicksButIsBounded()
    {
        var loop = new TickLoop();
        var ticks = 0;

        loop.Advance(TimeSpan.FromSeconds(10), () => ticks++);

        // A single stall must not run the whole backlog in one frame.
        Assert.Equal(TickLoop.MaxTicksPerAdvance, ticks);
    }

    [Fact]
    public void LeftoverTimeBecomesTheInterpolationFactor()
    {
        var loop = new TickLoop();

        loop.Advance(TimeSpan.FromMilliseconds(25), () => { });

        Assert.InRange(loop.Alpha, 0.45f, 0.55f);
        Assert.True(loop.PendingTicks > 0, "time should be pending");
    }

    [Fact]
    public void MovingTheMouseRightTurnsRightAndDownLooksDown()
    {
        var camera = new CameraController();

        camera.ApplyLook(new Vector2(100f, 50f), degreesPerPixel: 0.15f);

        Assert.True(camera.Yaw > 0f, $"yaw should grow when turning right, was {camera.Yaw}");
        Assert.True(camera.Pitch < 0f, $"pitch should drop when looking down, was {camera.Pitch}");
    }

    [Fact]
    public void PitchIsClampedAndYawWraps()
    {
        var camera = new CameraController();

        camera.ApplyLook(new Vector2(0f, -100000f), degreesPerPixel: 1f);
        Assert.Equal(CameraController.PitchLimit, camera.Pitch, 4);

        camera.ApplyLook(new Vector2(0f, 100000f), degreesPerPixel: 1f);
        Assert.Equal(-CameraController.PitchLimit, camera.Pitch, 4);

        camera.ApplyLook(new Vector2(100000f, 0f), degreesPerPixel: 1f);
        Assert.InRange(camera.Yaw, -MathF.PI * 2f, MathF.PI * 2f);
    }

    [Fact]
    public void YawZeroLooksTowardsMinusZ()
    {
        var camera = new CameraController();

        var forward = camera.Forward;

        Assert.Equal(0f, forward.X, 4);
        Assert.Equal(0f, forward.Y, 4);
        Assert.Equal(-1f, forward.Z, 4);
    }

    [Fact]
    public void TheCameraForwardMatchesTheDirectionThePlayerWalks()
    {
        // The entity rotates its input by yaw, so a "forward" input must move along the camera forward.
        var camera = new CameraController();
        camera.SetLook(yaw: MathF.PI / 2f, pitch: 0f);

        var forward = camera.Forward;
        Assert.Equal(1f, forward.X, 4);

        var player = new PlayerEntity(EmptyEntityWorld.Instance, new Vector3(0.5f, 0.5f, 0.5f));
        player.Tick(forward: 1f, strafe: 0f, jumping: false, sprinting: false, yaw: camera.Yaw);

        Assert.True(player.DeltaMovement.X > 0f, "walking forward must agree with the camera direction");
    }

    [Fact]
    public void TheEyeIsThePlayerEyePosition()
    {
        var player = new PlayerEntity(EmptyEntityWorld.Instance, new Vector3(4f, 5f, 6f));

        Assert.Equal(new Vector3(4f, 5f + 1.62f, 6f), player.EyePosition);
    }
}
