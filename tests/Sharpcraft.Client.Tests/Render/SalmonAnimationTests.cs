// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Salmon procedural pose coverage ported from the reference <c>SalmonModel.setupAnim</c>. Pins the single
/// <c>body_back.yRot</c> sway in and out of water (amplitude/angle multipliers 1.0/1.0 vs 1.3/1.7) and the part contract.
/// </summary>
public sealed class SalmonAnimationTests
{
    private static readonly int TypeId = SalmonModelDefinition.SalmonEntityTypeId;

    private static EntityAnimationFrame Frame(float ageInTicks, bool isInWater) =>
        new(
            TypeId,
            ageInTicks,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            IsInWater: isInWater);

    private static void AssertBodyBackYaw(SalmonAnimationProvider provider, float ageInTicks, bool isInWater, float expected)
    {
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks, isInWater), pose);
        Assert.Equal(expected, pose.Get("body_back").Rotation.Y, 3);
    }

    [Fact]
    public void ProviderExposesTheSalmonTypeIdAndClips()
    {
        var provider = new SalmonAnimationProvider(TypeId);
        Assert.Equal(TypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "swim" }, provider.Clips);
    }

    [Fact]
    public void InWaterSwayMatchesTheReference()
    {
        var provider = new SalmonAnimationProvider(TypeId);

        // amplitude 1.0, angle 1.0: yRot = -0.25 * sin(0.6 * age)
        AssertBodyBackYaw(provider, ageInTicks: 5f, isInWater: true, expected: -0.03528f);
        AssertBodyBackYaw(provider, ageInTicks: 10f, isInWater: true, expected: 0.06985f);
        AssertBodyBackYaw(provider, ageInTicks: 7.5f, isInWater: true, expected: 0.24438f);
    }

    [Fact]
    public void OutOfWaterSwayMatchesTheReference()
    {
        var provider = new SalmonAnimationProvider(TypeId);

        // amplitude 1.3, angle 1.7: yRot = -1.3 * 0.25 * sin(1.7 * 0.6 * age)
        AssertBodyBackYaw(provider, ageInTicks: 5f, isInWater: false, expected: 0.30089f);
        AssertBodyBackYaw(provider, ageInTicks: 10f, isInWater: false, expected: 0.22746f);
        AssertBodyBackYaw(provider, ageInTicks: 7.5f, isInWater: false, expected: -0.31826f);
    }

    [Fact]
    public void AtAgeZeroBothWaterStatesAreNeutral()
    {
        var provider = new SalmonAnimationProvider(TypeId);
        AssertBodyBackYaw(provider, ageInTicks: 0f, isInWater: true, expected: 0f);
        AssertBodyBackYaw(provider, ageInTicks: 0f, isInWater: false, expected: 0f);
    }

    [Fact]
    public void OutOfWaterSwayIsLargerThanInWater()
    {
        var provider = new SalmonAnimationProvider(TypeId);

        var inWater = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(5f, isInWater: true), inWater);
        var outOfWater = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(5f, isInWater: false), outOfWater);

        var inYaw = inWater.Get("body_back").Rotation.Y;
        var outYaw = outOfWater.Get("body_back").Rotation.Y;
        Assert.NotEqual(inYaw, outYaw);
        Assert.True(MathF.Abs(outYaw) > MathF.Abs(inYaw), "the 1.3 amplitude / 1.7 angle multipliers must widen the sway");
    }

    [Fact]
    public void OnlyBodyBackIsDrivenAndItExistsInTheModel()
    {
        var provider = new SalmonAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(5f, isInWater: true), pose);

        Assert.True(
            new HashSet<string>(pose.Offsets.Keys, StringComparer.Ordinal).SetEquals(new[] { "body_back" }),
            "setupAnim only rotates body_back");

        var modelParts = SalmonModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("body_back", modelParts);
        Assert.Contains("body_front", modelParts);
        Assert.Contains("head", modelParts);
        Assert.Contains("back_fin", modelParts);
    }

    [Fact]
    public void ApplyClipReconstructsInAndOutOfWaterAtSecondsTimesTwentyTicks()
    {
        var provider = new SalmonAnimationProvider(TypeId);

        // seconds 0.25 -> age 5 ticks.
        var swim = new EntityPose();
        provider.ApplyClip(provider.CreateState(), Frame(0f, isInWater: false), "swim", 0.25f, swim);
        Assert.Equal(-0.03528f, swim.Get("body_back").Rotation.Y, 3);

        var idle = new EntityPose();
        provider.ApplyClip(provider.CreateState(), Frame(0f, isInWater: true), "idle", 0.25f, idle);
        Assert.Equal(0.30089f, idle.Get("body_back").Rotation.Y, 3);
    }

    [Fact]
    public void ApplyClipIgnoresUnknownNames()
    {
        var provider = new SalmonAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.ApplyClip(provider.CreateState(), Frame(5f, isInWater: true), "not_a_clip", 1f, pose);
        Assert.Empty(pose.Offsets);
    }
}
