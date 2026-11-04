// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Llama procedural animation ported from <c>LlamaModel.setupAnim</c>. Covers the head look applied to the split head
/// group, the diagonal four-leg walk phases, clip reconstruction, and part coverage for the Llama and Trader Llama.
/// </summary>
public sealed class LlamaAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(float pitch = 0f, float yaw = 0f, float walkPos = 0f, float walkSpeed = 0f) => new(
        TypeId: LlamaModelDefinition.LlamaEntityTypeId,
        AgeInTicks: 0f,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new LlamaAnimationProvider(LlamaModelDefinition.LlamaEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTheTwoClipsAndTypeId()
    {
        var provider = new LlamaAnimationProvider(LlamaModelDefinition.LlamaEntityTypeId);
        Assert.Equal(LlamaModelDefinition.LlamaEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLookRotatesTheWholeHeadGroup()
    {
        var pose = Apply(Frame(pitch: 15f, yaw: -30f));
        foreach (var part in new[] { "head", "neck", "left_ear", "right_ear" })
        {
            Assert.Equal(15f * ToRad, pose.Get(part).Rotation.X, 5);
            Assert.Equal(-30f * ToRad, pose.Get(part).Rotation.Y, 5);
            Assert.Equal(0f, pose.Get(part).Rotation.Z, 5);
            AssertVec(Vector3.Zero, pose.Get(part).Position);
        }
    }

    [Fact]
    public void LegsSwingInTheDiagonalWalkPattern()
    {
        // phase 0, speed 1: right_hind +1.4, left_hind -1.4, right_front -1.4, left_front +1.4.
        var start = Apply(Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(1.4f, start.Get("right_hind_leg").Rotation.X, 5);
        Assert.Equal(-1.4f, start.Get("left_hind_leg").Rotation.X, 5);
        Assert.Equal(-1.4f, start.Get("right_front_leg").Rotation.X, 5);
        Assert.Equal(1.4f, start.Get("left_front_leg").Rotation.X, 5);

        // phase π swaps the signs.
        var halfPos = MathF.PI / 0.6662f;
        var half = Apply(Frame(walkPos: halfPos, walkSpeed: 1f));
        Assert.Equal(-1.4f, half.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(1.4f, half.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(1.4f, half.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, half.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void LegAmplitudeScalesWithWalkSpeed()
    {
        var slow = Apply(Frame(walkPos: 0f, walkSpeed: 0.5f));
        Assert.Equal(0.7f, slow.Get("right_hind_leg").Rotation.X, 5);
        Assert.Equal(-0.7f, slow.Get("left_hind_leg").Rotation.X, 5);

        var still = Apply(Frame(walkPos: 1.2f, walkSpeed: 0f));
        foreach (var leg in new[] { "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" })
        {
            Assert.Equal(0f, still.Get(leg).Rotation.X, 6);
        }
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndWalk()
    {
        var provider = new LlamaAnimationProvider(LlamaModelDefinition.LlamaEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 10f, yaw: 20f, walkPos: 3f, walkSpeed: 1f);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "walk", 0f, pose);
        Assert.Equal(1.4f, pose.Get("right_hind_leg").Rotation.X, 5);
        Assert.Equal(10f * ToRad, pose.Get("head").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(10f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(20f * ToRad, pose.Get("neck").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("right_hind_leg").Rotation.X, 6);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheLlamaAndTraderLlamaModels()
    {
        var llamaParts = LlamaModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var traderParts = TraderLlamaModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(pitch: 5f, yaw: 45f, walkPos: 0.8f, walkSpeed: 1f));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, llamaParts);
            Assert.Contains(partName, traderParts);
        }

        // Trader Llama reuses exactly the Llama part set.
        Assert.Equal(llamaParts, traderParts);
        Assert.Contains("head", llamaParts);
        Assert.Contains("neck", llamaParts);
        Assert.Contains("left_ear", llamaParts);
        Assert.Contains("right_ear", llamaParts);
        Assert.Contains("right_front_leg", llamaParts);
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }
}
