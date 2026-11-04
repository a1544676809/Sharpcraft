// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>NautilusModel.setupAnim</c>: the body follows the clamped yaw/pitch and the looping swim clip advances
/// from <c>walkPos + age/5</c> with the walk mapping <c>applyWalk(pos, speed + 0.2, 2.0, 3.0)</c>. Unlike the hostile
/// mob clips this one needs no animation-state clock, so it plays on the hot path in-game.
/// </summary>
public sealed class NautilusAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["swimming"];

    public NautilusAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        // applyBodyRotation: yaw/pitch clamped to ±10 degrees, set on the body.
        var yaw = Math.Clamp(frame.Yaw, -10f, 10f) * (MathF.PI / 180f);
        var pitch = Math.Clamp(frame.Pitch, -10f, 10f) * (MathF.PI / 180f);
        pose.Add("body", new PoseOffset(Vector3.Zero, new Vector3(pitch, yaw, 0f), Vector3.Zero));

        // applyWalk(pos, speed + 0.2, speedFactor 2, scaleFactor 3): seconds = pos * 2 * 0.05, scale = min(speed * 3, 1).
        var animationPos = frame.WalkAnimationPos + (frame.AgeInTicks / 5f);
        var seconds = animationPos * 2f * 0.05f;
        var targetScale = MathF.Min((frame.WalkAnimationSpeed + 0.2f) * 3f, 1f);
        NautilusAnimations.SwimmingBaked.Apply(seconds, targetScale, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "swimming", StringComparison.Ordinal))
        {
            NautilusAnimations.SwimmingBaked.Apply(seconds, 1f, pose);
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
