// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Hoglin procedural animation ported from the reference <c>HoglinModel.setupAnim</c>
/// (docs/analysis/render/entity-animation.md). These tests pin the ear sway, head yaw, headbutt endpoints, the
/// four-leg cosine gait, and the entity-event/tick attack countdown.
/// </summary>
public sealed class HoglinAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    /// <summary>The reference headbutt travel: ATTACK_HEAD_X_ROT_END − DEFAULT_HEAD_X_ROT = −π/9 − 0.87266463.</summary>
    private const float HeadButtSpan = -(MathF.PI / 9f) - 0.87266463f;

    private static EntityAnimationFrame Frame(float walkPos = 0f, float walkSpeed = 0f, float yaw = 0f, float pitch = 0f) => new(
        TypeId: HoglinModelDefinition.HoglinEntityTypeId,
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

    private static EntityPose Apply(HoglinAnimationProvider provider, IEntityAnimationState state, EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderExposesReferenceClipsAndTypeId()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);

        Assert.Equal(HoglinModelDefinition.HoglinEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "attack" }, provider.Clips);

        var state = provider.CreateState();
        Assert.IsAssignableFrom<IEntityEventAnimationState>(state);
        Assert.IsAssignableFrom<IEntityTickAnimationState>(state);
    }

    [Fact]
    public void EarsSwayOppositeAroundTheirRestAngles()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);
        var state = provider.CreateState();

        // Rest: sin(0) = 0, so both ears sit at the bind pose.
        var rest = Apply(provider, state, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(0f, rest.Get("right_ear").Rotation.Z, 4);
        Assert.Equal(0f, rest.Get("left_ear").Rotation.Z, 4);

        // Quarter cycle: sin(π/2) = 1, right ear leads negative, left ear positive (reference signs).
        var quarter = Apply(provider, state, Frame(walkPos: MathF.PI / 2f, walkSpeed: 1f));
        Assert.Equal(-1f, quarter.Get("right_ear").Rotation.Z, 4);
        Assert.Equal(1f, quarter.Get("left_ear").Rotation.Z, 4);

        // No walk speed: no sway even at a quarter phase.
        var still = Apply(provider, state, Frame(walkPos: MathF.PI / 2f, walkSpeed: 0f));
        Assert.Equal(0f, still.Get("right_ear").Rotation.Z, 4);
        Assert.Equal(0f, still.Get("left_ear").Rotation.Z, 4);
    }

    [Fact]
    public void HeadYawIsWrittenInRadians()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);
        var state = provider.CreateState();

        var right = Apply(provider, state, Frame(yaw: 30f));
        Assert.Equal(30f * ToRad, right.Get("head").Rotation.Y, 4);

        var left = Apply(provider, state, Frame(yaw: -30f));
        Assert.Equal(-30f * ToRad, left.Get("head").Rotation.Y, 4);

        // The headbutt is idle at a zero counter, so the head X offset stays at the bind pose.
        Assert.Equal(0f, right.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void HeadbuttEasesBetweenTheReferenceAngles()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);

        // Zero counter: factor 0, head stays at its rest angle.
        var idle = provider.CreateState();
        Assert.Equal(0f, Apply(provider, idle, Frame()).Get("head").Rotation.X, 4);

        // Counter 10 (just armed): factor 0, still at rest.
        var armed = provider.CreateState();
        var events = Assert.IsAssignableFrom<IEntityEventAnimationState>(armed);
        events.OnEntityEvent(4, 0);
        Assert.Equal(0f, Apply(provider, armed, Frame()).Get("head").Rotation.X, 4);

        // Counter 5: factor 1, the full travel to −π/9 is applied as the delta from rest.
        var mid = provider.CreateState();
        var midEvents = Assert.IsAssignableFrom<IEntityEventAnimationState>(mid);
        var midTicks = Assert.IsAssignableFrom<IEntityTickAnimationState>(mid);
        midEvents.OnEntityEvent(4, 0);
        for (var i = 0; i < 5; i++)
        {
            midTicks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        Assert.Equal(HeadButtSpan, Apply(provider, mid, Frame()).Get("head").Rotation.X, 4);
    }

    [Fact]
    public void LegsFollowTheReferenceCosineGait()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);
        var state = provider.CreateState();

        // Phase 0: cos = 1. Front right leads positive; the hind pair mirrors the opposite front leg.
        var start = Apply(provider, state, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(1.2f, start.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-1.2f, start.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(-1.2f, start.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(1.2f, start.Get("left_hind_leg").Rotation.X, 4);

        // Phase π: cos = −1, the gait flips.
        var opposite = Apply(provider, state, Frame(walkPos: MathF.PI, walkSpeed: 1f));
        Assert.Equal(-1.2f, opposite.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(1.2f, opposite.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(1.2f, opposite.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.2f, opposite.Get("left_hind_leg").Rotation.X, 4);

        // Phase π/2: cos = 0, legs cross the neutral position.
        var crossing = Apply(provider, state, Frame(walkPos: MathF.PI / 2f, walkSpeed: 1f));
        Assert.Equal(0f, crossing.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(0f, crossing.Get("left_front_leg").Rotation.X, 4);

        // Speed zero: no leg swing.
        var still = Apply(provider, state, Frame(walkPos: 0f, walkSpeed: 0f));
        Assert.Equal(Vector3.Zero, still.Get("right_front_leg").Rotation);
        Assert.Equal(Vector3.Zero, still.Get("left_hind_leg").Rotation);
    }

    [Fact]
    public void EntityEventArmsTheCountdownAndTicksDecrementIt()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);
        var state = provider.CreateState();
        var events = Assert.IsAssignableFrom<IEntityEventAnimationState>(state);
        var ticks = Assert.IsAssignableFrom<IEntityTickAnimationState>(state);

        events.OnEntityEvent(4, 0); // START_ATTACKING → counter 10.
        for (var i = 0; i < 5; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        // Counter 5 = mid attack = full headbutt.
        Assert.Equal(HeadButtSpan, Apply(provider, state, Frame()).Get("head").Rotation.X, 4);

        // An unrelated event must not reset the countdown.
        events.OnEntityEvent(99, 5);
        Assert.Equal(HeadButtSpan, Apply(provider, state, Frame()).Get("head").Rotation.X, 4);

        // Five more ticks reach zero and the head returns to rest; further ticks clamp at zero.
        for (var i = 5; i < 12; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        Assert.Equal(0f, Apply(provider, state, Frame()).Get("head").Rotation.X, 4);

        // Re-arming restarts the ten-tick countdown.
        events.OnEntityEvent(4, 12);
        for (var i = 12; i < 17; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        Assert.Equal(HeadButtSpan, Apply(provider, state, Frame()).Get("head").Rotation.X, 4);
    }

    [Fact]
    public void ClipsPreviewIdleWalkAndAttack()
    {
        var provider = new HoglinAnimationProvider(HoglinModelDefinition.HoglinEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();

        var idle = new EntityPose();
        provider.ApplyClip(state, in frame, "idle", 0.5f, idle);
        Assert.Equal(Vector3.Zero, idle.Get("right_front_leg").Rotation);
        Assert.Equal(0f, idle.Get("head").Rotation.X, 4);

        var walk = new EntityPose();
        provider.ApplyClip(state, in frame, "walk", 0.2f, walk);
        Assert.Equal(MathF.Cos(2f) * 1.2f, walk.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(MathF.Cos(2f + MathF.PI) * 1.2f, walk.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(walk.Get("left_front_leg").Rotation.X, walk.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(walk.Get("right_front_leg").Rotation.X, walk.Get("left_hind_leg").Rotation.X, 4);

        var attack = new EntityPose();
        provider.ApplyClip(state, in frame, "attack", 0.25f, attack);
        Assert.Equal(HeadButtSpan, attack.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheHoglinModel()
    {
        var modelParts = HoglinModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var partName in new[]
                 {
                     "right_ear", "left_ear", "head",
                     "right_front_leg", "left_front_leg", "right_hind_leg", "left_hind_leg"
                 })
        {
            Assert.Contains(partName, modelParts);
        }
    }
}
