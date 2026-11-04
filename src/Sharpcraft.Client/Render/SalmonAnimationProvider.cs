// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SalmonModel.setupAnim</c>: a single procedural tail sway on <c>body_back</c>. In water the amplitude and
/// angle multipliers are 1.0/1.0; out of water they become 1.3/1.7. The only animated channel is
/// <c>body_back.yRot = -amplitude * 0.25 * sin(angle * 0.6 * ageInTicks)</c>.
/// </summary>
/// <remarks>
/// The reference sets an absolute <c>yRot</c> on a root part whose bind rotation is zero, so the additive pose offset is
/// the value itself. No other part is touched by <c>setupAnim</c> (body_front, head and the fins keep their bind pose).
/// </remarks>
public sealed class SalmonAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "swim"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, frame.IsInWater, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clip: "swim" uses the in-water multipliers, "idle" the out-of-water ones; seconds converts to ticks.
        switch (clip)
        {
            case "swim":
                ApplyPose(seconds * 20f, isInWater: true, pose);
                break;
            case "idle":
                ApplyPose(seconds * 20f, isInWater: false, pose);
                break;
        }
    }

    private static void ApplyPose(float ageInTicks, bool isInWater, EntityPose pose)
    {
        // Reference: amplitudeMultiplier 1.0 / angleMultiplier 1.0 in water, 1.3 / 1.7 out of water.
        var amplitudeMultiplier = 1f;
        var angleMultiplier = 1f;
        if (!isInWater)
        {
            amplitudeMultiplier = 1.3f;
            angleMultiplier = 1.7f;
        }

        var yRot = -amplitudeMultiplier * 0.25f * MathF.Sin(angleMultiplier * 0.6f * ageInTicks);
        pose.Add("body_back", new PoseOffset(Vector3.Zero, new Vector3(0f, yRot, 0f), Vector3.Zero));
    }

    /// <summary>Salmon has no stateful clocks; the sway depends only on the frame's age and water flag.</summary>
    private sealed class State : IEntityAnimationState
    {
    }
}
