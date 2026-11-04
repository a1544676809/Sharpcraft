// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// Generic animation infrastructure: the ported walk-animation state, its per-tick derivation in the tracker, and the
/// entity-event / tick routing to animation states (docs/modules/entity-animation.md 4).
/// </summary>
public sealed class EntityAnimationInfrastructureTests
{
    [Fact]
    public void WalkAnimationStateMatchesTheReferenceFormulas()
    {
        var walk = new WalkAnimationState();

        walk.Update(1f, 0.4f, 1f);
        Assert.Equal(0.4f, walk.Speed(), 4);
        Assert.Equal(0.4f, walk.Position(), 4);

        walk.Update(1f, 0.4f, 1f);
        Assert.Equal(0.64f, walk.Speed(), 4);
        Assert.Equal(1.04f, walk.Position(), 4);

        Assert.Equal(0.52f, walk.Speed(0.5f), 4);
        Assert.Equal(1.04f - (0.64f * 0.5f), walk.Position(0.5f), 4);

        walk.Stop();
        Assert.Equal(0f, walk.Speed(), 4);
        Assert.Equal(0f, walk.Position(), 4);
    }

    [Fact]
    public void BabyPositionScaleTriplesTheAccumulatedPosition()
    {
        var walk = new WalkAnimationState();
        walk.Update(0.5f, 0.4f, 3f);
        Assert.Equal(0.6f, walk.Position(), 4);
        Assert.Equal(0.2f * 3f, walk.Position(1f), 4);
    }

    [Fact]
    public void TheTrackerDerivesWalkPositionAndSpeedFromMovement()
    {
        var tracker = new EntityTracker { GameTime = 0 };
        tracker.Add(Entity(3, typeId: 101));
        tracker.ApplyMove(new EntityMove(3, 1.0, 0, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: true));

        for (var tick = 1; tick <= 4; tick++)
        {
            tracker.GameTime = tick;
            tracker.Tick();
        }

        var state = tracker.Interpolate(0f)[0];
        Assert.True(state.WalkAnimationSpeed > 0f, $"expected a moving walk speed, was {state.WalkAnimationSpeed}");
        Assert.True(state.WalkAnimationPos > 0f, $"expected an accumulated walk position, was {state.WalkAnimationPos}");
    }

    [Fact]
    public void EntityEventsAndTicksAreRoutedToTheAnimationState()
    {
        var tracker = new EntityTracker { GameTime = 5 };
        var recorder = new RecordingState();
        tracker.AnimationStateFactory = _ => recorder;
        tracker.Add(Entity(4, typeId: 146));

        Assert.True(tracker.ApplyEntityEvent(4, 62));
        Assert.Equal(62, recorder.EventId);
        Assert.Equal(5, recorder.EventGameTime);

        tracker.ApplyMove(new EntityMove(4, 2.0, 0, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: true));
        tracker.GameTime = 6;
        tracker.Tick();
        Assert.Equal(1, recorder.Ticks);
        Assert.True(recorder.LastDistance > 0f);

        Assert.False(tracker.ApplyEntityEvent(999, 4));
    }

    [Fact]
    public void SyncedPoseAndCreatureDataAreRoutedToTheAnimationState()
    {
        var tracker = new EntityTracker { GameTime = 9 };
        var recorder = new RecordingState();
        tracker.AnimationStateFactory = _ => recorder;
        tracker.Add(Entity(5, typeId: 146));

        // Pose index 6 / serializer 20 value 13 (EMERGING); Creaking-style CAN_MOVE at index 16 / boolean 8 value 0.
        EntityDataItem[] items = [new(6, 20, 13), new(16, 8, 0)];
        tracker.ApplyEntityData(5, items);

        Assert.Equal(13, recorder.PoseId);
        Assert.Equal(9, recorder.PoseGameTime);
        Assert.Equal(16, recorder.LastSyncedIndex);
        Assert.Equal(0, recorder.LastSyncedValue);
    }

    [Fact]
    public void HeldItemFlagsComeFromTheEquipmentSlots()
    {
        var tracker = new EntityTracker { GameTime = 0 };
        tracker.Add(Entity(6, typeId: 28));

        var before = tracker.Interpolate(0f)[0];
        Assert.False(before.HasMainHandItem);
        Assert.False(before.HasOffHandItem);

        tracker.ApplyEquipment(6, [new EquipmentItem(0, 5, 1)]);
        var after = tracker.Interpolate(0f)[0];
        Assert.True(after.HasMainHandItem);
        Assert.False(after.HasOffHandItem);
    }

