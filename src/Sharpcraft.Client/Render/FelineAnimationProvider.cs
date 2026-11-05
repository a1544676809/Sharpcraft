// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>AdultFelineModel.setupAnim</c> shared by the adult Cat and Ocelot (docs/modules/entity-animation.md A5).
/// </summary>
/// <remarks>
/// Our <see cref="CatModelDefinition"/> splits the reference "head" group into four parts that share one pivot, so the
/// head rotation is applied to all four. The reference assigns several part transforms absolutely (body xRot = pi/2,
/// tail1 = 0.9, tail2 = 0); a <see cref="PoseOffset"/> is a delta from the bind pose, so those become
/// <c>delta = assigned - bind</c>. Position offsets follow the mesher's y-up part space (Y negated, Z unchanged; the
/// derivable offsets only use Y/Z). Blocked (recorded, not approximated): cat lieDown/relax (synced IS_LYING /
/// RELAX_STATE_ONE not decoded yet) and sprint tail (shared flags bit 3 not decoded).
/// </remarks>
public sealed class FelineAnimationProvider : IEntityAnimationProvider
{
    private const float Pi = MathF.PI;
    private const float DegToRad = Pi / 180f;

    // Reference bind rotations (ModelPart rest values): body pi/2, tail1 0.9, tail2 and legs 0.
    private const float BodyBindXRot = Pi / 2f;
    private const float Tail1BindXRot = 0.9f;

    private static readonly string[] HeadParts = ["head_main", "nose", "ear1", "ear2"];
    private static readonly string[] NominalClips = ["idle", "walk"];

    private readonly bool _isCat;

    /// <param name="typeId">Cat (21) or Ocelot (92).</param>
    /// <param name="isCat">Whether sitting/lie/relax states apply (Cat only; Ocelot's render state has no sitting).</param>
    public FelineAnimationProvider(int typeId, bool isCat)
    {
        TypeId = typeId;
        _isCat = isCat;
    }

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyFeline(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var sandbox = frame with { IsSitting = false, IsCrouching = false };
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplyFeline(sandbox with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplyFeline(sandbox with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private void ApplyFeline(in EntityAnimationFrame frame, EntityPose pose)
    {
        var ageScale = frame.IsBaby ? 0.5f : 1f;
        var sitting = _isCat && frame.IsSitting;
        var crouching = frame.IsCrouching;

        // Part rotations (delta from bind) and positions (y-up, model pixels).
        var bodyRotX = 0f;
        float leftHind = 0f, rightHind = 0f, leftFront = 0f, rightFront = 0f;
        var tail1RotX = 0f;
        var tail2RotX = 0f;

        var bodyY = 0f;
        var bodyZ = 0f;
        var headY = 0f;
        var headZ = 0f;
        var tail1Y = 0f;
        var tail1Z = 0f;
        var tail2Y = 0f;
        var tail2Z = 0f;
        var frontY = 0f;
        var frontZ = 0f;
        var hindY = 0f;
        var hindZ = 0f;

        if (crouching)
        {
            bodyY += 1f * ageScale;
            headY += 2f * ageScale;
            tail1Y += 1f * ageScale;
            tail2Y += -4f * ageScale;
            tail2Z += 2f * ageScale;
            tail1RotX = (Pi / 2f) - Tail1BindXRot; // absolute pi/2 vs bind 0.9
            tail2RotX = Pi / 2f;                    // absolute pi/2 vs bind 0
        }

        // Head look (absolute pitch + head-relative yaw).
        var headPitch = frame.Pitch * DegToRad;
        var headYaw = WrapDegrees(frame.HeadYaw - frame.Yaw) * DegToRad;

        if (!sitting)
        {
            // body xRot = pi/2 (== bind), so no delta.
            var animationPos = frame.WalkAnimationPos;
            var animationSpeed = frame.WalkAnimationSpeed;
            leftHind = MathF.Cos(animationPos * 0.6662f) * animationSpeed;
            rightHind = MathF.Cos((animationPos * 0.6662f) + Pi) * animationSpeed;
            leftFront = MathF.Cos((animationPos * 0.6662f) + Pi) * animationSpeed;
            rightFront = MathF.Cos(animationPos * 0.6662f) * animationSpeed;
            tail2RotX = crouching
                ? 1.7278761f + (0.47123894f * MathF.Cos(animationPos) * animationSpeed)
                : 1.7278761f + ((Pi / 4f) * MathF.Cos(animationPos) * animationSpeed);
        }

        if (sitting)
        {
            bodyRotX = (Pi / 4f) - BodyBindXRot; // absolute pi/4 vs bind pi/2
            bodyY += -4f * ageScale;
            bodyZ += 5f * ageScale;
            headY += -3.3f * ageScale;
            headZ += 1f * ageScale;
            tail1Y += 8f * ageScale;
            tail1Z += -2f * ageScale;
            tail2Y += 2f * ageScale;
            tail2Z += -0.8f * ageScale;
            tail1RotX = 1.7278761f - Tail1BindXRot;
            tail2RotX = 2.670354f;

            leftFront = -Pi / 20f;
            rightFront = -Pi / 20f;
            frontY += 2f * ageScale;
            frontZ -= 2f * ageScale;

            leftHind = -Pi / 2f;
            rightHind = -Pi / 2f;
            hindY += 3f * ageScale;
            hindZ -= 4f * ageScale;
        }

        foreach (var part in HeadParts)
        {
            pose.Add(part, new PoseOffset(new Vector3(0f, -headY, headZ), new Vector3(headPitch, headYaw, 0f), Vector3.Zero));
        }

        pose.Add("body", new PoseOffset(new Vector3(0f, -bodyY, bodyZ), new Vector3(bodyRotX, 0f, 0f), Vector3.Zero));
        pose.Add("tail1", new PoseOffset(new Vector3(0f, -tail1Y, tail1Z), new Vector3(tail1RotX, 0f, 0f), Vector3.Zero));
        pose.Add("tail2", new PoseOffset(new Vector3(0f, -tail2Y, tail2Z), new Vector3(tail2RotX, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(new Vector3(0f, -hindY, hindZ), new Vector3(leftHind, 0f, 0f), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(new Vector3(0f, -hindY, hindZ), new Vector3(rightHind, 0f, 0f), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(new Vector3(0f, -frontY, frontZ), new Vector3(leftFront, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(new Vector3(0f, -frontY, frontZ), new Vector3(rightFront, 0f, 0f), Vector3.Zero));
    }

    private static float WrapDegrees(float degrees)
    {
        var value = degrees % 360f;
        if (value >= 180f)
        {
            value -= 360f;
        }

        if (value < -180f)
        {
            value += 360f;
        }

        return value;
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
