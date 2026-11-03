// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>QuadrupedModel.setupAnim</c>: head pitch plus four legs from the walk phase. Pig, cow, mooshroom,
/// sheep and goat share this controller; idle is the same formula at walk speed zero.
/// </summary>
public sealed class QuadrupedAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public QuadrupedAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyWalk(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            var walk = frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f };
            ApplyWalk(walk, pose);
        }
        else
        {
            ApplyWalk(frame with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyWalk(in EntityAnimationFrame frame, EntityPose pose)
    {
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(frame.Pitch * (MathF.PI / 180f), 0f, 0f), Vector3.Zero));

        var phase = frame.WalkAnimationPos * 0.6662f;
        var amplitude = 1.4f * frame.WalkAnimationSpeed;
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

    private sealed class State : IEntityAnimationState
    {
    }
}
