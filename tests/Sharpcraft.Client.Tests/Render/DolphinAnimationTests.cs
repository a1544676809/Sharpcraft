// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Dolphin animation coverage ported from <c>DolphinModel.setupAnim</c>. The body looks along pitch/yaw; while moving it
/// adds a <c>−0.05 − 0.05·cos(age·0.3)</c> body bob and cosine flutter on the tail (−0.1·cos, minus its bind) and tail
/// fin (−0.2·cos). Pins body look, moving vs idle, cosine phases, the idle/swim clip reconstruction and part coverage.
/// </summary>
public sealed class DolphinAnimationTests
{
    private static readonly int TypeId = DolphinModelDefinition.DolphinEntityTypeId;

    private const float Deg = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float pitch = 0f,
        float yaw = 0f,
        bool moving = false) =>
        new(
            TypeId,
            ageInTicks,
            0f,
            0f,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            IsMoving: moving);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static float BodyX(float pitch, float age, bool moving) =>
        (pitch * Deg) + (moving ? -0.05f - (0.05f * MathF.Cos(age * 0.3f)) : 0f);

    private static float TailX(float age) => (-0.1f * MathF.Cos(age * 0.3f)) - (-0.10471976f);

    private static float TailFinX(float age) => -0.2f * MathF.Cos(age * 0.3f);

    [Fact]
    public void ProviderExposesIdleSwimAndItsTypeId()
    {
        var provider = new DolphinAnimationProvider(TypeId);
        Assert.Equal(36, provider.TypeId);
        Assert.Equal(new[] { "idle", "swim" }, provider.Clips);
    }

    [Fact]
    public void BodyLooksAlongPitchAndYawWhenIdle()
    {
        var provider = new DolphinAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 30f, yaw: -45f, moving: false));

        Assert.Equal(30f * Deg, pose.Get("body").Rotation.X, 4);
        Assert.Equal(-45f * Deg, pose.Get("body").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("body").Rotation.Z, 6);
    }

    [Fact]
    public void MovingAddsTheBodyBob()
    {
        var provider = new DolphinAnimationProvider(TypeId);
        var state = provider.CreateState();

        // age 0 → cos = 1 → body bob = -0.1.
        var atZero = Apply(state, provider, Frame(ageInTicks: 0f, pitch: 10f, moving: true));
        Assert.Equal(10f * Deg - 0.1f, atZero.Get("body").Rotation.X, 4);

        var atPhase = Apply(state, provider, Frame(ageInTicks: 2.5f, pitch: 10f, moving: true));
        Assert.Equal(BodyX(10f, 2.5f, true), atPhase.Get("body").Rotation.X, 4);
    }

    [Fact]
    public void TailAndFinFlutterOnlyWhileMoving()
    {
        var provider = new DolphinAnimationProvider(TypeId);
        var state = provider.CreateState();

        var movingZero = Apply(state, provider, Frame(ageInTicks: 0f, moving: true));
        Assert.Equal(TailX(0f), movingZero.Get("tail").Rotation.X, 4);
        Assert.Equal(TailFinX(0f), movingZero.Get("tail_fin").Rotation.X, 4);

        // Half a cosine period later (age = π/0.3) cos = -1.
        var halfPeriod = MathF.PI / 0.3f;
        var movingHalf = Apply(state, provider, Frame(ageInTicks: halfPeriod, moving: true));
        Assert.Equal(TailX(halfPeriod), movingHalf.Get("tail").Rotation.X, 3);
        Assert.Equal(TailFinX(halfPeriod), movingHalf.Get("tail_fin").Rotation.X, 3);
        Assert.Equal(0.20471975f, movingHalf.Get("tail").Rotation.X, 3);
        Assert.Equal(0.2f, movingHalf.Get("tail_fin").Rotation.X, 3);
    }

    [Fact]
    public void IdleLeavesTheTailAtBind()
    {
        var provider = new DolphinAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 3f, moving: false));

        // No pose entries for the tail/tail fin → they stay at their bind pose (tail bind xRot −0.10471976).
        Assert.False(pose.Offsets.ContainsKey("tail"));
        Assert.False(pose.Offsets.ContainsKey("tail_fin"));
        Assert.Single(pose.Offsets);
        Assert.True(pose.Offsets.ContainsKey("body"));
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndSwim()
    {
        var provider = new DolphinAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 5f, yaw: 10f);

        var swim = new EntityPose();
        provider.ApplyClip(state, frame, "swim", 0.1f, swim); // age 2, moving
        Assert.Equal(BodyX(5f, 2f, true), swim.Get("body").Rotation.X, 4);
        Assert.Equal(TailX(2f), swim.Get("tail").Rotation.X, 4);

        var idle = new EntityPose();
        provider.ApplyClip(state, frame, "idle", 0.1f, idle); // age 2, still
        Assert.Equal(BodyX(5f, 2f, false), idle.Get("body").Rotation.X, 4);
        Assert.False(idle.Offsets.ContainsKey("tail"));

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheDolphinModel()
    {
        var modelParts = DolphinModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("body", modelParts);
        Assert.Contains("tail", modelParts);
        Assert.Contains("tail_fin", modelParts);
    }
}
