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
        PigModelDefinition.PigEntityTypeId or CowModelDefinition.CowEntityTypeId or CowModelDefinition.MooshroomEntityTypeId
            or SheepModelDefinition.SheepEntityTypeId or GoatModelDefinition.GoatEntityTypeId => ["idle", "walk"],
        HorseModelDefinition.HorseEntityTypeId or HorseModelDefinition.DonkeyEntityTypeId
            or HorseModelDefinition.MuleEntityTypeId or HorseModelDefinition.SkeletonHorseEntityTypeId
            or HorseModelDefinition.ZombieHorseEntityTypeId => ["idle", "walk"],
        CamelModelDefinition.CamelEntityTypeId => ["idle", "walk"],
        154 or 39 or 68 => ["idle", "walk"],
        VillagerModelDefinition.VillagerEntityTypeId or ChickenModelDefinition.ChickenEntityTypeId => ["idle", "walk"],
        SquidModelDefinition.SquidEntityTypeId or SquidModelDefinition.GlowSquidEntityTypeId => ["idle"],
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
                WalkAnimationSpeed = 1f,
                Flap = state.TypeId == ChickenModelDefinition.ChickenEntityTypeId ? seconds * 20f : state.Flap,
                FlapSpeed = state.TypeId == ChickenModelDefinition.ChickenEntityTypeId ? 1f : state.FlapSpeed
            };
            Apply(frame, pose);
        }
    }
    /// <summary>Builds the pose overrides for one entity state.</summary>
    public static void Apply(EntityRenderState state, EntityPose pose)
    {
        if (state.TypeId == PigModelDefinition.PigEntityTypeId
            || state.TypeId == CowModelDefinition.CowEntityTypeId
            || state.TypeId == CowModelDefinition.MooshroomEntityTypeId
            || state.TypeId == SheepModelDefinition.SheepEntityTypeId
            || state.TypeId == GoatModelDefinition.GoatEntityTypeId)
        {
            ApplyQuadrupedWalk(state, pose);
        }
        else if (state.TypeId == CamelModelDefinition.CamelEntityTypeId)
        {
            CamelAnimations.WalkBaked.Apply(state.WalkAnimationPos * 0.1f, MathF.Min(state.WalkAnimationSpeed * 2.5f, 1f), pose);
            CamelAnimations.IdleBaked.Apply(state.AgeInTicks / 20f, 1f, pose);
        }
        else if (state.TypeId == HorseModelDefinition.HorseEntityTypeId
            || state.TypeId == HorseModelDefinition.DonkeyEntityTypeId
            || state.TypeId == HorseModelDefinition.MuleEntityTypeId
            || state.TypeId == HorseModelDefinition.SkeletonHorseEntityTypeId
            || state.TypeId == HorseModelDefinition.ZombieHorseEntityTypeId)
        {
            ApplyEquineWalk(state, pose);
        }
        else if (state.TypeId == 154 || state.TypeId == 39 || state.TypeId == 68)
        {
            ApplyHumanoidWalk(state, pose);
        }
        else if (state.TypeId == ChickenModelDefinition.ChickenEntityTypeId)
        {
            ApplyChicken(state, pose);
        }
        else if (state.TypeId == SquidModelDefinition.SquidEntityTypeId || state.TypeId == SquidModelDefinition.GlowSquidEntityTypeId)
        {
            ApplySquid(state, pose);
        }
        else if (state.TypeId == VillagerModelDefinition.VillagerEntityTypeId)
        {
            ApplyVillagerWalk(state, pose);
        }
    }

    /// <summary>Reference QuadrupedModel.setupAnim: head pitch and four legs from walkAnimationPos/Speed.</summary>
    private static void ApplyChicken(EntityRenderState state, EntityPose pose)
    {
        var flap = (MathF.Sin(state.Flap) + 1f) * state.FlapSpeed;
        pose.Add("right_wing", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, flap), Vector3.Zero));
        pose.Add("left_wing", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, -flap), Vector3.Zero));
        var phase = state.WalkAnimationPos * 0.6662f;
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * 1.4f * state.WalkAnimationSpeed, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * 1.4f * state.WalkAnimationSpeed, 0f, 0f), Vector3.Zero));
    }

    private static void ApplySquid(EntityRenderState state, EntityPose pose)
    {
        for (var i = 0; i < 8; i++)
        {
            pose.Add("tentacle" + i, new PoseOffset(Vector3.Zero, new Vector3(state.TentacleAngle, 0f, 0f), Vector3.Zero));
        }
    }
    private static void ApplyEquineWalk(EntityRenderState state, EntityPose pose)
    {
        var speed = state.WalkAnimationSpeed;
        var phase = state.WalkAnimationPos * 0.6662f;
        var headPitch = state.Pitch * (MathF.PI / 180f);
        if (speed > 0.2f)
        {
            headPitch += MathF.Cos(state.WalkAnimationPos * 0.8f) * 0.15f * speed;
        }

        pose.Add("head_parts", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));
        var legAnim = MathF.Cos(phase + MathF.PI);
        var legX = legAnim * 0.8f * speed;
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(legX, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(-legX, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(-legAnim * 0.5f * speed, 0f, 0f), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(legAnim * 0.5f * speed, 0f, 0f), Vector3.Zero));
        pose.Add("tail", new PoseOffset(new Vector3(0f, speed, speed * 2f), new Vector3(speed * 0.75f, 0f, 0f), Vector3.Zero));
    }
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









