// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Camera;
using Sharpcraft.Client.Game;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Client.Tests.Game;

/// <summary>
/// Rendering interpolation (docs/src/sharpcraft-client/game.md 5): frames are drawn between ticks, so the
/// eye has to move smoothly instead of jumping once per 50 ms.
/// </summary>
public sealed class PlayerInterpolatorTests
{
    [Fact]
    public void TheEyeMovesSmoothlyBetweenTicks()
    {
        var player = new PlayerEntity(EmptyEntityWorld.Instance, new Vector3(0f, 0f, 0f));
        var interpolator = new PlayerInterpolator(player, yaw: 0f);

        player.SetPosition(new Vector3(0f, 0f, -1f));
        interpolator.Push(player, yaw: 0f);

        var start = interpolator.Eye(0f);
        var middle = interpolator.Eye(0.5f);
        var end = interpolator.Eye(1f);

        Assert.Equal(0f, start.Z, 4);
        Assert.Equal(-0.5f, middle.Z, 4);
        Assert.Equal(-1f, end.Z, 4);
    }

    [Fact]
    public void AlphaIsClampedSoAFrameCannotOvershoot()
    {
        var player = new PlayerEntity(EmptyEntityWorld.Instance, new Vector3(0f, 0f, 0f));
        var interpolator = new PlayerInterpolator(player, yaw: 0f);

        player.SetPosition(new Vector3(0f, 0f, -1f));
        interpolator.Push(player, yaw: 0f);

        Assert.Equal(-1f, interpolator.Eye(2f).Z, 4);
        Assert.Equal(0f, interpolator.Eye(-1f).Z, 4);
    }

    [Fact]
    public void YawInterpolatesAlongTheShortArc()
    {
        var player = new PlayerEntity(EmptyEntityWorld.Instance, new Vector3(0f, 0f, 0f));
        var interpolator = new PlayerInterpolator(player, yaw: 3.0f);

        // Wrapping from just under +π to just over −π must not spin the view the long way round.
        interpolator.Push(player, yaw: -3.0f);

        var middle = interpolator.Yaw(0.5f);

        // Halfway across the seam is near ±π, not near 0.
        Assert.True(MathF.Abs(middle) > 3.0f, $"expected the short arc across π, got {middle}");
    }

    [Fact]
    public void SnappingDiscardsThePreviousSample()
    {
        var player = new PlayerEntity(EmptyEntityWorld.Instance, new Vector3(0f, 0f, 0f));
        var interpolator = new PlayerInterpolator(player, yaw: 0f);

        player.SetPosition(new Vector3(100f, 0f, 0f));
        interpolator.Snap(player, yaw: 0f);

        // A teleport must not be smeared over a tick: both samples are the new position.
        Assert.Equal(100f, interpolator.Eye(0f).X, 3);
        Assert.Equal(100f, interpolator.Eye(1f).X, 3);
        Assert.Equal(0f, interpolator.SampleDistance, 3);
    }
}
