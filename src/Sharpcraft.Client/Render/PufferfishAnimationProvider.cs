// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>PufferfishBigModel</c> / <c>PufferfishMidModel</c> / <c>PufferfishSmallModel</c> <c>setupAnim</c>: only
/// the two side fins flap. All three puff-state models use the same formula on their fins —
/// <c>right.zRot = -0.2 + 0.4·sin(ageInTicks·0.2)</c> and <c>left.zRot = 0.2 - 0.4·sin(ageInTicks·0.2)</c> — and every
/// fin bind zRot is 0, so the values are used directly as additive pose offsets (rotation Z keeps the reference sign).
/// </summary>
/// <remarks>
/// The medium and large models name the fins <c>right_blue_fin</c>/<c>left_blue_fin</c>; the small model names them
/// <c>right_fin</c>/<c>left_fin</c>. The active puff state is selected by <c>PufferfishRenderer.submit</c> from
/// <c>PufferfishRenderState.puffState</c>, which is not carried on <see cref="EntityAnimationFrame"/>. Because the
/// formula and the zero bind are identical for every puff state, the pose is written to all four fin names; the mesher
/// only reads the names present in the resolved model, so the inactive names are inert. This keeps the behaviour exact
/// without needing the puff-state input.
/// <para>
/// Not expressible here (renderer-level, reported as blocked): <c>PufferfishRenderer.setupRotations</c> applies a whole
/// entity vertical bob <c>translate(0, cos(ageInTicks·0.05)·0.08, 0)</c>. That is a pose-stack translation, not a model
/// part rotation/offset, so it cannot be reproduced through <see cref="EntityPose"/> without approximation.
/// </para>
/// </remarks>
public sealed class PufferfishAnimationProvider(int typeId) : IEntityAnimationProvider
{
    // Medium/large fins.
    private const string RightBlueFin = "right_blue_fin";
    private const string LeftBlueFin = "left_blue_fin";

    // Small fins.
    private const string RightFin = "right_fin";
    private const string LeftFin = "left_fin";

    private static readonly string[] NominalClips = ["idle"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clip: preview "seconds" converted back to ticks (20 ticks per second).
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            ApplyPose(seconds * 20f, pose);
        }
    }

    private static void ApplyPose(float ageInTicks, EntityPose pose)
    {
        var sway = 0.4f * MathF.Sin(ageInTicks * 0.2f);
        var rightZ = -0.2f + sway;
        var leftZ = 0.2f - sway;
        pose.Add(RightBlueFin, RotationZ(rightZ));
        pose.Add(LeftBlueFin, RotationZ(leftZ));
        pose.Add(RightFin, RotationZ(rightZ));
        pose.Add(LeftFin, RotationZ(leftZ));
    }

    private static PoseOffset RotationZ(float z) =>
        new(Vector3.Zero, new Vector3(0f, 0f, z), Vector3.Zero);

    private sealed class State : IEntityAnimationState
    {
    }
}
