// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Witch procedural animation ported from <c>WitchModel.setupAnim</c>. These tests pin the leg phase, head look,
/// integer-id nose wobble, the holding-item override, the provider clip set and the part-name contract with the model.
/// </summary>
public sealed class WitchAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float age = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        bool hasMainHand = false,
        bool hasOffHand = false,
        int entityId = 0) => new(
        TypeId: WitchModelDefinition.WitchEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        HasMainHandItem: hasMainHand,
        HasOffHandItem: hasOffHand,
        EntityId: entityId);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new WitchAnimationProvider(WitchModelDefinition.WitchEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderAdvertisesTheThreeClipsAndTypeId()
    {
        var provider = new WitchAnimationProvider(WitchModelDefinition.WitchEntityTypeId);
        Assert.Equal(WitchModelDefinition.WitchEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "hold_item" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var pose = Apply(Frame(pitch: 15f, yaw: -30f));
        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-30f * ToRad, pose.Get("head").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 5);
    }

    [Fact]
    public void LegsSwingOppositeFromTheWalkPhase()
    {
        // At phase 0 the right leg is +0.7 and the left is -0.7 (amplitude 1.4 * 1 * 0.5).
        var start = Apply(Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(0.7f, start.Get("right_leg").Rotation.X, 5);
        Assert.Equal(-0.7f, start.Get("left_leg").Rotation.X, 5);

        // One half cycle later (phase = π) the legs swap.
        var halfPhasePos = MathF.PI / 0.6662f;
        var half = Apply(Frame(walkPos: halfPhasePos, walkSpeed: 1f));
        Assert.Equal(-0.7f, half.Get("right_leg").Rotation.X, 4);
        Assert.Equal(0.7f, half.Get("left_leg").Rotation.X, 4);

        // Slower walk scales the amplitude linearly.
        var slow = Apply(Frame(walkPos: 0f, walkSpeed: 0.5f));
        Assert.Equal(0.35f, slow.Get("right_leg").Rotation.X, 5);
    }

    [Fact]
    public void NoseWobbleUsesTheEntityIdRemainderSpeed()
    {
        // entityId 7 -> speed 0.07; at age 0 sin = 0 (pitch 0) and cos = 1 (roll 2.5°).
        var atZero = Apply(Frame(age: 0f, entityId: 7));
        Assert.Equal(0f, atZero.Get("nose").Rotation.X, 5);
        Assert.Equal(2.5f * ToRad, atZero.Get("nose").Rotation.Z, 5);
        AssertVec(Vector3.Zero, atZero.Get("nose").Position);

        // A quarter of the wobble cycle: age * 0.07 = π/2 -> pitch reaches 4.5° and roll returns to ~0.
        var quarterAge = (MathF.PI / 2f) / 0.07f;
        var quarter = Apply(Frame(age: quarterAge, entityId: 7));
        Assert.Equal(4.5f * ToRad, quarter.Get("nose").Rotation.X, 4);
        Assert.Equal(0f, quarter.Get("nose").Rotation.Z, 4);

        // entityId 10 has remainder 0, so speed is 0: the pitch is frozen at sin(0) = 0 and the roll at cos(0) = 2.5°.
        var still = Apply(Frame(age: 12.3f, entityId: 10));
        Assert.Equal(0f, still.Get("nose").Rotation.X, 5);
        Assert.Equal(2.5f * ToRad, still.Get("nose").Rotation.Z, 5);
    }

    [Fact]
    public void HoldingAnItemPullsTheNoseUpAndClampsItsPitchButKeepsTheRoll()
    {
        // Choose an age where both sin and cos are non-zero so the kept roll is visible: age * 0.07 = π/4.
        var age = (MathF.PI / 4f) / 0.07f;
        var free = Apply(Frame(age: age, entityId: 7));
        var holding = Apply(Frame(age: age, entityId: 7, hasMainHand: true));

        // Pitch is overridden to the reference clamp, roll is carried over unchanged.
        Assert.Equal(-0.9f, holding.Get("nose").Rotation.X, 5);
        Assert.Equal(free.Get("nose").Rotation.Z, holding.Get("nose").Rotation.Z, 5);
        Assert.Equal(2.5f * ToRad * MathF.Cos(MathF.PI / 4f), holding.Get("nose").Rotation.Z, 4);

        // Reference nose.setPos(0, 1, -1.5) is absolute; the offset is relative to the nose bind pivot. In the
        // mesher's y-up storage the target pivot is (0, -1, -1.5) (Y negated) and the bind pivot is (0, 2, 0),
        // so the additive offset is (0, -3, -1.5).
        var nosePart = WitchModelDefinition.Definition.Parts.First(part => part.Name == "nose");
        var noseOffset = holding.Get("nose").Position;
        AssertVec(new Vector3(0f, -3f, -1.5f), noseOffset);
        Assert.Equal(0f, nosePart.PivotX + noseOffset.X, 5);
        Assert.Equal(-1f, nosePart.PivotY + noseOffset.Y, 5);
        Assert.Equal(-1.5f, nosePart.PivotZ + noseOffset.Z, 5);

        // Not holding leaves the nose at its bind pivot.
        AssertVec(Vector3.Zero, free.Get("nose").Position);
    }

    [Fact]
    public void ApplyClipReconstructsIdleWalkAndHoldItem()
    {
        var provider = new WitchAnimationProvider(WitchModelDefinition.WitchEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();

        var pose = new EntityPose();
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(0f, pose.Get("right_leg").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("left_leg").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "walk", 0f, pose);
        Assert.Equal(0.7f, pose.Get("right_leg").Rotation.X, 5);
        Assert.Equal(-0.7f, pose.Get("left_leg").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "hold_item", 0f, pose);
        Assert.Equal(-0.9f, pose.Get("nose").Rotation.X, 5);
        AssertVec(new Vector3(0f, -3f, -1.5f), pose.Get("nose").Position);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheWitchModel()
    {
        var modelParts = WitchModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame());

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("nose", modelParts);
        Assert.Contains("head", modelParts);
        Assert.Contains("right_leg", modelParts);
        Assert.Contains("left_leg", modelParts);
    }
}
