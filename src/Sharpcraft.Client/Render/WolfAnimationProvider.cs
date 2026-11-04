// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>AdultWolfModel.setupAnim</c>: procedural only (no keyframe clips). The tail wags with the walk phase
/// unless the wolf is angry, the four legs swing from the walk phase while standing, the sitting pose lowers the body /
/// upper body / tail and folds the legs, the head looks along pitch/yaw, and the tail pitch follows the reference
/// <c>tailAngle</c>. Sitting sub-parts are further rolled by <c>shakeOffWater</c>.
/// </summary>
/// <remarks>
/// Derivable from <see cref="EntityAnimationFrame"/> exactly: IsAngry, IsSitting, IsTame, IsBaby (age scale),
/// Pitch/Yaw, WalkAnimationPos/WalkAnimationSpeed. The reference's three local render-state fields are NOT on the frame:
/// <para>
/// - <c>tailAngle</c> (Wolf.getTailAngle): angry value 1.5393804, wild value π/5, and the tame branch now uses the synced
///   health/max-health ratio (health from entity data, max-health from the attribute map).
///
/// Not derivable (reported, not faked): <c>shakeOffWater</c> z-rolls and <c>headRollAngle</c> ("interested" tilt).
/// </para>
/// <para>
/// - <c>shakeAnim</c> (Wolf.getShakeAnim): drives <c>getBodyRollAngle</c>, i.e. <c>body.zRot</c> plus the
///   <c>real_head</c> / <c>upper_body</c> / <c>real_tail</c> z-rolls. Not derivable, so all shake z-rolls are omitted
///   (equivalent to a non-shaking wolf) and reported as blocked.
/// </para>
/// <para>
/// - <c>headRollAngle</c> (Wolf.getHeadRollAngle, the "interested" tilt) also feeds <c>real_head.zRot</c> and is not on
///   the frame; it is omitted and reported as blocked alongside the shake roll.
/// </para>
/// Absolute reference assignments are reduced to deltas from the reference bind values (the mesh stores the negated
/// bind); positions and increments are used directly, matching the sibling Fox provider.
/// </remarks>
public sealed class WolfAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk", "sit"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference <c>AdultWolfModel</c> body bind pitch, subtracted from the sitting absolute assignment.</summary>
    private const float RefBodyBindXRot = MathF.PI / 2f;

    /// <summary>Reference <c>AdultWolfModel</c> upper-body bind pitch, subtracted from the sitting absolute assignment.</summary>
    private const float RefUpperBodyBindXRot = MathF.PI / 2f;

    /// <summary>Reference <c>AdultWolfModel</c> tail bind pitch, subtracted from the <c>tailAngle</c> assignment.</summary>
    private const float RefTailBindXRot = MathF.PI / 5f;

    /// <summary>Reference angry tail angle (<c>Wolf.getTailAngle</c>).</summary>
    private const float AngryTailAngle = 1.5393804f;

    private const float SittingBodyPitch = MathF.PI / 4f;
    private const float SittingUpperBodyPitch = MathF.PI * 2f / 5f;
    private const float SittingHindLegPitch = MathF.PI * 3f / 2f;
    private const float SittingFrontLegPitch = 5.811947f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyPose(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var standing = frame with { IsSitting = false };
        switch (clip)
        {
            case "idle":
                ApplyPose(standing with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, pose);
                break;
            case "walk":
                ApplyPose(standing with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
                break;
            case "sit":
                ApplyPose(frame with { IsSitting = true, WalkAnimationSpeed = 0f }, pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, EntityPose pose)
    {
        var ageScale = frame.IsBaby ? 0.5f : 1f;
        var phase = frame.WalkAnimationPos * 0.6662f;

        // Tail yaw is assigned every frame in the reference; the bind yaw is zero so the delta is the value itself.
        var tailYaw = frame.IsAngry ? 0f : MathF.Cos(phase) * 1.4f * frame.WalkAnimationSpeed;

        // Tail pitch = tailAngle reduced by the reference bind. The tame (health-based) value is blocked, so a tamed
        // wolf keeps the rest delta (0) rather than an invented angle.
        var tailPitch = 0f;
        if (frame.IsAngry)
        {
            tailPitch = AngryTailAngle - RefTailBindXRot;
        }
        else if (!frame.IsTame)
        {
            // Wild tailAngle is π/5, exactly the bind, so the delta is zero.
            tailPitch = 0f;
        }
        else if (frame.MaxHealth > 0f)
        {
            var damageRatio = (frame.MaxHealth - frame.Health) / frame.MaxHealth;
            tailPitch = (0.55f - (damageRatio * 0.4f)) * MathF.PI - RefTailBindXRot;
        }

        if (frame.IsSitting)
        {
            pose.Add("body", new PoseOffset(
                new Vector3(0f, 4f * ageScale, -2f * ageScale),
                new Vector3(SittingBodyPitch - RefBodyBindXRot, 0f, 0f),
                Vector3.Zero));

            pose.Add("upper_body", new PoseOffset(
                new Vector3(0f, 2f, 0f),
                new Vector3(SittingUpperBodyPitch - RefUpperBodyBindXRot, 0f, 0f),
                Vector3.Zero));

            pose.Add("tail", new PoseOffset(
                new Vector3(0f, 9f * ageScale, -2f * ageScale),
                new Vector3(tailPitch, tailYaw, 0f),
                Vector3.Zero));

            var hindLegPosition = new Vector3(0f, 6.7f * ageScale, -5f * ageScale);
            pose.Add("right_hind_leg", new PoseOffset(hindLegPosition, new Vector3(SittingHindLegPitch, 0f, 0f), Vector3.Zero));
            pose.Add("left_hind_leg", new PoseOffset(hindLegPosition, new Vector3(SittingHindLegPitch, 0f, 0f), Vector3.Zero));
            pose.Add("right_front_leg", new PoseOffset(
                new Vector3(0.01f * ageScale, 1f * ageScale, 0f), new Vector3(SittingFrontLegPitch, 0f, 0f), Vector3.Zero));
            pose.Add("left_front_leg", new PoseOffset(
                new Vector3(-0.01f * ageScale, 1f * ageScale, 0f), new Vector3(SittingFrontLegPitch, 0f, 0f), Vector3.Zero));
        }
        else
        {
            var amplitude = 1.4f * frame.WalkAnimationSpeed;
            var hind = MathF.Cos(phase) * amplitude;
            var hindOpposite = MathF.Cos(phase + MathF.PI) * amplitude;
            pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(hind, 0f, 0f), Vector3.Zero));
            pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(hindOpposite, 0f, 0f), Vector3.Zero));
            pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(hindOpposite, 0f, 0f), Vector3.Zero));
            pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(hind, 0f, 0f), Vector3.Zero));
            pose.Add("tail", new PoseOffset(Vector3.Zero, new Vector3(tailPitch, tailYaw, 0f), Vector3.Zero));
        }

        // Head look: assigned every frame, including while sitting.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
