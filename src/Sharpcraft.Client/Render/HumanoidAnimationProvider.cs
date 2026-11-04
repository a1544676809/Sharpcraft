// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>HumanoidModel.setupAnim</c>: head pitch plus opposing arm and leg phases. Zombie, drowned and husk
/// share this controller.
/// </summary>
public sealed class HumanoidAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public HumanoidAnimationProvider(int typeId) => TypeId = typeId;

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

    /// <summary>
    /// Applies the shared <c>HumanoidModel.setupAnim</c> walk pose. <paramref name="applyArms"/> lets callers that
    /// overwrite the arms afterwards (for example <see cref="SkeletonAnimationProvider"/>'s aggressive melee branch)
    /// skip the opposing arm swing.
    /// </summary>
    internal static void ApplyWalk(in EntityAnimationFrame frame, EntityPose pose, bool applyArms = true)
    {
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));
        var phase = frame.WalkAnimationPos * 0.6662f;
        var speed = frame.WalkAnimationSpeed;
        if (applyArms)
        {
            pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * speed, 0f, 0f), Vector3.Zero));
            pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * speed, 0f, 0f), Vector3.Zero));
        }
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * 1.4f * speed, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * 1.4f * speed, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
