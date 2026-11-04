// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>DolphinModel.setupAnim</c>: the body looks along the render-state pitch/yaw, and while the dolphin is
/// moving it adds a swimming bob to the body xRot plus a cosine flutter on the tail and tail fin.
/// </summary>
/// <remarks>
/// Absolute→additive over the bind pose:
/// <list type="bullet">
/// <item>body xRot = Pitch (bind 0) plus, when moving, <c>−0.05 − 0.05·cos(age·0.3)</c>;</item>
/// <item>tail xRot, when moving, is <c>−0.1·cos(age·0.3)</c> in the reference; the bind tail xRot is −0.10471976, so the
/// offset is <c>−0.1·cos(age·0.3) − (−0.10471976)</c>;</item>
/// <item>tail_fin xRot, when moving, is <c>−0.2·cos(age·0.3)</c> (bind 0).</item>
/// </list>
/// When the dolphin is not moving the tail and tail fin keep their bind pose (no pose entries). Part names match
/// <see cref="DolphinModelDefinition"/>.
/// </remarks>
public sealed class DolphinAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference bind xRot of the <c>tail</c> part (<c>PartPose ... -0.10471976F</c>).</summary>
    private const float TailBindXRot = -0.10471976f;

    private static readonly string[] NominalClips = ["idle", "swim"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks, frame.Pitch, frame.Yaw, frame.IsMoving, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clip: "swim" previews the moving animation, "idle" the still pose; seconds → age in ticks.
        switch (clip)
        {
            case "swim":
                ApplyPose(seconds * 20f, frame.Pitch, frame.Yaw, isMoving: true, pose);
                break;
            case "idle":
                ApplyPose(seconds * 20f, frame.Pitch, frame.Yaw, isMoving: false, pose);
                break;
        }
    }

    private static void ApplyPose(float ageInTicks, float pitch, float yaw, bool isMoving, EntityPose pose)
    {
        var bodyXRot = pitch * DegreesToRadians;
        var bodyYRot = yaw * DegreesToRadians;

        if (isMoving)
        {
            var cos = MathF.Cos(ageInTicks * 0.3f);
            bodyXRot += -0.05f - (0.05f * cos);

            // Tail flutter is absolute in the reference; subtract the bind (reference sign) to get the additive offset.
            pose.Add("tail", new PoseOffset(
                Vector3.Zero,
                new Vector3((-0.1f * cos) - TailBindXRot, 0f, 0f),
                Vector3.Zero));

            pose.Add("tail_fin", new PoseOffset(
                Vector3.Zero,
                new Vector3(-0.2f * cos, 0f, 0f),
                Vector3.Zero));
        }

        pose.Add("body", new PoseOffset(Vector3.Zero, new Vector3(bodyXRot, bodyYRot, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
