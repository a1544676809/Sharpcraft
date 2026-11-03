// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// First animation phase controllers. These are behaviour data ported from the reference setupAnim methods, not
/// resources; the evaluator is <see cref="KeyframeAnimation"/> for keyframe animations.
/// </summary>
public static class EntityAnimationController
{
    /// <summary>Animation clip names exposed for a model; empty means the model has no animation in this phase.</summary>
    public static IReadOnlyList<string> ClipNames(int typeId) => typeId switch
    {
        PigModelDefinition.PigEntityTypeId => ["idle", "walk"],
        CamelModelDefinition.CamelEntityTypeId => ["idle", "walk"],
        154 or 39 or 68 => ["idle", "walk"],
        VillagerModelDefinition.VillagerEntityTypeId => ["idle", "walk"],
        _ => []
    };

    /// <summary>Applies a named clip at an explicit elapsed time (used by the sandbox playback controls).</summary>
    public static void ApplyClip(EntityRenderState state, EntityPose pose, string animation, float seconds)
    {
        if (animation == "walk" && state.TypeId == CamelModelDefinition.CamelEntityTypeId)
        {
            CamelAnimations.WalkBaked.Apply(seconds, 1f, pose);
            return;
        }

        if (animation == "idle" && state.TypeId == CamelModelDefinition.CamelEntityTypeId)
        {
            CamelAnimations.IdleBaked.Apply(seconds, 1f, pose);
            return;
        }

        if (animation == "walk")
        {
            var frame = state with
            {
                WalkAnimationPos = seconds * 10f,
                WalkAnimationSpeed = 1f
            };
            Apply(frame, pose);
        }
    }
    /// <summary>Builds the pose overrides for one entity state.</summary>
    public static void Apply(EntityRenderState state, EntityPose pose)
    {
        if (state.TypeId == PigModelDefinition.PigEntityTypeId)
        {
            ApplyQuadrupedWalk(state, pose);
        }
        else if (state.TypeId == CamelModelDefinition.CamelEntityTypeId)
        {
            CamelAnimations.WalkBaked.Apply(state.WalkAnimationPos * 0.1f, MathF.Min(state.WalkAnimationSpeed * 2.5f, 1f), pose);
            CamelAnimations.IdleBaked.Apply(state.AgeInTicks / 20f, 1f, pose);
        }
        else if (state.TypeId == 154 || state.TypeId == 39 || state.TypeId == 68)
        {
            ApplyHumanoidWalk(state, pose);
        }
        else if (state.TypeId == VillagerModelDefinition.VillagerEntityTypeId)
        {
            ApplyVillagerWalk(state, pose);
        }
    }

    /// <summary>Reference QuadrupedModel.setupAnim: head pitch and four legs from walkAnimationPos/Speed.</summary>
    private static void ApplyHumanoidWalk(EntityRenderState state, EntityPose pose)
    {
        var headPitch = state.Pitch * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));
        var phase = state.WalkAnimationPos * 0.6662f;
        var speed = state.WalkAnimationSpeed;
        pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * speed, 0f, 0f), Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * speed, 0f, 0f), Vector3.Zero));
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * 1.4f * speed, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * 1.4f * speed, 0f, 0f), Vector3.Zero));
    }

    private static void ApplyVillagerWalk(EntityRenderState state, EntityPose pose)
    {
        var headPitch = state.Pitch * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));
        var phase = state.WalkAnimationPos * 0.6662f;
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * 1.4f * state.WalkAnimationSpeed * 0.5f, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * 1.4f * state.WalkAnimationSpeed * 0.5f, 0f, 0f), Vector3.Zero));
    }
    private static void ApplyQuadrupedWalk(EntityRenderState state, EntityPose pose)
    {
        var headPitch = state.Pitch * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));

        var phase = state.WalkAnimationPos * 0.6662f;
        var amplitude = 1.4f * state.WalkAnimationSpeed;
        var rightHind = MathF.Cos(phase) * amplitude;
        var leftHind = MathF.Cos(phase + MathF.PI) * amplitude;
        var rightFront = leftHind;
        var leftFront = rightHind;

        AddLegRotation(pose, "right_hind_leg", rightHind);
        AddLegRotation(pose, "left_hind_leg", leftHind);
        AddLegRotation(pose, "right_front_leg", rightFront);
        AddLegRotation(pose, "left_front_leg", leftFront);
    }

    private static void AddLegRotation(EntityPose pose, string part, float radians) =>
        pose.Add(part, new PoseOffset(Vector3.Zero, new Vector3(radians, 0f, 0f), Vector3.Zero));
}





