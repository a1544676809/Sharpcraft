// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>HappyGhastModel.setupAnim</c>: it calls <c>GhastModel.animateTentacles</c>, so each of the nine
/// tentacles sways its xRot as <c>0.2·sin(ageInTicks·0.3 + index) + 0.4</c> (the bind xRot is 0, so this is used directly
/// as an additive pose offset).
/// </summary>
/// <remarks>
/// <b>Body squeeze is blocked.</b> The reference also sets body x/y/z scale to 0.9375 while <c>state.bodyItem</c> is
/// non-empty, but the body-item flag is not carried on <see cref="EntityAnimationFrame"/> (nor derivable from a tick), so
/// that branch is intentionally not faked; the body stays at its bind scale. Part names match
/// <see cref="HappyGhastModelDefinition"/> (tentacle0..tentacle8) and are precomputed so the hot path never concatenates
/// strings.
/// </remarks>
public sealed class HappyGhastAnimationProvider(int typeId) : IEntityAnimationProvider
{
    // Precomputed so the hot path never concatenates strings; index i is "tentacle{i}".
    private static readonly string[] TentacleParts =
    [
        "tentacle0", "tentacle1", "tentacle2", "tentacle3", "tentacle4",
        "tentacle5", "tentacle6", "tentacle7", "tentacle8"
    ];

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
        for (var i = 0; i < TentacleParts.Length; i++)
        {
            var xRot = (0.2f * MathF.Sin((ageInTicks * 0.3f) + i)) + 0.4f;
            pose.Add(TentacleParts[i], new PoseOffset(Vector3.Zero, new Vector3(xRot, 0f, 0f), Vector3.Zero));
        }

        // Body squeeze (scale 0.9375 while holding a body item) is blocked: no body-item flag on the frame.
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
