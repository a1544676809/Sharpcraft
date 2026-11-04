// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>BlazeModel.setupAnim</c>: a purely procedural pose with no keyframe clips. The twelve rods are split
/// into three rings of four; each ring spins its rods around a radius and bobs each rod vertically from the age, and the
/// head looks along the render-state pitch/yaw.
/// </summary>
/// <remarks>
/// <para>
/// The reference writes each rod's <c>x</c>/<c>y</c>/<c>z</c> as an absolute model-space position. Our
/// <see cref="PoseOffset"/> is additive over the bind pose, so every rod is expressed as the delta from its bind pivot.
/// The mesher stores pivots in the y-up convention (X and Y negated vs the reference y-down model space), therefore the
/// vertical delta is negated while the horizontal deltas keep the reference sign - the same convention the other
/// procedural providers use (see <c>GuardianAnimationProvider</c>). At <c>ageInTicks == 0</c> every delta is zero, i.e.
/// the reference animation reproduces its own create-body-layer pose.
/// </para>
/// <para>
/// Ring index mapping: <c>part0..part3</c> = ring 0 (radius 9, base y -2, angle rate -π/10 per tick), <c>part4..part7</c>
/// = ring 1 (radius 7, base y +2, base angle π/4, rate +0.03π per tick), <c>part8..part11</c> = ring 2 (radius 5, base y
/// +11, base angle 0.47123894, rate -0.05π per tick).
/// </para>
/// </remarks>
public sealed class BlazeAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;
    private const float HalfPi = MathF.PI / 2f;

    /// <summary>Reference radius of each ring, indexed 0..2.</summary>
    private static readonly float[] RingRadius = [9f, 7f, 5f];

    /// <summary>Reference base Y of each ring's rods (before the per-rod cosine term).</summary>
    private static readonly float[] RingBaseY = [-2f, 2f, 11f];

    /// <summary>Reference starting angle of ring 0 and ring 1; ring 2 uses <see cref="Ring2StartAngle"/>.</summary>
    private const float Ring1StartAngle = MathF.PI / 4f;

    /// <summary>Reference ring-2 starting angle (0.47123894 rad).</summary>
    private const float Ring2StartAngle = 0.47123894f;

    /// <summary>Precomputed part names so the hot path never concatenates strings; index i is "part{i}".</summary>
    private static readonly string[] RodNames =
    [
        "part0", "part1", "part2", "part3", "part4", "part5",
        "part6", "part7", "part8", "part9", "part10", "part11"
    ];

    private static readonly string[] NominalClips = ["idle"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyCore(frame.AgeInTicks, frame.Pitch, frame.Yaw, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clip: the preview "seconds" is converted back to ticks (20 ticks per second).
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            ApplyCore(seconds * 20f, frame.Pitch, frame.Yaw, pose);
        }
    }

    private static void ApplyCore(float ageInTicks, float pitch, float yaw, EntityPose pose)
    {
        // Head look: reference head.yRot = yaw, head.xRot = pitch.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(pitch * DegreesToRadians, yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        // Ring 0: parts 0..3. Reference starts at angle 0 and adds -0.1π per tick; y = -2 + cos((2i + age)·0.25).
        var animAngle = ageInTicks * MathF.PI * -0.1f;
        var bindAngle = 0f;
        for (var i = 0; i < 4; i++)
        {
            AddRod(0, i, ageInTicks, animAngle, bindAngle, pose);
            animAngle += HalfPi;
            bindAngle += HalfPi;
        }

        // Ring 1: parts 4..7. Reference base angle π/4 plus +0.03π per tick; y = 2 + cos((2i + age)·0.25).
        animAngle = Ring1StartAngle + (ageInTicks * MathF.PI * 0.03f);
        bindAngle = Ring1StartAngle;
        for (var i = 4; i < 8; i++)
        {
            AddRod(1, i, ageInTicks, animAngle, bindAngle, pose);
            animAngle += HalfPi;
            bindAngle += HalfPi;
        }

        // Ring 2: parts 8..11. Reference base angle 0.47123894 plus -0.05π per tick; y = 11 + cos((1.5i + age)·0.5).
        animAngle = Ring2StartAngle + (ageInTicks * MathF.PI * -0.05f);
        bindAngle = Ring2StartAngle;
        for (var i = 8; i < 12; i++)
        {
            AddRod(2, i, ageInTicks, animAngle, bindAngle, pose);
            animAngle += HalfPi;
            bindAngle += HalfPi;
        }
    }

    /// <summary>Adds one rod's additive position delta from its bind pivot, using the reference ring formulas.</summary>
    private static void AddRod(int ring, int index, float ageInTicks, float animAngle, float bindAngle, EntityPose pose)
    {
        var radius = RingRadius[ring];
        var baseY = RingBaseY[ring];

        var bindX = MathF.Cos(bindAngle) * radius;
        var bindZ = MathF.Sin(bindAngle) * radius;
        var animX = MathF.Cos(animAngle) * radius;
        var animZ = MathF.Sin(animAngle) * radius;

        float bindY;
        float animY;
        if (ring < 2)
        {
            // Rings 0 and 1: cos((i·2 + age)·0.25).
            bindY = baseY + MathF.Cos(index * 2f * 0.25f);
            animY = baseY + MathF.Cos((index * 2f + ageInTicks) * 0.25f);
        }
        else
        {
            // Ring 2: cos((i·1.5 + age)·0.5).
            bindY = baseY + MathF.Cos(index * 1.5f * 0.5f);
            animY = baseY + MathF.Cos((index * 1.5f + ageInTicks) * 0.5f);
        }

        // Additive over bind; the mesher stores pivots y-up so the vertical delta is negated.
        pose.Add(RodNames[index], new PoseOffset(
            new Vector3(animX - bindX, -(animY - bindY), animZ - bindZ),
            Vector3.Zero,
            Vector3.Zero));
    }

    /// <summary>Blaze has no stateful clocks; the procedural pose depends only on the frame.</summary>
    private sealed class State : IEntityAnimationState
    {
    }
}
