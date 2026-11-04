// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>WitherBossModel.setupAnim</c>: the age-driven ribcage/tail bob and the centre-head look are fully
/// derivable from <see cref="EntityAnimationFrame"/> and are ported here; the two side-head aim rotations are not.
/// </summary>
/// <remarks>
/// <para>
/// Implemented (exact reference formulas):
/// </para>
/// <list type="bullet">
/// <item><c>anim = cos(ageInTicks · 0.1)</c>.</item>
/// <item><c>ribcage.xRot = (0.065 + 0.05 · anim) · π</c>; bind is <c>0.065·π</c>, so the additive delta is
/// <c>0.05 · anim · π</c>.</item>
/// <item><c>tail.xRot = (0.265 + 0.1 · anim) · π</c>; bind is <c>0.265·π</c>, so the additive delta is
/// <c>0.1 · anim · π</c>.</item>
/// <item><c>tail.setPos(-2, 6.9 + cos(ribcage.xRot)·10, -0.5 + sin(ribcage.xRot)·10)</c>, expressed as a delta from
/// the bind with the mesher's y-up storage convention (Y negated, X/Z unchanged).</item>
/// <item><c>center_head.yRot = state.yRot</c> — the head yaw relative to the body, <c>HeadYaw − Yaw</c>;
/// <c>center_head.xRot = state.xRot</c> — the entity pitch. Both bind rotations are 0, so the pose offset equals the
/// reference value.</item>
/// </list>
/// <para>
/// Rotation offsets use the reference signs; the mesher stores the X/Y bind negations in the model definition (see
/// <c>PlayerModelDefinition.FromVanilla</c> and <c>FoxAnimationProvider.Rotation</c>), consistent with the other
/// procedural providers.
/// </para>
/// <para>
/// Blocked (left at the bind pose): <c>right_head</c> / <c>left_head</c> require <c>WitherRenderState.xHeadRots[2]</c>
/// and <c>yHeadRots[2]</c> plus <c>bodyRot</c>. Those arrays are the client-side per-head aim rotations computed in
/// <c>WitherBoss.aiStep</c> from each head's alternative target entity and head world position; they are independent
/// of the single synced head yaw / body yaw and therefore cannot be derived from <see cref="EntityAnimationFrame"/>.
/// </para>
/// </remarks>
public sealed class WitherAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference <c>ribcage</c> bind xRot (<c>0.065·π</c>) in radians.</summary>
    private const float RibcageBindXRot = 0.20420352f;

    /// <summary>Reference <c>tail</c> bind xRot (<c>0.265·π</c>) in radians.</summary>
    private const float TailBindXRot = 0.83252203f;

    /// <summary>Reference <c>tail</c> bind pivot Y (<c>6.9 + cos(0.20420352)·10</c>).</summary>
    private const float TailBindY = 16.692228f;

    /// <summary>Reference <c>tail</c> bind pivot Z (<c>-0.5 + sin(0.20420352)·10</c>).</summary>
    private const float TailBindZ = 1.527873f;

    private static readonly string[] NominalClips = ["idle"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame.AgeInTicks + frame.PartialTick, frame.Pitch, frame.HeadYaw, frame.Yaw, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            ApplyPose(seconds * 20f, frame.Pitch, frame.HeadYaw, frame.Yaw, pose);
        }
    }

    private static void ApplyPose(float ageInTicks, float pitch, float headYaw, float yaw, EntityPose pose)
    {
        var anim = MathF.Cos(ageInTicks * 0.1f);

        // ribcage bob: additive delta from the reference bind (0.065π).
        var ribcageRefX = (0.065f + 0.05f * anim) * MathF.PI;
        pose.Add("ribcage", Rotation(ribcageRefX - RibcageBindXRot, 0f, 0f));

        // tail follows the ribcage angle; setPos is absolute, so it becomes a bind delta (y-up: Y negated).
        var tailRefX = (0.265f + 0.1f * anim) * MathF.PI;
        var tailRefY = 6.9f + MathF.Cos(ribcageRefX) * 10f;
        var tailRefZ = -0.5f + MathF.Sin(ribcageRefX) * 10f;
        pose.Add("tail", new PoseOffset(
            new Vector3(0f, -(tailRefY - TailBindY), tailRefZ - TailBindZ),
            new Vector3(tailRefX - TailBindXRot, 0f, 0f),
            Vector3.Zero));

        // centre head look: state.yRot = headRot - bodyRot (relative), state.xRot = pitch.
        var relativeYaw = WrapDegrees(headYaw - yaw);
        pose.Add("center_head", Rotation(pitch * DegreesToRadians, relativeYaw * DegreesToRadians, 0f));
    }

    private static PoseOffset Rotation(float x, float y, float z) =>
        new(Vector3.Zero, new Vector3(x, y, z), Vector3.Zero);

    private static float WrapDegrees(float degrees)
    {
        var wrapped = degrees % 360f;
        if (wrapped >= 180f)
        {
            wrapped -= 360f;
        }
        else if (wrapped < -180f)
        {
            wrapped += 360f;
        }

        return wrapped;
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
