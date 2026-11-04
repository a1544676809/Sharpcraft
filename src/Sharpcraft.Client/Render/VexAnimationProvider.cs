// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>VexModel.setupAnim</c> (fully procedural; there are no keyframe definitions to bake). The head looks
/// along the render-state pitch/yaw, the arms carry a small cosine Z-bob around the resting <c>π/5</c> spread, the
/// wings flap from the age in ticks, and while charging the body levels out and the arms snap into the charging pose
/// (empty-hand pose or per-hand item-raise, depending on which hands hold an item).
/// </summary>
/// <remarks>
/// <para>
/// Charging comes from the Vex flags byte: <c>DATA_FLAGS_ID</c> (entity data index 16, BYTE serializer id 0) with bit 0
/// set. <see cref="EntityAnimationFrame"/> carries the render-state pitch/yaw plus
/// <see cref="EntityAnimationFrame.HasMainHandItem"/>/<see cref="EntityAnimationFrame.HasOffHandItem"/>; the latter are
/// mapped to the reference's right/left hand item checks for <c>setArmsCharging</c>.
/// </para>
/// <para>
/// All contributions are additive pose offsets over the zero bind rotation, so each part receives its final reference
/// local rotation (the reference assigns the same values absolutely after <c>resetPose</c>). Part names match
/// <see cref="VexModelDefinition"/>: head, body, right_arm, left_arm, left_wing, right_wing.
/// </para>
/// </remarks>
public sealed class VexAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference <c>DATA_FLAGS_ID</c> accessor index (the Vex-defined byte, after Entity/Mob fields).</summary>
    private const int VexFlagsIndex = 16;

    /// <summary>Reference <c>EntityDataSerializers.BYTE</c> id used by <c>DATA_FLAGS_ID</c>.</summary>
    private const int ByteSerializerId = 0;

    /// <summary>Reference <c>FLAG_IS_CHARGING</c> bit in the flags byte.</summary>
    private const int FlagIsCharging = 1;

    private static readonly string[] NominalClips = ["idle", "charging"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var charging = state is State vex && vex.IsCharging;
        ApplyPose(
            charging,
            frame.AgeInTicks,
            frame.Pitch,
            frame.Yaw,
            frame.HasMainHandItem,
            frame.HasOffHandItem,
            pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clips have no keyframes; the explicit "seconds" is interpreted as elapsed seconds (20 ticks/s),
        // which drives the arm bob and wing flap.
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            ApplyPose(false, seconds * 20f, frame.Pitch, frame.Yaw, frame.HasMainHandItem, frame.HasOffHandItem, pose);
        }
        else if (string.Equals(clip, "charging", StringComparison.Ordinal))
        {
            ApplyPose(true, seconds * 20f, frame.Pitch, frame.Yaw, frame.HasMainHandItem, frame.HasOffHandItem, pose);
        }
    }

    private static void ApplyPose(
        bool charging,
        float ageInTicks,
        float pitchDegrees,
        float yawDegrees,
        bool hasMainHandItem,
        bool hasOffHandItem,
        EntityPose pose)
    {
        // Head look: reference head.yRot = yaw * deg, head.xRot = pitch * deg.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(pitchDegrees * DegreesToRadians, yawDegrees * DegreesToRadians, 0f),
            Vector3.Zero));

        // movingArmZBob = cos(ageInTicks * 5.5 deg) * 0.1; arms rest at ±π/5 around that bob.
        var movingArmZBob = MathF.Cos(ageInTicks * 5.5f * DegreesToRadians) * 0.1f;
        var baseArmZ = (MathF.PI / 5f) + movingArmZBob;
        var rightArm = new Vector3(0f, 0f, baseArmZ);
        var leftArm = new Vector3(0f, 0f, -baseArmZ);

        var bodyXRot = MathF.PI / 20f;
        if (charging)
        {
            bodyXRot = 0f;
            SetArmsCharging(hasMainHandItem, hasOffHandItem, movingArmZBob, ref rightArm, ref leftArm);
        }

        pose.Add("body", new PoseOffset(Vector3.Zero, new Vector3(bodyXRot, 0f, 0f), Vector3.Zero));
        pose.Add("right_arm", new PoseOffset(Vector3.Zero, rightArm, Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(Vector3.Zero, leftArm, Vector3.Zero));

        // Wing flap: leftWing.yRot = 1.0995574 + cos(ageInTicks * 45.836624 deg) * deg * 16.2; right = -left.
        var wingFlap = MathF.Cos(ageInTicks * 45.836624f * DegreesToRadians) * DegreesToRadians * 16.2f;
        var leftWingY = 1.0995574f + wingFlap;
        pose.Add("left_wing", new PoseOffset(
            Vector3.Zero,
            new Vector3(0.47123888f, leftWingY, -0.47123888f),
            Vector3.Zero));
        pose.Add("right_wing", new PoseOffset(
            Vector3.Zero,
            new Vector3(0.47123888f, -leftWingY, 0.47123888f),
            Vector3.Zero));
    }

    /// <summary>
    /// Reference <c>VexModel.setArmsCharging</c>. Both hands empty use the symmetric raised pose; otherwise each hand
    /// that holds an item raises to <c>π*7/6</c> while the other keeps the resting arm spread.
    /// </summary>
    private static void SetArmsCharging(
        bool hasItemInMainHand,
        bool hasItemInOffHand,
        float movingArmZBob,
        ref Vector3 rightArm,
        ref Vector3 leftArm)
    {
        if (!hasItemInMainHand && !hasItemInOffHand)
        {
            rightArm = new Vector3(-1.2217305f, MathF.PI / 12f, -0.47123888f - movingArmZBob);
            leftArm = new Vector3(-1.2217305f, -MathF.PI / 12f, 0.47123888f + movingArmZBob);
            return;
        }

        if (hasItemInMainHand)
        {
            rightArm = new Vector3(MathF.PI * 7f / 6f, MathF.PI / 12f, -0.47123888f - movingArmZBob);
        }

        if (hasItemInOffHand)
        {
            leftArm = new Vector3(MathF.PI * 7f / 6f, -MathF.PI / 12f, 0.47123888f + movingArmZBob);
        }
    }

    /// <summary>
    /// Per-entity Vex state: the synced flags byte. <c>isCharging</c> is bit 0, matching
    /// <c>Vex.isCharging()</c> (<c>getVexFlag(FLAG_IS_CHARGING)</c>).
    /// </summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState
    {
        private int _flags;

        public bool IsCharging => (_flags & FlagIsCharging) != 0;

        public void OnSyncedData(int index, int serializerId, int value)
        {
            // DATA_FLAGS_ID is a byte at index 16; ignore anything else (e.g. the AgeableMob baby boolean at index 16
            // uses serializer 8, and the shared entity flags live elsewhere).
            if (index != VexFlagsIndex || serializerId != ByteSerializerId)
            {
                return;
            }

            _flags = value & 0xFF;
        }
    }
}
