// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>AbstractEquineModel.setupAnim</c>: head pitch (with a speed-scaled bob), four leg phases and a tail
/// offset. Horse, donkey, mule, skeleton horse and zombie horse share this controller.
/// </summary>
public sealed class EquineAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "walk"];

    public EquineAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyEquineWalk(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            var walk = frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f };
            ApplyEquineWalk(walk, pose);
        }
        else
        {
            ApplyEquineWalk(frame with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyEquineWalk(in EntityAnimationFrame frame, EntityPose pose)
    {
        var speed = frame.WalkAnimationSpeed;
        var phase = frame.WalkAnimationPos * 0.6662f;
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        if (speed > 0.2f)
        {
            headPitch += MathF.Cos(frame.WalkAnimationPos * 0.8f) * 0.15f * speed;
        }

        pose.Add("head_parts", new PoseOffset(Vector3.Zero, new Vector3(headPitch, 0f, 0f), Vector3.Zero));
        var legAnim = MathF.Cos(phase + MathF.PI);
        var legX = legAnim * 0.8f * speed;
        pose.Add("left_front_leg", new PoseOffset(Vector3.Zero, new Vector3(legX, 0f, 0f), Vector3.Zero));
        pose.Add("right_front_leg", new PoseOffset(Vector3.Zero, new Vector3(-legX, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(-legAnim * 0.5f * speed, 0f, 0f), Vector3.Zero));
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(legAnim * 0.5f * speed, 0f, 0f), Vector3.Zero));
        pose.Add("tail", new PoseOffset(new Vector3(0f, speed, speed * 2f), new Vector3(speed * 0.75f, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
