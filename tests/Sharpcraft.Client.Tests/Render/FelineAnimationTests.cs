// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Reference <c>AdultFelineModel.setupAnim</c> for the Cat and Ocelot (docs/modules/entity-animation.md A5).</summary>
public sealed class FelineAnimationTests
{
    private const float Rad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(int typeId, float bodyYaw, float headYaw, float pitch, bool sitting, float walkPos, float walkSpeed) => new(
        TypeId: typeId, AgeInTicks: 5f, WalkAnimationPos: walkPos, WalkAnimationSpeed: walkSpeed,
        Pitch: pitch, Yaw: bodyYaw, Flap: 0f, FlapSpeed: 0f, TentacleAngle: 0f,
        IsResting: false, IsBaby: false, IsHidingInShell: false, HeadYaw: headYaw,
        IsSitting: sitting);

    private static EntityPose Apply(int typeId, in EntityAnimationFrame frame)
    {
        var provider = EntityAnimationRegistry.For(typeId);
        Assert.NotNull(provider);
        var pose = new EntityPose();
        provider!.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    [Theory]
    [InlineData(CatModelDefinition.CatEntityTypeId)]
    [InlineData(CatModelDefinition.OcelotEntityTypeId)]
    public void TheHeadIsAppliedToEveryHeadPartAndTheLegsWalk(int typeId)
    {
        var pose = Apply(typeId, Frame(typeId, bodyYaw: 30f, headYaw: 60f, pitch: 10f, sitting: false, walkPos: 0f, walkSpeed: 1f));

        foreach (var part in new[] { "head_main", "nose", "ear1", "ear2" })
        {
            var head = pose.Get(part).Rotation;
            Assert.Equal(10f * Rad, head.X, 4);
            Assert.Equal(30f * Rad, head.Y, 4);
            Assert.Equal(0f, head.Z, 4);
        }

        // Walk at phase 0, speed 1 (reference phases).
        Assert.Equal(1f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-1f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-1f, pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(1f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(1.7278761f + (MathF.PI / 4f), pose.Get("tail2").Rotation.X, 3);
    }

    [Fact]
    public void SittingAppliesTheReferenceBodyAndLegPose()
    {
        var pose = Apply(CatModelDefinition.CatEntityTypeId, Frame(CatModelDefinition.CatEntityTypeId, 0f, 0f, 0f, sitting: true, walkPos: 0f, walkSpeed: 0f));

        // body absolute pi/4 vs bind pi/2 -> delta -pi/4; body.y += -4 (up) -> Position.Y = +4.
        Assert.Equal((MathF.PI / 4f) - (MathF.PI / 2f), pose.Get("body").Rotation.X, 4);
        Assert.Equal(4f, pose.Get("body").Position.Y, 3);
        Assert.Equal(-MathF.PI / 20f, pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(-MathF.PI / 2f, pose.Get("left_hind_leg").Rotation.X, 4);
        // head.y += -3.3 -> Position.Y = +3.3 (y-up).
        Assert.Equal(3.3f, pose.Get("head_main").Position.Y, 3);
    }

    [Fact]
    public void OcelotIgnoresSittingEvenIfTheFlagIsSet()
    {
        var pose = Apply(CatModelDefinition.OcelotEntityTypeId, Frame(CatModelDefinition.OcelotEntityTypeId, 0f, 0f, 0f, sitting: true, walkPos: 0f, walkSpeed: 0f));
        Assert.Equal(0f, pose.Get("body").Rotation.X, 4);
    }

    [Fact]
    public void SprintingUsesTheReferenceLegPhasesAndTail()
    {
        var frame = Frame(CatModelDefinition.CatEntityTypeId, 0f, 0f, 0f, sitting: false, walkPos: 0f, walkSpeed: 1f) with { IsSprinting = true };
        var pose = Apply(CatModelDefinition.CatEntityTypeId, frame);

        Assert.Equal(1f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(MathF.Cos(0.3f), pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(MathF.Cos(MathF.PI + 0.3f), pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(-1f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(1.7278761f + (MathF.PI / 10f), pose.Get("tail2").Rotation.X, 3);
    }

    [Fact]
    public void LieDownTiltsTheHeadAndSplaysTheLegs()
    {
        var frame = Frame(CatModelDefinition.CatEntityTypeId, 0f, 0f, 0f, sitting: false, walkPos: 0f, walkSpeed: 0f)
            with { LieDownAmount = 1f, LieDownAmountTail = 1f };
        var pose = Apply(CatModelDefinition.CatEntityTypeId, frame);

        Assert.Equal(-1.2707963f, pose.Get("head_main").Rotation.Z, 4);
        Assert.Equal(1.2707963f, pose.Get("head_main").Rotation.Y, 4);
        Assert.Equal(-1.2707963f, pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(-0.2f, pose.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(0.8f, pose.Get("tail1").Rotation.X, 4);
        Assert.Equal(-0.4f, pose.Get("tail2").Rotation.X, 4);
    }

    [Fact]
    public void RelaxStateOneLerpsTheHeadPitch()
    {
        var frame = Frame(CatModelDefinition.CatEntityTypeId, 0f, 0f, pitch: 0f, sitting: false, walkPos: 0f, walkSpeed: 0f) with { RelaxStateOneAmount = 1f };
        var pose = Apply(CatModelDefinition.CatEntityTypeId, frame);
        Assert.Equal(-0.58177644f, pose.Get("head_main").Rotation.X, 4);
    }
}
