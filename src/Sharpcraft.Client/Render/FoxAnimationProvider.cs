// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>AdultFoxModel.setupAnim</c> (FoxModel + AdultFoxModel overrides): a fully procedural pose selected by
/// the crouching / sleeping / sitting / pouncing / faceplanted flags and driven by the walk phase, head look, age and a
/// local faceplant leg-motion counter.
/// </summary>
/// <remarks>
/// <para>
/// Ordering mirrors the reference: the walking pose (leg swing) is written first, then the exclusive crouch / sleep /
/// sit branch, then the pounce branch, then the head look, then the sleep head override, then the faceplant leg swing.
/// When a later branch assigns a leg's absolute xRot (sit, faceplant) the walking swing is skipped rather than summed,
/// because the reference assignments would otherwise double-count atop an additive pose.
/// </para>
/// <para>
/// Position storage is y-up (the mesher negates the reference y-down pivot), so the reference <c>.y +=/-=</c> offsets are
/// emitted with the y sign flipped; x and z keep the reference signs. Rotations keep the reference signs, like
/// <see cref="ParrotAnimationProvider"/>; absolute rotation assignments are reduced to their delta from the reference
/// bind value.
/// </para>
/// <para>
/// Blocked (not on <see cref="EntityAnimationFrame"/>, deliberately not faked):
/// <list type="bullet">
/// <item><c>headRollAngle</c> — <c>setWalkingPose</c>'s <c>head.zRot</c> roll cannot be written;</item>
/// <item><c>crouchAmount</c> / <c>ageScale</c> — the crouch <c>head.y</c> offset and AdultFox <c>body.y</c> offset, and
/// the pounce <c>body.y</c>/<c>head.y</c> crouch offsets, cannot be written;</item>
/// <item>leg visibility — walking forces legs visible and sleeping hides them, but there is no pose visibility channel.</item>
/// </list>
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fox.FoxModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fox/FoxModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class FoxAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk", "crouch", "sleep", "sit", "pounce", "faceplant"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference crouch body pitch bump (<c>body.xRot += 0.10471976</c>).</summary>
    private const float CrouchBodyXRot = 0.10471976f;

    /// <summary>Reference <c>AdultFoxModel</c> body bind pitch used to reduce the sitting absolute assignment.</summary>
    private const float RefBodyBindXRot = MathF.PI / 2f;

    /// <summary>Reference <c>AdultFoxModel</c> tail bind pitch used to reduce the sleeping/sitting absolute assignments.</summary>
    private const float RefTailBindXRot = -0.05235988f;

    /// <summary>Faceplant leg-motion advance per tick (reference <c>legMotionPos += 0.67F</c>).</summary>
    private const float FaceplantLegMotionStep = 0.67f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var local = state as State;
        if (local is not null)
        {
            // The tracker only advances the counter while the fox is faceplanted; remember the current flag for OnTick.
            local.Faceplanted = frame.IsFaceplanted;
        }

        ApplyPose(frame, local?.LegMotionPos ?? 0f, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var neutral = frame with
        {
            IsCrouching = false,
            IsSleeping = false,
            IsSitting = false,
            IsPouncing = false,
            IsFaceplanted = false,
        };

        switch (clip)
        {
            case "idle":
                ApplyPose(neutral with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, 0f, pose);
                break;
            case "walk":
                ApplyPose(neutral with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, 0f, pose);
                break;
            case "crouch":
                ApplyPose(neutral with { AgeInTicks = seconds * 20f, WalkAnimationSpeed = 0f, IsCrouching = true }, 0f, pose);
                break;
            case "sleep":
                ApplyPose(neutral with { AgeInTicks = seconds * 20f, WalkAnimationSpeed = 0f, IsSleeping = true }, 0f, pose);
                break;
            case "sit":
                ApplyPose(neutral with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f, IsSitting = true }, 0f, pose);
                break;
            case "pounce":
                ApplyPose(neutral with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f, IsPouncing = true }, 0f, pose);
                break;
            case "faceplant":
                ApplyPose(
                    neutral with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f, IsFaceplanted = true },
                    seconds * 20f * FaceplantLegMotionStep,
                    pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, float legMotionPos, EntityPose pose)
    {
        var crouching = frame.IsCrouching;
        var sleeping = frame.IsSleeping;
        var sitting = !crouching && !sleeping && frame.IsSitting;

        // Walking pose (AdultFoxModel): four-leg cosine swing. Skipped when a later branch overwrites the leg xRot.
        if (!sitting && !frame.IsFaceplanted)
        {
            var phase = frame.WalkAnimationPos * 0.6662f;
            var amplitude = 1.4f * frame.WalkAnimationSpeed;
            var hind = MathF.Cos(phase) * amplitude;
            var hindOpposite = MathF.Cos(phase + MathF.PI) * amplitude;
            pose.Add("right_hind_leg", Rotation(hind, 0f, 0f));
            pose.Add("left_hind_leg", Rotation(hindOpposite, 0f, 0f));
            pose.Add("right_front_leg", Rotation(hindOpposite, 0f, 0f));
            pose.Add("left_front_leg", Rotation(hind, 0f, 0f));
        }

        if (crouching)
        {
            // Body pitch bump plus the wiggle; head.y (crouchAmount * ageScale) and body.y (crouchAmount) are blocked.
            var wiggle = MathF.Cos(frame.AgeInTicks) * 0.05f;
            pose.Add("body", new PoseOffset(Vector3.Zero, new Vector3(CrouchBodyXRot, wiggle, 0f), Vector3.Zero));
            pose.Add("right_hind_leg", Rotation(0f, 0f, wiggle));
            pose.Add("left_hind_leg", Rotation(0f, 0f, wiggle));
            pose.Add("right_front_leg", Rotation(0f, 0f, wiggle / 2f));
            pose.Add("left_front_leg", Rotation(0f, 0f, wiggle / 2f));
        }
        else if (sleeping)
        {
            // AdultFox sleeping pose. Legs are made invisible in the reference (no visibility channel here).
            pose.Add("body", new PoseOffset(Position(0f, 5f, 0f), new Vector3(0f, 0f, -MathF.PI / 2f), Vector3.Zero));
            pose.Add("tail", Rotation((-5f * MathF.PI / 6f) - RefTailBindXRot, 0f, 0f));
            pose.Add("head", new PoseOffset(Position(2f, 2.99f, 0f), Vector3.Zero, Vector3.Zero));
            // The head rotation is set by the sleeping override below.
        }
        else if (sitting)
        {
            // AdultFox sitting pose (absolute assignments reduced to deltas from the reference bind values).
            pose.Add("body", new PoseOffset(Position(0f, -7f, 3f), new Vector3((MathF.PI / 6f) - RefBodyBindXRot, 0f, 0f), Vector3.Zero));
            pose.Add("tail", new PoseOffset(Position(0f, 0f, -1f), new Vector3((MathF.PI / 4f) - RefTailBindXRot, 0f, 0f), Vector3.Zero));
            pose.Add("head", new PoseOffset(Position(0f, -6.5f, 2.75f), Vector3.Zero, Vector3.Zero));
            pose.Add("right_front_leg", Rotation(-MathF.PI / 12f, 0f, 0f));
            pose.Add("left_front_leg", Rotation(-MathF.PI / 12f, 0f, 0f));
            pose.Add("right_hind_leg", new PoseOffset(Position(0f, 4f, -0.25f), new Vector3(-5f * MathF.PI / 12f, 0f, 0f), Vector3.Zero));
            pose.Add("left_hind_leg", new PoseOffset(Position(0f, 4f, -0.25f), new Vector3(-5f * MathF.PI / 12f, 0f, 0f), Vector3.Zero));
        }

        // Pouncing pose: the base is empty and AdultFox's body.y/head.y crouch offsets need crouchAmount (blocked).

        // Head look, skipped for sleeping / faceplanted / crouching (sitting still looks around, exactly as the reference).
        if (!sleeping && !frame.IsFaceplanted && !crouching)
        {
            pose.Add("head", new PoseOffset(
                Vector3.Zero,
                new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
                Vector3.Zero));
        }

        if (sleeping)
        {
            // Final sleeping override: head xRot 0, yRot -2π/3, zRot a slow cosine wobble driven by age.
            pose.Add("head", new PoseOffset(
                Vector3.Zero,
                new Vector3(0f, -2f * MathF.PI / 3f, MathF.Cos(frame.AgeInTicks * 0.027f) / 22f),
                Vector3.Zero));
        }

        if (frame.IsFaceplanted)
        {
            // Faceplant leg swing from the local leg-motion counter; overwrites the walking swing.
            var phase = legMotionPos * 0.4662f;
            var hind = MathF.Cos(phase) * 0.1f;
            var hindOpposite = MathF.Cos(phase + MathF.PI) * 0.1f;
            pose.Add("right_hind_leg", Rotation(hind, 0f, 0f));
            pose.Add("left_hind_leg", Rotation(hindOpposite, 0f, 0f));
            pose.Add("right_front_leg", Rotation(hindOpposite, 0f, 0f));
            pose.Add("left_front_leg", Rotation(hind, 0f, 0f));
        }
    }

    /// <summary>Rotation delta with the reference signs (rotations are stored raw; x/y bind negations live in the mesh).</summary>
    private static PoseOffset Rotation(float x, float y, float z) =>
        new(Vector3.Zero, new Vector3(x, y, z), Vector3.Zero);

    /// <summary>Reference y-down position delta mapped to the y-up pose storage (y negated, x/z unchanged).</summary>
    private static Vector3 Position(float x, float y, float z) => new(x, -y, z);

    private sealed class State : IEntityAnimationState, IEntityTickAnimationState
    {
        /// <summary>Reference <c>legMotionPos</c>: starts at 0 and advances while the fox is faceplanted.</summary>
        public float LegMotionPos;

        /// <summary>Latest faceplanted flag, refreshed by <see cref="Apply"/>; gates the per-tick advance.</summary>
        public bool Faceplanted;

        public void OnTick(in EntityAnimationTickContext context)
        {
            if (Faceplanted)
            {
                LegMotionPos += FaceplantLegMotionStep;
            }
        }
    }
}
