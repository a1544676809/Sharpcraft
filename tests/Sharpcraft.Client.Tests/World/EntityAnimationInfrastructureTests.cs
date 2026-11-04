// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
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
