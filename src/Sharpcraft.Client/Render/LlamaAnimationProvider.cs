// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>LlamaModel.setupAnim</c>: fully procedural. The head looks along the render-state pitch/yaw and the four
/// legs swing in a diagonal walk pattern. The Trader Llama (138) reuses the same geometry, so one provider serves both.
/// </summary>
/// <remarks>
/// The port split the reference's single <c>head</c> group (head cube + neck + two ears at one pivot) into four model
/// parts (<c>head</c>, <c>neck</c>, <c>left_ear</c>, <c>right_ear</c>) that share the same pivot, so the reference head
/// pitch/yaw is applied to all four to rotate the group together. Legs: <c>right_hind</c> = <c>cos(pos·0.6662)·1.4·speed</c>,
/// <c>left_hind</c> = same with +π, <c>right_front</c> = same with +π, <c>left_front</c> = same without. Bind rotations
/// are 0, so these absolute values are the additive offsets. Chest visibility is handled by the mesher
/// (<c>HasChest</c>) and is not animated here. Part names match <see cref="LlamaModelDefinition"/>.
/// </remarks>
public sealed class LlamaAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    /// <summary>The four parts that make up the reference head group (same pivot).</summary>
    private static readonly string[] HeadGroup = ["head", "neck", "left_ear", "right_ear"];

    private const float DegreesToRadians = MathF.PI / 180f;
    private const float LegAmplitude = 1.4f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyPose(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplyPose(frame with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, EntityPose pose)
    {
        var headRotation = new Vector3(
            frame.Pitch * DegreesToRadians,
            frame.Yaw * DegreesToRadians,
            0f);
        for (var i = 0; i < HeadGroup.Length; i++)
        {
            pose.Add(HeadGroup[i], new PoseOffset(Vector3.Zero, headRotation, Vector3.Zero));
        }

        var phase = frame.WalkAnimationPos * 0.6662f;
        var amplitude = LegAmplitude * frame.WalkAnimationSpeed;
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * amplitude, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * amplitude, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase + MathF.PI) * amplitude, 0f, 0f), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(phase) * amplitude, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
