// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Strider procedural animation coverage ported from <c>StriderModel.setupAnim</c> and
/// <c>AdultStriderModel.customAnimations</c>. Pins the body/leg phases, the ridden body lock, the bristle flow and idle
/// sines (baseline removed because it is baked into the bind pose), the body/leg y offsets in storage coordinates, and
/// the part-name contract.
/// </summary>
public sealed class StriderAnimationTests
{
    private static readonly int TypeId = StriderModelDefinition.StriderEntityTypeId;

    private const float Deg = MathF.PI / 180f;

    private static float Rad(float degrees) => degrees * Deg;

    private static float ClampedSpeed(float speed) => MathF.Min(speed, 0.25f);

    private static EntityAnimationFrame Frame(
        float pos = 0f,
        float speed = 0f,
        float ageInTicks = 0f,
        float pitch = 0f,
        float yaw = 0f,
        bool isVehicle = false) =>
        new(
            TypeId,
            ageInTicks,
            pos,
            speed,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            0f,
            false,
            false,
            0,
            false,
            false,
            false,
            isVehicle);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderExposesIdleWalkAndItsTypeId()
    {
        var provider = new StriderAnimationProvider(TypeId);
        Assert.Equal(132, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk" }, provider.Clips);
    }

    [Fact]
    public void BodyLooksAlongPitchAndYawUnlessRidden()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var state = provider.CreateState();

        var free = Apply(state, provider, Frame(pitch: 30f, yaw: -45f));
        Assert.Equal(Rad(30f), free.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(-45f), free.Get("body").Rotation.Y, 4);

        var ridden = Apply(state, provider, Frame(pitch: 30f, yaw: -45f, isVehicle: true));
        Assert.Equal(0f, ridden.Get("body").Rotation.X, 4);
        Assert.Equal(0f, ridden.Get("body").Rotation.Y, 4);
    }

