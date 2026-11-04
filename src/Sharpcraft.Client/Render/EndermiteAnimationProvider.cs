// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>EndermiteModel.setupAnim</c>: the four root body segments sway their yRot and slide their x position
/// with a per-segment phase offset. Same shape as <see cref="SilverfishAnimationProvider"/> but with the smaller
/// endermite factors (0.01 for yRot, 0.1 for x) and four segments.
/// </summary>
/// <remarks>
/// For segment <c>i</c> (0..3): <c>yRot = cos(ageInTicks·0.9 + i·0.15·π)·π·0.01·(1 + |i−2|)</c> and
/// <c>x = sin(ageInTicks·0.9 + i·0.15·π)·π·0.1·|i−2|</c>. The bind rotation/position are 0, so these absolute values
/// are used directly as additive pose offsets (the mesher keeps the reference x sign for position offsets). Part names
/// match <see cref="EndermiteModelDefinition"/> (segment0..segment3) and are precomputed so the hot path never
/// concatenates strings.
/// </remarks>
public sealed class EndermiteAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] SegmentNames = ["segment0", "segment1", "segment2", "segment3"];

    private static readonly string[] NominalClips = ["idle"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            ApplyPose(seconds * 20f, pose);
        }
    }

    private static void ApplyPose(float ageInTicks, EntityPose pose)
    {
        for (var i = 0; i < SegmentNames.Length; i++)
        {
            var phase = (ageInTicks * 0.9f) + (i * 0.15f * MathF.PI);
            var spread = MathF.Abs(i - 2);
            var yRot = MathF.Cos(phase) * MathF.PI * 0.01f * (1f + spread);
            var xPos = MathF.Sin(phase) * MathF.PI * 0.1f * spread;

            pose.Add(SegmentNames[i], new PoseOffset(
                new Vector3(xPos, 0f, 0f),
                new Vector3(0f, yRot, 0f),
                Vector3.Zero));
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
