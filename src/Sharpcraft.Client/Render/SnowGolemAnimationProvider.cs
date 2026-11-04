// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>SnowGolemModel.setupAnim</c>: fully procedural. The head looks along the render-state pitch/yaw, the
/// upper body follows the yaw at a quarter turn, and the two (root-level) arms are repositioned to swing around the
/// body as it turns, with their own yaw following the upper body.
/// </summary>
/// <remarks>
/// Reference values: <c>head.yRot = yaw</c>, <c>head.xRot = pitch</c>, <c>upperBody.yRot = yaw * 0.25</c>;
/// with <c>s = sin(upperYaw)</c>, <c>c = cos(upperYaw)</c> it sets
/// <c>leftArm.x = c*5, leftArm.z = -s*5</c> and <c>rightArm.x = -c*5, rightArm.z = s*5</c>, plus
/// <c>leftArm.yRot = upperYaw</c>, <c>rightArm.yRot = upperYaw + π</c>.
/// <para>
/// The reference writes absolute part positions/rotations. Our pose system is additive over the model bind pose, so
/// this provider emits deltas: the arm rotations subtract their bind y-rotation (right arm bind = π, so its y delta is
/// <c>upperYaw</c>), and the arm x/z positions subtract the bind pivots. The mesher stores arm X mirrored (the golem
/// parts use <c>FromVanilla</c> with the default <c>mirrorX: true</c>), so the X deltas are negated:
/// left x = <c>5(1 - c)</c>, right x = <c>5(c - 1)</c>; left z = <c>-5s - 1</c>, right z = <c>5s + 1</c>. The arm bind
/// z-rotations (∓1) are never touched. Rotations keep the reference signs.
/// </para>
/// </remarks>
public sealed class SnowGolemAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "look"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference arm reach (5 model units) and bind pivots (x ±5, z ±1).</summary>
    private const float ArmReach = 5f;
    private const float ArmBindX = 5f;
    private const float ArmBindZ = 1f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyPose(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "look", StringComparison.Ordinal))
        {
            ApplyPose(frame, pose);
        }
        else
        {
            // "idle": a neutral (zero) look, so the head/upper body/arms sit in their rest configuration.
            ApplyPose(frame with { Pitch = 0f, Yaw = 0f }, pose);
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, EntityPose pose)
    {
        var headPitch = frame.Pitch * DegreesToRadians;
        var headYaw = frame.Yaw * DegreesToRadians;
        var upperYaw = headYaw * 0.25f;

        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headPitch, headYaw, 0f), Vector3.Zero));
        pose.Add("upper_body", new PoseOffset(Vector3.Zero, new Vector3(0f, upperYaw, 0f), Vector3.Zero));

        var sin = MathF.Sin(upperYaw);
        var cos = MathF.Cos(upperYaw);

        // Left arm: reference bind pivot (5, 6, 1), engine X mirrored. target x = cos*5, z = -sin*5.
        //   engine x delta = -(cos*5 - 5) = 5(1 - cos); z delta = (-sin*5) - 1.
        pose.Add("left_arm", new PoseOffset(
            new Vector3((ArmReach * (1f - cos)), 0f, (-ArmReach * sin) - ArmBindZ),
            new Vector3(0f, upperYaw, 0f),
            Vector3.Zero));

        // Right arm: reference bind pivot (-5, 6, -1), engine X mirrored. target x = -cos*5, z = sin*5.
        //   engine x delta = cos*5 - 5 = 5(cos - 1); z delta = sin*5 - (-1).
        pose.Add("right_arm", new PoseOffset(
            new Vector3((ArmReach * (cos - 1f)), 0f, (ArmReach * sin) + ArmBindZ),
            new Vector3(0f, upperYaw, 0f),
            Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
