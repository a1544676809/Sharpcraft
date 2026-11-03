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
    /// <summary>Builds the pose overrides for one entity state.</summary>
    public static void Apply(EntityRenderState state, EntityPose pose)
    {
        if (state.TypeId == PigModelDefinition.PigEntityTypeId)
        {
            ApplyQuadrupedWalk(state, pose);
        }
    }

    /// <summary>Reference QuadrupedModel.setupAnim: head pitch and four legs from walkAnimationPos/Speed.</summary>
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

