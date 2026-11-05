// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>EndermanModel.setupAnim</c>: the plain <c>HumanoidModel.setupAnim</c> chain (no zombie/skeleton arm
/// override) followed by the Enderman long-limb damping (arms/legs xRot halved and clamped to [-0.4, 0.4]).
/// </summary>
/// <remarks>
/// Blocked (not approximated): the carried-block arm pose (<c>state.carriedBlock</c> — the carried block state is not
/// decoded yet) and the creepy head drop (<c>isCreepy</c> moves head and hat in opposite directions; our hat is an
/// overlay of the head part, not a separate part).
/// </remarks>
public sealed class EndermanAnimationProvider : IEntityAnimationProvider
{
    private const float LimbClamp = 0.4f;

    private static readonly string[] NominalClips = ["idle", "walk"];
    private static readonly string[] LimbParts = ["right_arm", "left_arm", "right_leg", "left_leg"];

    public EndermanAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyEnderman(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var sandbox = frame with { IsAggressive = false, SwingAnimation = 0f, SwingType = 0 };
        if (string.Equals(clip, "walk", StringComparison.Ordinal))
        {
            ApplyEnderman(sandbox with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, pose);
        }
        else
        {
            ApplyEnderman(sandbox with { WalkAnimationSpeed = 0f }, pose);
        }
    }

    private static void ApplyEnderman(in EntityAnimationFrame frame, EntityPose pose)
    {
        HumanoidArmPoseResolver.Compute(frame, skeleton: false, out var right, out var left);
        HumanoidModelPose.Apply(frame, pose, HumanoidVariant.Plain, right, left, isHoldingBow: false);

        foreach (var part in LimbParts)
        {
            var offset = pose.Get(part);
            var xRot = Math.Clamp(offset.Rotation.X * 0.5f, -LimbClamp, LimbClamp);
            pose.Set(part, offset with { Rotation = new Vector3(xRot, offset.Rotation.Y, offset.Rotation.Z) });
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
