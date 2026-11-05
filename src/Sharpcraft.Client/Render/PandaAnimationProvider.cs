// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>PandaModel.setupAnim</c> (which first runs <c>QuadrupedModel.setupAnim</c>). Derivable branches: head
/// look + quadruped leg walk, unhappy head shake / front-leg wave, sneeze head tilt, sitting (with eating), lying on the
/// back, and rolling.
/// </summary>
/// <remarks>
/// <para>
/// <c>isUnhappy = UnhappyCounter &gt; 0</c>, <c>sneezeTime = SneezeCounter</c>, <c>isSneezing = SneezeCounter &gt; 0</c>,
/// and <c>isScared = IsWorried &amp;&amp; IsThundering</c> (reference <c>Panda.isScared()</c>). The sit / lie / roll branches
/// use <see cref="EntityAnimationFrame.SitAmount"/>, <see cref="EntityAnimationFrame.LieOnBackAmount"/>,
/// <see cref="EntityAnimationFrame.RollAmount"/> and <see cref="EntityAnimationFrame.IsEating"/>.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.panda.PandaModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/panda/PandaModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class PandaAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;
    private const float QuarterPi = MathF.PI / 4f;

    /// <summary>Reference body bind xRot (<c>PartPose.offsetAndRotation(0, 10, 0, π/2, …)</c>).</summary>
    private const float BodyBindXRot = MathF.PI / 2f;

    /// <summary>Reference <c>animateSitting</c> body target.</summary>
    private const float SitBodyXRot = 1.7407963f;

    /// <summary>Reference roll head target (<c>Mth.rotLerpRad(rollAmount, head.xRot, 2.0561945F)</c>).</summary>
    private const float RollHeadXRot = 2.0561945f;

    private static readonly string[] NominalClips = ["idle", "walk", "unhappy", "sneeze", "sit", "eat", "scared", "lie", "roll"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        ApplyPose(frame, frame.UnhappyCounter > 0, frame.SneezeCounter > 0, frame.SneezeCounter, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var rest = frame with
        {
            AgeInTicks = seconds * 20f,
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 0f,
            UnhappyCounter = 0,
            SneezeCounter = 0,
            SitAmount = 0f,
            IsEating = false,
            LieOnBackAmount = 0f,
            RollAmount = 0f
        };

        switch (clip)
        {
            case "idle":
                ApplyPose(rest, false, false, 0, pose);
                break;
            case "walk":
                ApplyPose(rest with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, false, false, 0, pose);
                break;
            case "unhappy":
                ApplyPose(rest with { UnhappyCounter = 1 }, true, false, 0, pose);
                break;
            case "sneeze":
                var sneezeTime = (int)(seconds * 20f);
                ApplyPose(rest with { SneezeCounter = sneezeTime }, false, sneezeTime > 0, sneezeTime, pose);
                break;
            case "sit":
                ApplyPose(rest with { SitAmount = 1f }, false, false, 0, pose);
                break;
            case "eat":
                ApplyPose(rest with { SitAmount = 1f, IsEating = true }, false, false, 0, pose);
                break;
            case "scared":
                ApplyPose(rest with { SitAmount = 1f, IsWorried = true, IsThundering = true }, false, false, 0, pose);
                break;
            case "lie":
                ApplyPose(rest with { LieOnBackAmount = 1f }, false, false, 0, pose);
                break;
            case "roll":
                ApplyPose(rest with { RollAmount = 1f }, false, false, 0, pose);
                break;
        }
    }

    private static void ApplyPose(
        in EntityAnimationFrame frame,
        bool isUnhappy,
        bool isSneezing,
        int sneezeTime,
        EntityPose pose)
    {
        var ageInTicks = frame.AgeInTicks;

        // Quadruped base: head pitch/yaw and the four legs' walk swing on xRot.
        var headX = frame.Pitch * DegreesToRadians;
        var headY = frame.Yaw * DegreesToRadians;
        var headZ = 0f;

        var phase = frame.WalkAnimationPos * 0.6662f;
        var amplitude = 1.4f * frame.WalkAnimationSpeed;
        var rightHindX = MathF.Cos(phase) * amplitude;
        var leftHindX = MathF.Cos(phase + MathF.PI) * amplitude;
        var rightFrontX = leftHindX;
        var leftFrontX = rightHindX;

        var rightHindZ = 0f;
        var leftHindZ = 0f;
        var rightFrontZ = 0f;
        var leftFrontZ = 0f;

        var bodyXRot = 0f; // additive offset from the bind (zero when not sitting)

        if (isUnhappy)
        {
            var shake = 0.35f * MathF.Sin(0.6f * ageInTicks);
            headY = shake;
            headZ = shake;
            rightFrontX = -0.75f * MathF.Sin(0.3f * ageInTicks);
            leftFrontX = 0.75f * MathF.Sin(0.3f * ageInTicks);
        }

        if (isSneezing)
        {
            if (sneezeTime < 15)
            {
                headX = (-QuarterPi) * sneezeTime / 14f;
            }
            else if (sneezeTime < 20)
            {
                // Reference uses integer division here: (sneezeTime - 15) / 5 == 0 for 15..19.
                var internalSneezePos = (sneezeTime - 15) / 5;
                headX = -QuarterPi + (QuarterPi * internalSneezePos);
            }
        }

        if (frame.SitAmount > 0f)
        {
            // animateSitting: body/head lerp toward the sit pose and the four legs splay their zRot.
            bodyXRot = RotLerpRad(frame.SitAmount, BodyBindXRot, SitBodyXRot) - BodyBindXRot;
            headX = RotLerpRad(frame.SitAmount, headX, QuarterPi * 2f);
            rightFrontZ = -0.27079642f;
            leftFrontZ = 0.27079642f;
            rightHindZ = 0.5707964f;
            leftHindZ = -0.5707964f;

            if (frame.IsEating)
            {
                var eat = 0.2f * MathF.Sin(ageInTicks * 0.6f);
                headX = (QuarterPi * 2f) + eat;
                rightFrontX = -0.4f - eat;
                leftFrontX = -0.4f - eat;
            }

            if (frame.IsWorried && frame.IsThundering)
            {
                // Panda.isScared(): worried main gene + thundering weather overrides the eating pose.
                headX = 2.1707964f;
                rightFrontX = -0.9f;
                leftFrontX = -0.9f;
            }
        }

        if (frame.LieOnBackAmount > 0f)
        {
            rightHindX = -0.6f * MathF.Sin(ageInTicks * 0.15f);
            leftHindX = 0.6f * MathF.Sin(ageInTicks * 0.15f);
            rightFrontX = 0.3f * MathF.Sin(ageInTicks * 0.25f);
            leftFrontX = -0.3f * MathF.Sin(ageInTicks * 0.25f);
            headX = RotLerpRad(frame.LieOnBackAmount, headX, QuarterPi * 2f);
        }

        if (frame.RollAmount > 0f)
        {
            headX = RotLerpRad(frame.RollAmount, headX, RollHeadXRot);
            rightHindX = -0.5f * MathF.Sin(ageInTicks * 0.5f);
            leftHindX = 0.5f * MathF.Sin(ageInTicks * 0.5f);
            rightFrontX = 0.5f * MathF.Sin(ageInTicks * 0.5f);
            leftFrontX = -0.5f * MathF.Sin(ageInTicks * 0.5f);
        }

        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headX, headY, headZ), Vector3.Zero));
        pose.Add("body", new PoseOffset(Vector3.Zero, new Vector3(bodyXRot, 0f, 0f), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHindX, 0f, rightHindZ), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHindX, 0f, leftHindZ), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(rightFrontX, 0f, rightFrontZ), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(leftFrontX, 0f, leftFrontZ), Vector3.Zero));
    }

    /// <summary>Reference <c>Mth.rotLerpRad</c>: shortest-arc interpolation in radians.</summary>
    private static float RotLerpRad(float delta, float from, float to)
    {
        var diff = to - from;
        while (diff < -MathF.PI)
        {
            diff += MathF.PI * 2f;
        }

        while (diff >= MathF.PI)
        {
            diff -= MathF.PI * 2f;
        }

        return from + (delta * diff);
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
