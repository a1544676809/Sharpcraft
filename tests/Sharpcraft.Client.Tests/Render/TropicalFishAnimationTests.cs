// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Tropical fish animation ported from <c>TropicalFishSmallModel.setupAnim</c> / <c>TropicalFishLargeModel.setupAnim</c>:
/// both shapes only sway the <c>tail</c>, <c>yRot = -amplitude·0.45·sin(0.6·ageInTicks)</c>, with amplitude 1.0 in water
/// and 1.5 out of water (docs/analysis/render/entity-animation-architecture.md).
/// </summary>
[Collection(NonParallelCollection.Name)]
public sealed class TropicalFishAnimationTests
{
    private const int TypeId = TropicalFishModelDefinition.TropicalFishEntityTypeId;

    private static EntityAnimationFrame Frame(float age = 0f, bool inWater = false) => new(
        TypeId: TypeId,
        AgeInTicks: age,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: 0f,
        Yaw: 0f,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        IsInWater: inWater);

    private static EntityPose Apply(TropicalFishAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTypeIdAndClips()
    {
        var provider = new TropicalFishAnimationProvider();
        Assert.Equal(TypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "swim" }, provider.Clips);
        Assert.NotNull(provider.CreateState());

        Assert.Equal(TypeId, new TropicalFishAnimationProvider(TypeId).TypeId);
    }

    [Fact]
    public void InWaterTailReachesTheReferenceAmplitude()
    {
        var provider = new TropicalFishAnimationProvider();
        // 0.6 * age = pi/2 -> sin = 1, so the tail yRot is exactly -0.45 in water.
        var age = MathF.PI / 1.2f;
        var pose = Apply(provider, provider.CreateState(), Frame(age, inWater: true));

        Assert.Equal(-0.45f, pose.Get("tail").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("tail").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("tail").Rotation.Z, 5);
        Assert.Equal(Vector3.Zero, pose.Get("tail").Position);
    }

    [Fact]
    public void OnlyTheTailIsAnimated()
    {
        var provider = new TropicalFishAnimationProvider();
        var pose = Apply(provider, provider.CreateState(), Frame(1f, inWater: true));

        Assert.Single(pose.Offsets);
        Assert.Contains("tail", pose.Offsets.Keys);
        // Every other part keeps its bind pose (a missing override means zero delta).
        Assert.Equal(Vector3.Zero, pose.Get("body").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("right_fin").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("left_fin").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("top_fin").Rotation);
    }

    [Fact]
    public void OutOfWaterTailUsesTheWiderAmplitude()
    {
        var provider = new TropicalFishAnimationProvider();
        var age = MathF.PI / 1.2f;
        var inWater = Apply(provider, provider.CreateState(), Frame(age, inWater: true));
        var outOfWater = Apply(provider, provider.CreateState(), Frame(age, inWater: false));

        Assert.Equal(-0.45f, inWater.Get("tail").Rotation.Y, 5);
        Assert.Equal(-0.675f, outOfWater.Get("tail").Rotation.Y, 5);
        Assert.Equal(1.5f, outOfWater.Get("tail").Rotation.Y / inWater.Get("tail").Rotation.Y, 5);
    }

    [Fact]
    public void TailIsNeutralAtAgeZero()
    {
        var provider = new TropicalFishAnimationProvider();
        Assert.Equal(0f, Apply(provider, provider.CreateState(), Frame(0f, inWater: true)).Get("tail").Rotation.Y, 6);
        Assert.Equal(0f, Apply(provider, provider.CreateState(), Frame(0f, inWater: false)).Get("tail").Rotation.Y, 6);
    }

    [Fact]
    public void TailFormulaMatchesTheReferenceAtAnArbitraryAge()
    {
        var provider = new TropicalFishAnimationProvider();
        var age = 1f;
        var expectedInWater = -0.45f * MathF.Sin(0.6f * age);
        var expectedOutOfWater = -0.675f * MathF.Sin(0.6f * age);

        Assert.Equal(expectedInWater, Apply(provider, provider.CreateState(), Frame(age, inWater: true)).Get("tail").Rotation.Y, 5);
        Assert.Equal(expectedOutOfWater, Apply(provider, provider.CreateState(), Frame(age, inWater: false)).Get("tail").Rotation.Y, 5);
    }

    [Fact]
    public void ApplyClipSelectsTheWaterAmplitude()
    {
        var provider = new TropicalFishAnimationProvider();
        var state = provider.CreateState();
        var frame = Frame();

        // seconds -> ticks (20/s); choose seconds so 0.6 * age = pi/2.
        var seconds = (MathF.PI / 1.2f) / 20f;

        var swim = new EntityPose();
        provider.ApplyClip(state, in frame, "swim", seconds, swim);
        Assert.Equal(-0.45f, swim.Get("tail").Rotation.Y, 4);

        var idle = new EntityPose();
        provider.ApplyClip(state, in frame, "idle", seconds, idle);
        Assert.Equal(-0.675f, idle.Get("tail").Rotation.Y, 4);
    }

    [Fact]
    public void EveryAnimatedPartExistsInEveryTropicalFishShape()
    {
        var provider = new TropicalFishAnimationProvider();
        var pose = Apply(provider, provider.CreateState(), Frame(1f, inWater: true));

        foreach (var (variant, definition) in TropicalFishModelDefinition.Variants)
        {
            var parts = definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
            Assert.True(parts.Contains("tail"), $"variant '{variant}' is missing the animated tail part");
            foreach (var partName in pose.Offsets.Keys)
            {
                Assert.True(parts.Contains(partName), $"variant '{variant}' is missing animated part '{partName}'");
            }
        }
    }

    [Fact]
    public void ApplyDoesNotAllocateOnceWarm()
    {
        var provider = new TropicalFishAnimationProvider();
        var state = provider.CreateState();
        var pose = new EntityPose();
        var frame = Frame(12.5f, inWater: true);

        for (var i = 0; i < 512; i++)
        {
            pose.Clear();
            provider.Apply(state, in frame, pose);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++)
        {
            pose.Clear();
            provider.Apply(state, in frame, pose);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