    [Fact]
    public void BodySwayAndVerticalOffsetFollowTheWalkPhase()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 1f, speed: 0.2f));

        // reference body.zRot = 0.1·sin(pos·1.5)·4·speed; body.y offset = 4·cos(pos·1.5)·speed − 1 (y-up storage).
        var expectedZ = 0.1f * MathF.Sin(1.5f) * 4f * 0.2f;
        var expectedY = (4f * MathF.Cos(1.5f) * 0.2f) - 1f;
        Assert.Equal(expectedZ, pose.Get("body").Rotation.Z, 4);
        Assert.Equal(expectedY, pose.Get("body").Position.Y, 4);
    }

    [Fact]
    public void LegsFollowTheOppositeWalkPhase()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 1f, speed: 0.2f));

        var legPhase = 1f * 1.5f * 0.5f; // 0.75
        var leftX = MathF.Sin(legPhase) * 2f * 0.2f;
        var rightX = MathF.Sin(legPhase + MathF.PI) * 2f * 0.2f;
        Assert.Equal(leftX, pose.Get("left_leg").Rotation.X, 4);
        Assert.Equal(rightX, pose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-leftX, rightX, 4);

        var zScale = (MathF.PI / 18f) * 0.2f;
        Assert.Equal(zScale * MathF.Cos(legPhase), pose.Get("left_leg").Rotation.Z, 4);
        Assert.Equal(zScale * MathF.Cos(legPhase + MathF.PI), pose.Get("right_leg").Rotation.Z, 4);

        // reference leg.y deltas are 4·sin(phase)·speed; storage y-up: left keeps +, right is negated.
        Assert.Equal(4f * MathF.Sin(legPhase) * 0.2f, pose.Get("left_leg").Position.Y, 4);
        Assert.Equal(-4f * MathF.Sin(legPhase) * 0.2f, pose.Get("right_leg").Position.Y, 4);
    }

    [Fact]
    public void BodyAndLegsRestWithTheBodyBaselineShift()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 0f, speed: 0f));

        // reference body.y baseline 2 vs bind 1 → storage offset −1; legs keep their bind y.
        Assert.Equal(-1f, pose.Get("body").Position.Y, 4);
        Assert.Equal(0f, pose.Get("left_leg").Position.Y, 4);
        Assert.Equal(0f, pose.Get("right_leg").Position.Y, 4);
        Assert.Equal(Vector3.Zero, pose.Get("body").Rotation);
    }

    [Fact]
    public void WalkSpeedIsCappedAtAQuarter()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var state = provider.CreateState();

        var capped = Apply(state, provider, Frame(pos: 2f, speed: 0.25f));
        var fast = Apply(state, provider, Frame(pos: 2f, speed: 5f));

        Assert.Equal(capped.Get("body").Rotation.Z, fast.Get("body").Rotation.Z, 5);
        Assert.Equal(capped.Get("body").Position.Y, fast.Get("body").Position.Y, 5);
        Assert.Equal(capped.Get("left_leg").Rotation.X, fast.Get("left_leg").Rotation.X, 5);
        Assert.Equal(capped.Get("right_leg").Position.Y, fast.Get("right_leg").Position.Y, 5);
    }

    [Fact]
    public void BristlesRippleWithTheFlowAndStayNeutralAtRest()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var state = provider.CreateState();

        // At rest the bristle baselines are already in the mesh, so the animation delta is zero.
        var rest = Apply(state, provider, Frame(pos: 0f, speed: 0f, ageInTicks: 0f));
        Assert.Equal(0f, rest.Get("right_top_bristle").Rotation.Z, 5);
        Assert.Equal(0f, rest.Get("right_middle_bristle").Rotation.Z, 5);
        Assert.Equal(0f, rest.Get("right_bottom_bristle").Rotation.Z, 5);
        Assert.Equal(0f, rest.Get("left_top_bristle").Rotation.Z, 5);

        // flow = cos(0·1.5 + π)·0.25 = −0.25 at age 0; multipliers 0.6 / 1.2 / 1.3.
        var moving = Apply(state, provider, Frame(pos: 0f, speed: 0.25f, ageInTicks: 0f));
        Assert.Equal(-0.25f * 0.6f, moving.Get("right_top_bristle").Rotation.Z, 4);
        Assert.Equal(-0.25f * 1.2f, moving.Get("right_middle_bristle").Rotation.Z, 4);
        Assert.Equal(-0.25f * 1.3f, moving.Get("right_bottom_bristle").Rotation.Z, 4);
        // Mirrored bristles share the same additive delta (their baselines differ only in sign).
        Assert.Equal(moving.Get("right_top_bristle").Rotation.Z, moving.Get("left_top_bristle").Rotation.Z, 5);
        Assert.Equal(moving.Get("right_bottom_bristle").Rotation.Z, moving.Get("left_bottom_bristle").Rotation.Z, 5);
    }

    [Fact]
    public void BristleIdleSinesFollowTheAge()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 0f, speed: 0f, ageInTicks: 5f));

        Assert.Equal(0.1f * MathF.Sin(5f * 0.4f), pose.Get("right_top_bristle").Rotation.Z, 4);
        Assert.Equal(0.1f * MathF.Sin(5f * 0.2f), pose.Get("right_middle_bristle").Rotation.Z, 4);
        Assert.Equal(0.05f * MathF.Sin(5f * -0.4f), pose.Get("right_bottom_bristle").Rotation.Z, 4);
        Assert.Equal(pose.Get("right_top_bristle").Rotation.Z, pose.Get("left_top_bristle").Rotation.Z, 5);
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndWalk()
    {
        var provider = new StriderAnimationProvider(TypeId);
        var state = provider.CreateState();

        var idle = new EntityPose();
        provider.ApplyClip(state, Frame(pitch: 10f, yaw: 20f), "idle", 0.5f, idle);
        Assert.Equal(-1f, idle.Get("body").Position.Y, 4);
        Assert.Equal(0f, idle.Get("left_leg").Position.Y, 4);
        Assert.Equal(0f, idle.Get("body").Rotation.Z, 4);

        var walkFrame = Frame(pos: 1f, speed: 1f, ageInTicks: 3f);
        var walkClip = new EntityPose();
        provider.ApplyClip(state, walkFrame, "walk", 0.1f, walkClip);
        var direct = Apply(state, provider, walkFrame);
        Assert.Equal(direct.Get("body").Rotation.Z, walkClip.Get("body").Rotation.Z, 4);
        Assert.Equal(direct.Get("left_leg").Rotation.X, walkClip.Get("left_leg").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, walkFrame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheStriderModel()
    {
        var modelParts = StriderModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var partName in new[]
                 {
                     "body", "left_leg", "right_leg",
                     "right_bottom_bristle", "right_middle_bristle", "right_top_bristle",
                     "left_top_bristle", "left_middle_bristle", "left_bottom_bristle"
                 })
        {
            Assert.Contains(partName, modelParts);
        }
    }
}