    [Fact]
    public void FlapClockMatchesTheReferenceRule()
    {
        var flap = new FlapAnimationState();

        flap.Update(airborne: true); // speed 0 -> 1.2 clamped to 1; flapping 1 -> 0.9; flap += 1.8
        Assert.Equal(1.0f, flap.Speed(1f), 4);
        Assert.Equal(1.8f, flap.Flap(1f), 4);

        flap.Update(airborne: true); // speed stays 1; flapping 0.9 -> reset 1 -> 0.9; flap += 1.8
        Assert.Equal(1.0f, flap.Speed(1f), 4);
        Assert.Equal(3.6f, flap.Flap(1f), 4);

        flap.Update(airborne: false); // speed 1 - 0.3 = 0.7
        Assert.Equal(0.7f, flap.Speed(1f), 4);
    }

    [Fact]
    public void TheTrackerDerivesGroundAndFlapPresentationState()
    {
        var tracker = new EntityTracker { GameTime = 0 };
        tracker.Add(Entity(8, typeId: 26));
        tracker.ApplyMove(new EntityMove(8, 1.0, 0, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: false));
        tracker.GameTime = 1;
        tracker.Tick();

        var airborne = tracker.Interpolate(1f)[0];
        Assert.False(airborne.IsOnGround);
        Assert.True(airborne.FlapSpeed > 0f, $"expected a flapping wing speed, was {airborne.FlapSpeed}");

        tracker.ApplyMove(new EntityMove(8, 0, 0, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: true));
        tracker.GameTime = 2;
        tracker.Tick();
        Assert.True(tracker.Interpolate(1f)[0].IsOnGround);
    }

    [Fact]
    public void PassengerGraphDrivesRidingFlags()
    {
        var tracker = new EntityTracker { GameTime = 0 };
        tracker.Add(Entity(1, typeId: 65));
        tracker.Add(Entity(2, typeId: 65));

        tracker.ApplyPassengers(1, [2]);
        var states = tracker.Interpolate(0f);

        EntityRenderState? vehicle = null;
        EntityRenderState? passenger = null;
        foreach (var state in states)
        {
            if (state.Id == 1) vehicle = state;
            if (state.Id == 2) passenger = state;
        }

        Assert.True(vehicle!.Value.IsVehicle);
        Assert.False(vehicle.Value.IsPassenger);
        Assert.True(passenger!.Value.IsPassenger);
        Assert.False(passenger.Value.IsVehicle);
    }

    [Fact]
    public void WaterAtTheFeetBlockSetsIsInWater()
    {
        var water = VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse("minecraft:water"));
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.AddChunk(0, 0).SetSection(new ChunkSection(singleState: water, sectionY: 0));

        var tracker = new EntityTracker { World = world, GameTime = 0 };
        tracker.Add(Entity(9, typeId: 130));
        tracker.Tick();

        Assert.True(tracker.Interpolate(1f)[0].IsInWater);
    }

    private static AddedEntity Entity(int id, int typeId) =>
        new(id, Guid.Empty, typeId, 0, 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, 0);

    private sealed class RecordingState : IEntityAnimationState, IEntityEventAnimationState, IEntityTickAnimationState, IEntityPoseAnimationState, IEntitySyncedDataAnimationState
    {
        public int EventId { get; private set; } = -1;
        public int EventGameTime { get; private set; }
        public int Ticks { get; private set; }
        public float LastDistance { get; private set; }
        public int PoseId { get; private set; } = -1;
        public int PoseGameTime { get; private set; }
        public int LastSyncedIndex { get; private set; } = -1;
        public int LastSyncedValue { get; private set; }

        public void OnEntityEvent(int eventId, int gameTime)
        {
            EventId = eventId;
            EventGameTime = gameTime;
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            Ticks++;
            LastDistance = context.HorizontalDistance;
        }

        public void OnPoseChanged(int poseId, int gameTime)
        {
            PoseId = poseId;
            PoseGameTime = gameTime;
        }

        public void OnSyncedData(int index, int serializerId, int value)
        {
            LastSyncedIndex = index;
            LastSyncedValue = value;
        }
    }
}
