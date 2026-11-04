// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>PandaModel.setupAnim</c> (which first runs <c>QuadrupedModel.setupAnim</c>). Derivable branches: head
/// look + quadruped leg walk, the unhappy head shake / front-leg wave, and the sneeze head tilt.
/// </summary>
/// <remarks>
/// <para>
/// <c>isUnhappy = UnhappyCounter &gt; 0</c>, <c>sneezeTime = SneezeCounter</c>. <c>isSneezing</c> is derived as
/// <c>SneezeCounter &gt; 0</c> (the exact reference flag is the Panda flags bit 2, which is not on the frame; the counter
/// is 1..20 while sneezing).
/// </para>
/// <para>
/// <b>Blocked branches</b> (their inputs are not on <see cref="EntityAnimationFrame"/>): sitting/eating/scared
/// (<c>sitAmount</c>, <c>isEating</c>, <c>isScared</c>), lying on back (<c>lieOnBackAmount</c>) and rolling
/// (<c>rollAmount</c>). The <c>sitAmount == 0</c> reset of the legs' zRot is a no-op against the zero bind and is
/// represented by simply not touching zRot.
/// </para>
/// </remarks>
public sealed class PandaAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;
    private const float QuarterPi = MathF.PI / 4f;

    private static readonly string[] NominalClips = ["idle", "walk", "unhappy", "sneeze"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        ApplyPose(
            frame.AgeInTicks,
            frame.Pitch,
            frame.Yaw,
            frame.WalkAnimationPos,
            frame.WalkAnimationSpeed,
            frame.UnhappyCounter > 0,
            frame.SneezeCounter > 0,
            frame.SneezeCounter,
            pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "idle":
                ApplyPose(seconds * 20f, frame.Pitch, frame.Yaw, 0f, 0f, false, false, 0, pose);
                break;
            case "walk":
                ApplyPose(seconds * 20f, frame.Pitch, frame.Yaw, seconds * 10f, 1f, false, false, 0, pose);
                break;
            case "unhappy":
                ApplyPose(seconds * 20f, frame.Pitch, frame.Yaw, 0f, 0f, true, false, 0, pose);
                break;
            case "sneeze":
                var sneezeTime = (int)(seconds * 20f);
                ApplyPose(seconds * 20f, frame.Pitch, frame.Yaw, 0f, 0f, false, sneezeTime > 0, sneezeTime, pose);
                break;
        }
    }

    private static void ApplyPose(
        float ageInTicks,
        float pitch,
        float yaw,
        float walkPos,
        float walkSpeed,
        bool isUnhappy,
        bool isSneezing,
        int sneezeTime,
        EntityPose pose)
    {
        // Quadruped base: head pitch/yaw and the four legs' walk swing on xRot.
        var headX = pitch * DegreesToRadians;
        var headY = yaw * DegreesToRadians;
        var headZ = 0f;

        var phase = walkPos * 0.6662f;
        var amplitude = 1.4f * walkSpeed;
        var rightHindX = MathF.Cos(phase) * amplitude;
        var leftHindX = MathF.Cos(phase + MathF.PI) * amplitude;
        var rightFrontX = leftHindX;
        var leftFrontX = rightHindX;

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

        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(headX, headY, headZ), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHindX, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHindX, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(rightFrontX, 0f, 0f), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(leftFrontX, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
