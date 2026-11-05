// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>PhantomModel.setupAnim</c>: fully procedural. The flap clock is
/// <c>flapTime = EntityId * 3 + AgeInTicks</c> (the renderer's unique flap tick offset plus age), and
/// <c>anim = flapTime * 7.448451°</c>. Wing base/tip z-rotation = <c>cos(anim) * 16°</c> (right = -left) and tail
/// base/tip x-rotation = <c>-(5 + cos(2·anim) * 5)°</c>.
/// </summary>
/// <remarks>
/// The reference assigns those rotations absolutely; our pose system is additive over the model bind pose, so the
/// wing offsets subtract the bind z-rotation (±0.1 rad, stored in <see cref="PhantomModelDefinition"/>). The tail bind
/// x-rotation is 0, so its offset is the assigned value. Rotations keep the reference signs. Part names match
/// <see cref="PhantomModelDefinition"/>: tail_base/tail_tip and left/right wing base/tip (body and head are static).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.phantom.PhantomModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/phantom/PhantomModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class PhantomAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle", "flap"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference flap frequency in degrees per flap-time unit.</summary>
    private const float FlapFrequencyDegrees = 7.448451f;

    /// <summary>Reference wing flap amplitude (degrees).</summary>
    private const float WingFlapDegrees = 16f;

    /// <summary>Reference tail sway base and amplitude (degrees).</summary>
    private const float TailBaseDegrees = 5f;
    private const float TailSwayDegrees = 5f;

    /// <summary>Bind z-rotation of the wing base/tip parts (±0.1, matching the reference PartPose).</summary>
    private const float WingBindZ = 0.1f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) => ApplyPose(frame, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "flap", StringComparison.Ordinal))
        {
            // Explicit clip clock in seconds; the reference samples ageInTicks, so convert 20 ticks per second.
            ApplyPose(frame with { AgeInTicks = seconds * 20f }, pose);
        }
        else
        {
            // "idle" samples the live frame's flap clock (the reference has one continuous flap cycle).
            ApplyPose(frame, pose);
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, EntityPose pose)
    {
        var flapTime = (frame.EntityId * 3f) + frame.AgeInTicks;
        var anim = flapTime * FlapFrequencyDegrees * DegreesToRadians;

        var wingZ = MathF.Cos(anim) * WingFlapDegrees * DegreesToRadians;
        var tailX = -(TailBaseDegrees + (MathF.Cos(anim * 2f) * TailSwayDegrees)) * DegreesToRadians;

        // Left wings: absolute cos(anim)*16°; subtract the +0.1 bind z-rotation.
        pose.Add("left_wing_base", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, wingZ - WingBindZ), Vector3.Zero));
        pose.Add("left_wing_tip", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, wingZ - WingBindZ), Vector3.Zero));
        // Right wings: absolute -cos(anim)*16°; subtract the -0.1 bind z-rotation.
        pose.Add("right_wing_base", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, -wingZ + WingBindZ), Vector3.Zero));
        pose.Add("right_wing_tip", new PoseOffset(Vector3.Zero, new Vector3(0f, 0f, -wingZ + WingBindZ), Vector3.Zero));

        // Tail: bind x-rotation is 0, so the absolute value is the offset.
        pose.Add("tail_base", new PoseOffset(Vector3.Zero, new Vector3(tailX, 0f, 0f), Vector3.Zero));
        pose.Add("tail_tip", new PoseOffset(Vector3.Zero, new Vector3(tailX, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
