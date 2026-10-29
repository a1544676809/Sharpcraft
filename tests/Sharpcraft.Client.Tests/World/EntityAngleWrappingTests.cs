// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// Angles must stay inside one turn. Interpolation moves towards a target that the server may express on either
/// side of the zero crossing, and a naive accumulation lets the rendered angle drift out of range, which showed
/// up in a live session as values like minus eight hundred degrees.
/// </summary>
public sealed class EntityAngleWrappingTests
{
    [Fact]
    public void RepeatedInterpolationKeepsTheAngleInsideOneTurn()
    {
        var tracker = new EntityTracker();
        tracker.Add(new AddedEntity(1, Guid.Empty, TypeId: 159, 0, 0, 0, 0f, 0f, 0f, 0f, 359f, 359f, 0));

        // Alternate the target across the zero crossing many times; the rendered angle must never leave [0, 360).
        for (var round = 0; round < 200; round++)
        {
            var target = (round % 2 == 0) ? 1f : 359f;
            tracker.ApplyMove(new EntityMove(1, 0, 0, 0, HasPosition: false, target, 0f, HasRotation: true, OnGround: true));

            for (var tick = 0; tick < EntityTracker.InterpolationSteps; tick++)
            {
                tracker.Tick();
            }

            Assert.True(tracker.TryGet(1, out var entity));
            Assert.InRange(entity!.Yaw, 0f, 360f);
            Assert.InRange(tracker.Interpolate(0.5f)[0].Yaw, 0f, 360f);
        }
    }
}
