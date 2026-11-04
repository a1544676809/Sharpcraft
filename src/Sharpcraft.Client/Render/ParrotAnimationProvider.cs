// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>ParrotModel.setupAnim</c> (with <c>prepare</c> and <c>getPose</c>): fully procedural, no baked clips.
/// The pose is selectable from the generic frame — <see cref="EntityAnimationFrame.IsPassenger"/> → ON_SHOULDER,
/// otherwise <see cref="EntityAnimationFrame.IsOnGround"/> false → FLYING, else STANDING — and each pose drives the head
/// look, the wing flap/roll and the body bob from <see cref="EntityAnimationFrame.Flap"/>/<see cref="EntityAnimationFrame.FlapSpeed"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>flapAngle = (sin(flap) + 1) * flapSpeed</c> (computed client-side, already on the frame). For every selectable
/// pose the reference adds <c>bobbingBody = flapAngle * 0.3</c> to the y of the head, tail, body, both wings and both
/// legs, sets <c>left_wing.zRot = -0.0873 - flapAngle</c> / <c>right_wing.zRot = 0.0873 + flapAngle</c>, and swings the
/// tail xRot by <c>cos(walkPos * 0.6662) * 0.3 * walkSpeed</c>. FLYING additionally raises both legs by
/// <c>2π/9</c> (prepare) and STANDING adds the <c>cos * 1.4 * walkSpeed</c> leg walk swing; ON_SHOULDER adds neither.
/// </para>
/// <para>
/// Position storage is y-up (the mesher negates the reference y-down pivot), so the reference <c>.y +=</c> bobbing is
/// emitted as a negative additive offset; rotations keep the reference signs, exactly like
/// <see cref="StriderAnimationProvider"/>. The head rotation is the render-state pitch/yaw in radians.
/// </para>
/// <para>
/// Blocked pose: PARTY cannot be selected because the jukebox party state is not carried by the frame. SITTING is
/// selected from the synced TamableAnimal sitting flag. PARTY is intentionally not faked.
/// </para>
/// </remarks>
public sealed class ParrotAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["flying", "standing", "on_shoulder", "sitting"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference prepare() FLYING leg lift: <c>2π / 9</c>.</summary>
    private const float FlyingLegLift = 2f * MathF.PI / 9f;

    /// <summary>Baseline wing roll when flapAngle is zero (<c>0.0873 = 5°</c>), left negative / right positive.</summary>
    private const float WingRollBase = 0.0873f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(SelectPose(frame), frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "flying":
                ApplyPose(ParrotPose.Flying, frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
                break;
            case "standing":
                ApplyPose(ParrotPose.Standing, frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
                break;
            case "on_shoulder":
                ApplyPose(ParrotPose.OnShoulder, frame with { WalkAnimationSpeed = 0f }, pose);
                break;
            case "sitting":
                ApplyPose(ParrotPose.Sitting, frame with { WalkAnimationSpeed = 0f }, pose);
                break;
        }
    }

    /// <summary>Reference <c>ParrotModel.getPose</c> restricted to the poses the generic frame can select.</summary>
    private static ParrotPose SelectPose(in EntityAnimationFrame frame)
    {
        if (frame.IsPassenger)
        {
            return ParrotPose.OnShoulder;
        }

        if (frame.IsSitting)
        {
            return ParrotPose.Sitting;
        }

        return frame.IsOnGround ? ParrotPose.Standing : ParrotPose.Flying;
    }

    private static void ApplyPose(ParrotPose poseKind, in EntityAnimationFrame frame, EntityPose pose)
    {
        if (poseKind == ParrotPose.Sitting)
        {
            // Reference prepare(SITTING): head/tail/body/wings y += 1.9, tail xRot += π/6, wing zRot ±0.0873; then the
            // switch breaks, so no bob/flap/leg swing is added. Storage is y-up, so y is negated.
            const float sitOffset = -1.9f;
            pose.Add("head", new PoseOffset(new Vector3(0f, sitOffset, 0f), new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f), Vector3.Zero));
            pose.Add("tail", new PoseOffset(new Vector3(0f, sitOffset, 0f), new Vector3(MathF.PI / 6f, 0f, 0f), Vector3.Zero));
            pose.Add("body", new PoseOffset(new Vector3(0f, sitOffset, 0f), Vector3.Zero, Vector3.Zero));
            pose.Add("left_wing", new PoseOffset(new Vector3(0f, sitOffset, 0f), new Vector3(0f, 0f, -WingRollBase), Vector3.Zero));
            pose.Add("right_wing", new PoseOffset(new Vector3(0f, sitOffset, 0f), new Vector3(0f, 0f, WingRollBase), Vector3.Zero));
            return;
        }

        var flapAngle = (MathF.Sin(frame.Flap) + 1f) * frame.FlapSpeed;
        // Reference adds bobbingBody to each part's y (y-down); storage is y-up, so negate.
        var bobY = -(flapAngle * 0.3f);
        var phase = frame.WalkAnimationPos * 0.6662f;

        // Head look: absolute pitch/yaw in radians over a zero bind rotation.
        pose.Add("head", new PoseOffset(
            new Vector3(0f, bobY, 0f),
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        // Tail: walk-phase sweep plus the shared body bob.
        pose.Add("tail", new PoseOffset(
            new Vector3(0f, bobY, 0f),
            new Vector3(MathF.Cos(phase) * 0.3f * frame.WalkAnimationSpeed, 0f, 0f),
            Vector3.Zero));

        // Body bob.
        pose.Add("body", new PoseOffset(new Vector3(0f, bobY, 0f), Vector3.Zero, Vector3.Zero));

        // Wings: flap roll (left negative, right positive) plus the shared bob.
        pose.Add("left_wing", new PoseOffset(
            new Vector3(0f, bobY, 0f),
            new Vector3(0f, 0f, -WingRollBase - flapAngle),
            Vector3.Zero));
        pose.Add("right_wing", new PoseOffset(
            new Vector3(0f, bobY, 0f),
            new Vector3(0f, 0f, WingRollBase + flapAngle),
            Vector3.Zero));

        // Legs: pose-specific xRot offset, plus the shared bob on y.
        var (leftLegXRot, rightLegXRot) = poseKind switch
        {
            ParrotPose.Flying => (FlyingLegLift, FlyingLegLift),
            ParrotPose.Standing => (
                MathF.Cos(phase) * 1.4f * frame.WalkAnimationSpeed,
                MathF.Cos(phase + MathF.PI) * 1.4f * frame.WalkAnimationSpeed),
            _ => (0f, 0f)
        };
        pose.Add("left_leg", new PoseOffset(new Vector3(0f, bobY, 0f), new Vector3(leftLegXRot, 0f, 0f), Vector3.Zero));
        pose.Add("right_leg", new PoseOffset(new Vector3(0f, bobY, 0f), new Vector3(rightLegXRot, 0f, 0f), Vector3.Zero));
    }

    /// <summary>The four poses selectable from <see cref="EntityAnimationFrame"/> (PARTY still not selectable).</summary>
    private enum ParrotPose
    {
        Flying,
        Standing,
        OnShoulder,
        Sitting
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
