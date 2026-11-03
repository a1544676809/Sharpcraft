// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>ChickenModel.setupAnim</c>: wing flap from the flap clock plus a two-leg walk phase.
/// </summary>
public sealed class ChickenAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public ChickenAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyChicken(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            var walk = frame with
            {
                WalkAnimationPos = seconds * 10f,
                WalkAnimationSpeed = 1f,
                Flap = seconds * 20f,
                FlapSpeed = 1f
            };
            ApplyChicken(walk, pose);
        }
        else
        {
            ApplyChicken(frame with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyChicken(in EntityAnimationFrame frame, EntityPose pose)
    {
        var flap = (MathF.Sin(frame.Flap) + 1f) * frame.FlapSpeed;
        pose.Add("right_wing", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, flap), Vector3.Zero));
        pose.Add("left_wing", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, -flap), Vector3.Zero));
        var phase = frame.WalkAnimationPos * 0.6662f;
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * 1.4f * frame.WalkAnimationSpeed, 0f, 0f), Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * 1.4f * frame.WalkAnimationSpeed, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
