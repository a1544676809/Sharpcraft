// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class NameplateProjectionTests
{
    private static readonly Matrix4x4 Projection = Matrix4x4.CreatePerspectiveFieldOfView(1.2f, 16f / 9f, 0.1f, 256f);

    [Fact]
    public void ACentredEntityProjectsToTheScreenCentre()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        Assert.True(NameplateProjection.TryProject(new Vector3(0f, -(1.8f + NameplateProjection.HeadOffset), -10f), 1.8f, viewProjection, out var screen, out var depth));

        Assert.Equal(0.5f, screen.X, 3);
        Assert.Equal(0.5f, screen.Y, 3);
        Assert.True(depth > 0f && depth < 1f);
    }

    [Fact]
    public void AnEntityBehindTheCameraIsInvalid()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        Assert.False(NameplateProjection.TryProject(new Vector3(0f, 0f, 10f), 1.8f, viewProjection, out var screen, out var depth));
        Assert.True(float.IsNaN(screen.X));
        Assert.True(float.IsNaN(depth));
    }

    [Fact]
    public void ALeftEntityProjectsLeftOfCentre()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        Assert.True(NameplateProjection.TryProject(new Vector3(-2f, 0f, -10f), 1.8f, viewProjection, out var screen, out _));
        Assert.True(screen.X < 0.5f);
    }

    [Fact]
    public void TheAnchorRisesWithEntityHeight()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        Assert.True(NameplateProjection.TryProject(new Vector3(0f, 0f, -10f), 1.8f, viewProjection, out var tall, out _));
        Assert.True(NameplateProjection.TryProject(new Vector3(0f, 0f, -10f), 0.9f, viewProjection, out var shortEntity, out _));
        Assert.True(tall.Y < shortEntity.Y);
    }
    [Fact]
    public void ABillboardIsWiderWhenTheEntityIsCloser()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;

        Assert.True(NameplateProjection.TryProjectBillboard(
            new Vector3(0f, 0f, -2f), 1.8f, 60f, 8f, Vector3.Zero, new Vector3(0f, 0f, -1f), viewProjection,
            out _, out var nearWidth, out _));
        Assert.True(NameplateProjection.TryProjectBillboard(
            new Vector3(0f, 0f, -20f), 1.8f, 60f, 8f, Vector3.Zero, new Vector3(0f, 0f, -1f), viewProjection,
            out _, out var farWidth, out _));

        Assert.True(nearWidth > farWidth);
    }

    [Fact]
    public void ABillboardAnchorUsesTheHeadOffset()
    {
        var viewProjection = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY) * Projection;
        var feet = new Vector3(0f, -(1.8f + NameplateProjection.HeadOffset), -10f);

        Assert.True(NameplateProjection.TryProjectBillboard(
            feet, 1.8f, 60f, 8f, Vector3.Zero, new Vector3(0f, 0f, -1f), viewProjection,
            out var screen, out _, out _));

        Assert.Equal(0.5f, screen.X, 3);
        Assert.Equal(0.5f, screen.Y, 3);
    }}
