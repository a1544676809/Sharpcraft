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
/// <c>delta = assigned - bind</c>. Position offsets follow the mesher's y-up part space (Y negated, Z unchanged).
/// Sprint, cat lieDown and relax are driven by decoded frame fields (shared flag bit 3, synced IS_LYING/RELAX_STATE_ONE).
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
        var sandbox = frame with { IsSitting = false, IsCrouching = false, IsSprinting = false, LieDownAmount = 0f, LieDownAmountTail = 0f, RelaxStateOneAmount = 0f };
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
        var sprinting = frame.IsSprinting;

        // Part rotations (delta from bind) and positions (vanilla y-down model pixels; Y is negated at emit).
        var bodyRotX = 0f;
        float leftHind = 0f, rightHind = 0f, leftFront = 0f, rightFront = 0f;
        var rightFrontZRot = 0f;
        var rightHindZRot = 0f;
        var tail1RotX = 0f;
        var tail2RotX = 0f;

        float bodyY = 0f, bodyZ = 0f;
        float headY = 0f, headZ = 0f;
        float tail1Y = 0f, tail1Z = 0f, tail2Y = 0f, tail2Z = 0f;
        float lfX = 0f, lfY = 0f, lfZ = 0f, rfX = 0f, rfY = 0f, rfZ = 0f;
        float lhX = 0f, lhY = 0f, lhZ = 0f, rhX = 0f, rhY = 0f, rhZ = 0f;

        var headPitch = frame.Pitch * DegToRad;
        var headYaw = WrapDegrees(frame.HeadYaw - frame.Yaw) * DegToRad;
        var headZRot = 0f;

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
        else if (sprinting)
        {
            tail2Y += 15f - 20f; // tail2.y = tail1.y (bind 15) - tail2 bind 20
            tail2Z += 2f * ageScale;
            tail1RotX = (Pi / 2f) - Tail1BindXRot;
            tail2RotX = Pi / 2f;
        }

        if (!sitting)
        {
            // body xRot = pi/2 (== bind), so no delta.
            var animationPos = frame.WalkAnimationPos;
            var animationSpeed = frame.WalkAnimationSpeed;
            if (sprinting)
            {
                leftHind = MathF.Cos(animationPos * 0.6662f) * animationSpeed;
                rightHind = MathF.Cos((animationPos * 0.6662f) + 0.3f) * animationSpeed;
                leftFront = MathF.Cos((animationPos * 0.6662f) + Pi + 0.3f) * animationSpeed;
                rightFront = MathF.Cos((animationPos * 0.6662f) + Pi) * animationSpeed;
                tail2RotX = 1.7278761f + ((Pi / 10f) * MathF.Cos(animationPos) * animationSpeed);
            }
            else
            {
                leftHind = MathF.Cos(animationPos * 0.6662f) * animationSpeed;
                rightHind = MathF.Cos((animationPos * 0.6662f) + Pi) * animationSpeed;
                leftFront = MathF.Cos((animationPos * 0.6662f) + Pi) * animationSpeed;
                rightFront = MathF.Cos(animationPos * 0.6662f) * animationSpeed;
                tail2RotX = crouching
                    ? 1.7278761f + (0.47123894f * MathF.Cos(animationPos) * animationSpeed)
                    : 1.7278761f + ((Pi / 4f) * MathF.Cos(animationPos) * animationSpeed);
            }
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
            lfY = rfY = 2f * ageScale;
            lfZ = rfZ = -2f * ageScale;

            leftHind = -Pi / 2f;
            rightHind = -Pi / 2f;
            lhY = rhY = 3f * ageScale;
            lhZ = rhZ = -4f * ageScale;
        }

        if (frame.LieDownAmount > 0f)
        {
            var amount = frame.LieDownAmount;
            headZRot = Lerp(amount, headZRot, -1.2707963f);
            headYaw = Lerp(amount, headYaw, 1.2707963f);
            leftFront = -1.2707963f;
            rightFront = -0.47079635f;
            rightFrontZRot = -0.2f;
            rfX += ageScale;
            leftHind = -0.4f;
            rightHind = 0.5f;
            rightHindZRot = -0.5f;
            rhX += 0.8f * ageScale;
            rhY += 2f * ageScale;
            tail1RotX = Lerp(frame.LieDownAmountTail, tail1RotX, 0.8f);
            tail2RotX = Lerp(frame.LieDownAmountTail, tail2RotX, -0.4f);
        }

        if (frame.RelaxStateOneAmount > 0f)
        {
            headPitch = Lerp(frame.RelaxStateOneAmount, headPitch, -0.58177644f);
        }

        foreach (var part in HeadParts)
        {
            pose.Add(part, new PoseOffset(new Vector3(0f, -headY, headZ), new Vector3(headPitch, headYaw, headZRot), Vector3.Zero));
        }

        pose.Add("body", new PoseOffset(new Vector3(0f, -bodyY, bodyZ), new Vector3(bodyRotX, 0f, 0f), Vector3.Zero));
        pose.Add("tail1", new PoseOffset(new Vector3(0f, -tail1Y, tail1Z), new Vector3(tail1RotX, 0f, 0f), Vector3.Zero));
        pose.Add("tail2", new PoseOffset(new Vector3(0f, -tail2Y, tail2Z), new Vector3(tail2RotX, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(new Vector3(-lhX, -lhY, lhZ), new Vector3(leftHind, 0f, 0f), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(new Vector3(-rhX, -rhY, rhZ), new Vector3(rightHind, 0f, rightHindZRot), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(new Vector3(-lfX, -lfY, lfZ), new Vector3(leftFront, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(new Vector3(-rfX, -rfY, rfZ), new Vector3(rightFront, 0f, rightFrontZRot), Vector3.Zero));
    }

    private static float Lerp(float alpha, float from, float to) => from + (alpha * (to - from));

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
