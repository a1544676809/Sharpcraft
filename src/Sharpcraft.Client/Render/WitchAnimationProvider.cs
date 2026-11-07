// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>WitchModel.setupAnim</c>: fully procedural (no baked clips). Each frame it looks the head along the
/// render-state pitch/yaw, swings the two legs from the walk position/speed, and wobbles the nose at an integer-id
/// derived speed; while the main hand holds an item the nose is pulled up/forward and its pitch is clamped.
/// </summary>
/// <remarks>
/// Formula-for-formula port of the reference:
/// <list type="bullet">
/// <item><c>head.yRot = yRot * deg2rad</c>, <c>head.xRot = xRot * deg2rad</c>.</item>
/// <item><c>right_leg.xRot = cos(walkPos * 0.6662) * 1.4 * walkSpeed * 0.5</c>; the left leg uses the same phase plus π.</item>
/// <item><c>speed = 0.01 * (entityId % 10)</c>; <c>nose.xRot = sin(age * speed) * 4.5°</c>,
/// <c>nose.zRot = cos(age * speed) * 2.5°</c>.</item>
/// <item>Holding an item (<see cref="EntityAnimationFrame.HasMainHandItem"/>, matching the reference
/// <c>isHoldingItem</c>) sets the nose pivot to <c>(0, 1, -1.5)</c> and <c>nose.xRot = -0.9</c>; the z wobble is
/// untouched because the reference only overrides xRot.</item>
/// </list>
/// All inputs the reference reads are present on <see cref="EntityAnimationFrame"/> (age, pitch, yaw, walk position/
/// speed, entity id, main-hand item), so the hot path is fully implemented. <c>isHoldingPotion</c> is not read by
/// <c>setupAnim</c>, so only <c>isHoldingItem</c> is needed.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.witch.WitchModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/witch/WitchModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class WitchAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk", "hold_item"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>
    /// Nose bind pivot Y in the mesher's y-up storage. The reference <c>PartPose.offset(0, -2, 0)</c> is stored as
    /// <c>-pivotY</c>, i.e. <c>+2</c> (see <see cref="VanillaMeshBake.BakeChild"/>).
    /// </summary>
    private const float NoseBindStoredY = 2f;

    /// <summary>Nose bind pivot Z in the mesher's storage (Z is not flipped).</summary>
    private const float NoseBindStoredZ = 0f;

    /// <summary>
    /// Reference <c>nose.setPos(0, 1, -1.5)</c> translated to an additive pose offset. The target pivot is
    /// <c>(0, -1, -1.5)</c> in storage (Y negated from the reference y-down <c>1</c>) and the bind pivot is
    /// <c>(0, 2, 0)</c>, so the mesher-ready offset is <c>(-1 - 2, -1.5 - 0) = (0, -3, -1.5)</c> on Y/Z.
    /// </summary>
    private static readonly Vector3 HoldingNoseOffset = new(0f, -1f - NoseBindStoredY, -1.5f - NoseBindStoredZ);

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyCore(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "walk":
                // Reconstruct the walk phase from explicit seconds; the reference walk phase is walkPos * 0.6662.
                ApplyCore(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
                break;
            case "hold_item":
                ApplyCore(frame with { HasMainHandItem = true, WalkAnimationSpeed = 0f }, pose);
                break;
            default:
                // "idle": head look plus nose wobble, legs at rest.
                ApplyCore(frame with { WalkAnimationSpeed = 0f }, pose);
                break;
        }
    }

    private static void ApplyCore(in EntityAnimationFrame frame, EntityPose pose)
    {
        // Head look: both render-state angles are degrees; the reference stores them on yRot / xRot.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        // Leg swing: half-amplitude 1.4 * speed * 0.5; the left leg is the same cosine one half-cycle later.
        var phase = frame.WalkAnimationPos * 0.6662f;
        var legAmplitude = 1.4f * frame.WalkAnimationSpeed * 0.5f;
        pose.Add("right_leg", new PoseOffset(
            Vector3.Zero,
            new Vector3(MathF.Cos(phase) * legAmplitude, 0f, 0f),
            Vector3.Zero));
        pose.Add("left_leg", new PoseOffset(
            Vector3.Zero,
            new Vector3(MathF.Cos(phase + MathF.PI) * legAmplitude, 0f, 0f),
            Vector3.Zero));

        // Nose wobble keyed off the entity id: faster for larger id remainders, up to 9 * 0.01.
        var speed = 0.01f * (frame.EntityId % 10);
        var nosePitch = MathF.Sin(frame.AgeInTicks * speed) * 4.5f * DegreesToRadians;
        var noseRoll = MathF.Cos(frame.AgeInTicks * speed) * 2.5f * DegreesToRadians;
        var noseOffset = Vector3.Zero;

        if (frame.HasMainHandItem)
        {
            // Reference overrides the absolute nose pivot and pitch; the roll wobble above is kept.
            noseOffset = HoldingNoseOffset;
            nosePitch = -0.9f;
        }

        pose.Add("nose", new PoseOffset(noseOffset, new Vector3(nosePitch, 0f, noseRoll), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
