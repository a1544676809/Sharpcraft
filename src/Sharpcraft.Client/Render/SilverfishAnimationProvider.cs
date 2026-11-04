// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SilverfishModel.setupAnim</c>: each of the seven root body segments sways its yRot and slides its x
/// position with a phase offset, and the three layer cubes copy the segment they are attached to.
/// </summary>
/// <remarks>
/// For segment <c>i</c>: <c>yRot = cos(ageInTicks·0.9 + i·0.15·π)·π·0.05·(1 + |i−2|)</c> and
/// <c>x = sin(ageInTicks·0.9 + i·0.15·π)·π·0.2·|i−2|</c>. The bind rotation/position are 0, so these absolute values are
/// used directly as additive pose offsets (the mesher keeps the reference x sign for position offsets). Layer copies:
/// layer0 ← segment2.yRot; layer1 ← segment4 (yRot + x); layer2 ← segment1 (yRot + x). Part names match
/// <see cref="SilverfishModelDefinition"/> (segment0..segment6, layer0..layer2) and are precomputed so the hot path never
/// concatenates strings.
/// </remarks>
public sealed class SilverfishAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] SegmentNames =
    [
        "segment0", "segment1", "segment2", "segment3", "segment4", "segment5", "segment6"
    ];

    private static readonly string[] LayerNames = ["layer0", "layer1", "layer2"];

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
        float segment1Y = 0f, segment1X = 0f, segment2Y = 0f, segment4Y = 0f, segment4X = 0f;

        for (var i = 0; i < SegmentNames.Length; i++)
        {
            var phase = (ageInTicks * 0.9f) + (i * 0.15f * MathF.PI);
            var spread = MathF.Abs(i - 2);
            var yRot = MathF.Cos(phase) * MathF.PI * 0.05f * (1 + spread);
            var xPos = MathF.Sin(phase) * MathF.PI * 0.2f * spread;

            pose.Add(SegmentNames[i], new PoseOffset(
                new Vector3(xPos, 0f, 0f),
                new Vector3(0f, yRot, 0f),
                Vector3.Zero));

            // Capture the segments the layer cubes copy.
            switch (i)
            {
                case 1:
                    segment1Y = yRot;
                    segment1X = xPos;
                    break;
                case 2:
                    segment2Y = yRot;
                    break;
                case 4:
                    segment4Y = yRot;
                    segment4X = xPos;
                    break;
            }
        }

        pose.Add(LayerNames[0], new PoseOffset(Vector3.Zero, new Vector3(0f, segment2Y, 0f), Vector3.Zero));
        pose.Add(LayerNames[1], new PoseOffset(new Vector3(segment4X, 0f, 0f), new Vector3(0f, segment4Y, 0f), Vector3.Zero));
        pose.Add(LayerNames[2], new PoseOffset(new Vector3(segment1X, 0f, 0f), new Vector3(0f, segment1Y, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
