// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>TurtleModel.setupAnim</c> (which first runs <c>QuadrupedModel.setupAnim</c>). The head looks along the
/// pitch; the quadruped base swings all four legs on xRot from the walk phase; then the turtle picks a land or swim pose.
/// </summary>
/// <remarks>
/// <para>
/// On land: front/hind legs also get <c>yRot</c> flipper splay
/// (<c>right_front = −cos(pos·5)·8·speed</c>, <c>left_front = +…</c>, hind with a 3× amplitude) while keeping the
/// quadruped xRot walk swing from the base.
/// </para>
/// <para>
/// In water: hind legs' xRot is <b>overwritten</b> to <c>±sin</c>-style swing
/// (<c>swing = cos(pos·0.6662·0.6)·0.5·speed</c>), front legs keep the quadruped xRot and add
/// <c>zRot = ∓swing</c>.
/// </para>
/// <para>
/// <b>Laying-egg branch blocked.</b> The reference scales the land swing by <c>layEgg = isLayingEgg ? 4 : 1</c> and
/// <c>layEggAmplitude = isLayingEgg ? 2 : 1</c>, but <c>isLayingEgg</c> is an AI/runtime state not present on
/// <see cref="EntityAnimationFrame"/>. Only the non-laying land branch (<c>layEgg = 1</c>, amplitude 1) is ported.
/// </para>
/// </remarks>
public sealed class TurtleAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    private static readonly string[] NominalClips = ["walk_land", "swim"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var isOnLand = frame.IsOnGround && !frame.IsInWater;
        ApplyPose(frame, isOnLand, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "walk_land":
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, isOnLand: true, pose);
                break;
            case "swim":
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, isOnLand: false, pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, bool isOnLand, EntityPose pose)
    {
        // Quadruped base: head pitch (head yaw is not animated by this codebase's quadruped controller).
        pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(frame.Pitch * DegreesToRadians, 0f, 0f), Vector3.Zero));

        var pos = frame.WalkAnimationPos;
        var speed = frame.WalkAnimationSpeed;

        // Quadruped walk swing on the leg xRot.
        var phase = pos * 0.6662f;
        var amplitude = 1.4f * speed;
        var rightHindX = MathF.Cos(phase) * amplitude;
        var leftHindX = MathF.Cos(phase + MathF.PI) * amplitude;
        var rightFrontX = leftHindX;
        var leftFrontX = rightHindX;

        if (isOnLand)
        {
            // Non-laying land branch: layEgg = 1 → frontSwing == hindSwing == cos(pos·5).
            var swing = MathF.Cos(pos * 5f);
            var front = swing * 8f * speed;
            var hind = swing * 3f * speed;

            pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(rightFrontX, -front, 0f), Vector3.Zero));
            pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(leftFrontX, front, 0f), Vector3.Zero));
            pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHindX, -hind, 0f), Vector3.Zero));
            pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHindX, hind, 0f), Vector3.Zero));
        }
        else
        {
            // Swim branch: hind xRot is replaced by the swim swing; front keeps the quadruped xRot and adds zRot.
            var swing = MathF.Cos(pos * 0.6662f * 0.6f) * 0.5f * speed;

            pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(swing, 0f, 0f), Vector3.Zero));
            pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(-swing, 0f, 0f), Vector3.Zero));
            pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(rightFrontX, 0f, -swing), Vector3.Zero));
            pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(leftFrontX, 0f, swing), Vector3.Zero));
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
