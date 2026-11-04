// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>StriderModel.setupAnim</c> plus <c>AdultStriderModel.customAnimations</c> (fully procedural, no keyframe
/// data). The body follows the render-state pitch/yaw unless ridden, sways with the walk phase, the two legs swing, and
/// the six bristles ripple with the <c>animateBristle</c> flow and their slow idle sines.
/// </summary>
/// <remarks>
/// <para>
/// The reference writes absolute model rotations/positions. This pose system is additive over the bind pose, so every
/// assigned value is reduced to its delta from the model's bind value:
/// the bristle baselines (±1.2217305/±1.134464/±0.87266463) are already baked into the mesh, so only the oscillation
/// survives; the arm/leg rotations rest at 0. The reference <c>body.y</c> baseline is 2 while the bind is 1 and
/// <c>leftLeg/rightLeg.y</c> baseline is 8 while the bind is 8.
/// </para>
/// <para>
/// Position storage is y-up (the mesher negates the reference y-down pivot), so the reference y deltas are negated when
/// producing the additive offsets: <c>body</c> offset.y = 4·cos(pos·1.5)·speed − 1, and the leg offset.y terms are the
/// negative of the reference sine deltas. Rotations keep the reference signs (x/y/z are added to the part rotations).
/// Part names match <see cref="StriderModelDefinition"/>.
/// </para>
/// </remarks>
public sealed class StriderAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    /// <summary>Reference <c>SPEED = 1.5F</c>, the walk-phase multiplier.</summary>
    private const float PhaseSpeed = 1.5f;

    /// <summary>Body bind y (reference y-down), used to convert the absolute <c>body.y</c> baseline.</summary>
    private const float BodyBindY = 1f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "walk":
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
                break;
            case "idle":
                ApplyPose(frame with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, EntityPose pose)
    {
        var pos = frame.WalkAnimationPos;
        var speed = MathF.Min(frame.WalkAnimationSpeed, 0.25f);

        // Body: look at the render-state pitch/yaw unless ridden; sway around z from the walk phase.
        float bodyXRot = 0f;
        float bodyYRot = 0f;
        if (!frame.IsVehicle)
        {
            bodyXRot = frame.Pitch * (MathF.PI / 180f);
            bodyYRot = frame.Yaw * (MathF.PI / 180f);
        }

        var bodyZRot = 0.1f * MathF.Sin(pos * PhaseSpeed) * 4f * speed;
        // reference body.y = 2 - 4·cos(pos·1.5)·speed; storage is y-up and bind is 1 → offset = 4·cos·speed − 1.
        var bodyYOffset = (4f * MathF.Cos(pos * PhaseSpeed) * speed) - BodyBindY;

        pose.Add("body", new PoseOffset(
            new Vector3(0f, bodyYOffset, 0f),
            new Vector3(bodyXRot, bodyYRot, bodyZRot),
            Vector3.Zero));

        // Legs: half-phase sine for x swing, cosine for z splay, plus the reference leg.y term.
        var legPhase = pos * PhaseSpeed * 0.5f;
        var leftLegXRot = MathF.Sin(legPhase) * 2f * speed;
        var rightLegXRot = MathF.Sin(legPhase + MathF.PI) * 2f * speed;
        var legZScale = (MathF.PI / 18f) * speed;
        var leftLegZRot = legZScale * MathF.Cos(legPhase);
        var rightLegZRot = legZScale * MathF.Cos(legPhase + MathF.PI);

        // reference leftLeg.y = 8 + 4·sin(phase+π)·speed and rightLeg.y = 8 + 4·sin(phase)·speed; storage y-up:
        // offsets are the negated sine delta from the shared 8 bind.
        var legYSwing = 4f * MathF.Sin(legPhase) * speed;
        pose.Add("left_leg", new PoseOffset(
            new Vector3(0f, legYSwing, 0f),
            new Vector3(leftLegXRot, 0f, leftLegZRot),
            Vector3.Zero));
        pose.Add("right_leg", new PoseOffset(
            new Vector3(0f, -legYSwing, 0f),
            new Vector3(rightLegXRot, 0f, rightLegZRot),
            Vector3.Zero));

        // Bristles: baseline zRot is baked into the mesh; only the animateBristle deltas remain. The left and right
        // sets use the same flow multipliers and idle sines, and their mirrored baselines live in the bind pose.
        var flow = MathF.Cos(pos * PhaseSpeed + MathF.PI) * speed;
        var age = frame.AgeInTicks;
        var topZ = (flow * 0.6f) + (0.1f * MathF.Sin(age * 0.4f));
        var middleZ = (flow * 1.2f) + (0.1f * MathF.Sin(age * 0.2f));
        var bottomZ = (flow * 1.3f) + (0.05f * MathF.Sin(age * -0.4f));

        pose.Add("right_top_bristle", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, topZ), Vector3.Zero));
        pose.Add("right_middle_bristle", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, middleZ), Vector3.Zero));
        pose.Add("right_bottom_bristle", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, bottomZ), Vector3.Zero));
        pose.Add("left_top_bristle", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, topZ), Vector3.Zero));
        pose.Add("left_middle_bristle", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, middleZ), Vector3.Zero));
        pose.Add("left_bottom_bristle", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, bottomZ), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
