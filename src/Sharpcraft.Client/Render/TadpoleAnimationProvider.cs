// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>TadpoleModel.setupAnim</c>: fully procedural. The single animated part is the tail, whose yaw follows
/// <c>-amplitude * 0.25 * sin(0.3 * ageInTicks)</c>, where the amplitude is <c>1.0</c> in water and <c>1.5</c> out of
/// water (a larger sweep on land).
/// </summary>
/// <remarks>
/// Only the tail is animated; the body stays in its bind pose. The tail yaw is a rotation about Y, which keeps the
/// reference sign in the pose system (rotations are added to the part rotations). Part names match
/// <see cref="TadpoleModelDefinition"/> (body, tail).
/// </remarks>
public sealed class TadpoleAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "swim"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyTail(frame.AgeInTicks, frame.IsInWater, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // "swim" plays with the in-water amplitude, "idle" with the out-of-water amplitude. Seconds are the clip clock;
        // the reference samples ageInTicks, so convert 20 ticks per second.
        var inWater = string.Equals(clip, "swim", StringComparison.Ordinal);
        ApplyTail(seconds * 20f, inWater, pose);
    }

    private static void ApplyTail(float ageInTicks, bool inWater, EntityPose pose)
    {
        var amplitude = inWater ? 1.0f : 1.5f;
        var yaw = -amplitude * 0.25f * MathF.Sin(0.3f * ageInTicks);
        pose.Add("tail", new PoseOffset(Vector3.Zero, new Vector3(0f, yaw, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
