// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SpiderModel.setupAnim</c>: fully procedural. The head looks along the render-state pitch/yaw and the
/// eight legs swing in four phase-shifted pairs (hind, middle-hind, middle-front, front) with a yaw swing and a z lift.
/// Both the Spider (127) and the Cave Spider (22) use this model, so one provider serves both type ids.
/// </summary>
/// <remarks>
/// Formula-for-formula port of the reference: <c>animationPos = walkPos * 0.6662</c>; the yaw swings are
/// <c>-cos(animationPos*2 + phase) * 0.4 * speed</c> and the z lifts are <c>abs(sin(animationPos + phase)) * 0.4 * speed</c>.
/// The right legs add the swing/lift and the left legs subtract it. All values are additive rotation offsets over the
/// leg bind pose (rotations keep the reference signs). No leg x rotation is animated.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.spider.SpiderModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/spider/SpiderModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class SpiderAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    private const float DegreesToRadians = MathF.PI / 180f;
    private const float LegAmplitude = 0.4f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyPose(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplyPose(frame with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, EntityPose pose)
    {
        // Head look.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        var animationPos = frame.WalkAnimationPos * 0.6662f;
        var speed = frame.WalkAnimationSpeed;
        var doublePos = animationPos * 2f;

        // Yaw swings: -cos(2*pos + phase) * 0.4 * speed.
        var swingHind = -(MathF.Cos(doublePos) * LegAmplitude) * speed;
        var swingMiddleHind = -(MathF.Cos(doublePos + MathF.PI) * LegAmplitude) * speed;
        var swingMiddleFront = -(MathF.Cos(doublePos + (MathF.PI / 2f)) * LegAmplitude) * speed;
        var swingFront = -(MathF.Cos(doublePos + (MathF.PI * 3f / 2f)) * LegAmplitude) * speed;

        // Z lifts: abs(sin(pos + phase)) * 0.4 * speed.
        var stepHind = MathF.Abs(MathF.Sin(animationPos) * LegAmplitude) * speed;
        var stepMiddleHind = MathF.Abs(MathF.Sin(animationPos + MathF.PI) * LegAmplitude) * speed;
        var stepMiddleFrontHind = MathF.Abs(MathF.Sin(animationPos + (MathF.PI / 2f)) * LegAmplitude) * speed;
        var stepFront = MathF.Abs(MathF.Sin(animationPos + (MathF.PI * 3f / 2f)) * LegAmplitude) * speed;

        AddLegPair(pose, "right_hind_leg", "left_hind_leg", swingHind, stepHind);
        AddLegPair(pose, "right_middle_hind_leg", "left_middle_hind_leg", swingMiddleHind, stepMiddleHind);
        AddLegPair(pose, "right_middle_front_leg", "left_middle_front_leg", swingMiddleFront, stepMiddleFrontHind);
        AddLegPair(pose, "right_front_leg", "left_front_leg", swingFront, stepFront);
    }

    private static void AddLegPair(EntityPose pose, string rightLeg, string leftLeg, float swing, float step)
    {
        pose.Add(rightLeg, new PoseOffset(Vector3.Zero, new Vector3(0f, swing, step), Vector3.Zero));
        pose.Add(leftLeg, new PoseOffset(Vector3.Zero, new Vector3(0f, -swing, -step), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
