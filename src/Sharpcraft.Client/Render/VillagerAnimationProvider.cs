// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>VillagerModel.setupAnim</c>: head pitch plus a half amplitude leg swing.
/// </summary>
public sealed class VillagerAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public VillagerAnimationProvider(int typeId) => TypeId = typeId;

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
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));
        var phase = frame.WalkAnimationPos * 0.6662f;
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * 1.4f * frame.WalkAnimationSpeed * 0.5f, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * 1.4f * frame.WalkAnimationSpeed * 0.5f, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
