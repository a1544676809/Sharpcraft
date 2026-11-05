// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>EnderDragonModel.setupAnim</c>. Only the flap-clock-driven, rotation-only sections can be derived from
/// the animation inputs available today: the jaw, both two-segment wings (with their skin membranes) and the six leg
/// chains. Every remaining section is left at the bind pose and recorded as blocked below — deliberately not faked.
/// </summary>
/// <remarks>
/// Derivable now:
/// <list type="bullet">
/// <item><c>jaw.xRot = (sin(flap) + 1) · 0.2</c>;</item>
/// <item>wings: <c>xRot = 0.125 − cos(flap) · 0.2</c>, <c>yRot = ∓0.25</c>, <c>zRot = ±(sin(flap) + 0.125) · 0.8</c>,
/// tip <c>zRot = ∓(sin(flap + 2) + 0.5) · 0.75</c>;</item>
/// <item><c>poseLimbs</c>: <c>bounce = ((sin(flap − 1) + 1)² + 2·(sin(flap − 1) + 1)) · 0.05</c> drives every leg's
/// <c>xRot</c> by ±<c>0.1·bounce</c> (the front-leg tip is the only negative one).</item>
/// </list>
/// <c>flap</c> is the tick-space <c>flapTime</c> multiplied by 2π. <see cref="State"/> advances <c>flapTime</c> once per
/// tick exactly like the reference's common flight branch —
/// <c>flapSpeed = 0.2 / (horizontalDistance·10 + 1) · 2^verticalDistance</c> — using the per-tick displacement from
/// <see cref="EntityAnimationTickContext"/> (the Ender Dragon is <c>noPhysics</c>, so its position delta equals its
/// velocity), and interpolates with the partial tick.
/// <para>Blocked (not approximated; these parts stay at the bind pose):</para>
/// <list type="bullet">
/// <item>All five <c>neck0..4</c> rotations and positions, the <c>head</c> rotation/position, <c>body.zRot</c> and all
/// twelve <c>tail0..11</c> rotations and positions. Every one reads
/// <c>EnderDragonRenderState.getHistoricalPos(delay)</c> from a 64-sample <c>DragonFlightHistory</c> of per-tick
/// <c>(y, yRot)</c> samples (delays 0..23), and the neck x-rotation additionally reads
/// <c>getHeadPartYOffset(i, start, point)</c>, which branches on <c>isLandingOrTakingOff</c>, <c>isSitting</c> (the
/// dragon phase) and <c>distanceToEgg</c>. None of that history or phase data exists on
/// <see cref="EntityAnimationFrame"/> or the tick context, so porting them would require synthesising a flight path.</item>
/// <item><c>root.y</c> / <c>root.z</c> / <c>root.xRot</c>: the reference transforms a model root group, but
/// <see cref="EnderDragonModelDefinition"/> has no <c>root</c> part (head/neck/tail/body are top-level), so the group
/// transform cannot be reproduced per part.</item>
/// <item>The <c>flapTime</c> sitting (<c>+0.1</c>/tick), <c>inWall</c> (<c>×0.5</c>) and dying (frozen) branches: they
/// need the dragon phase / inWall / death flags, which the frame and tick context do not carry.</item>
/// </list>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.dragon.EnderDragonModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/dragon/EnderDragonModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class EnderDragonAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var clocks = state as State;
        var flapTime = clocks is null ? 0f : clocks.FlapTimeAt(frame.PartialTick);
        ApplyFlapPose(flapTime, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        if (string.Equals(clip, "idle", StringComparison.Ordinal))
        {
            // Preview path only: the common flight branch advances flapTime by ~0.2/tick, i.e. ~4 per second at 20 tps.
            ApplyFlapPose(seconds * 4f, pose);
        }
    }

    private static void ApplyFlapPose(float flapTimeTicks, EntityPose pose)
    {
        var flap = flapTimeTicks * MathF.Tau;

        // jaw.xRot = (sin(flap) + 1) * 0.2, bind rotation 0.
        AddRotation(pose, "jaw", (MathF.Sin(flap) + 1f) * 0.2f, 0f, 0f);

        // bounce drives every leg x rotation; the front-leg tip is assigned -0.1*bounce, all others +0.1*bounce.
        var beat = MathF.Sin(flap - 1f) + 1f;
        var bounce = ((beat * beat) + (beat * 2f)) * 0.05f;
        var legDelta = 0.1f * bounce;

        // Wings, bind rotations 0. The skin membranes are separate parts sharing the wing's pivot, so they inherit the
        // same rotation as the bone (the reference keeps bone and skin in one ModelPart).
        var wingX = 0.125f - (MathF.Cos(flap) * 0.2f);
        var wingY = -0.25f;
        var wingZ = -(MathF.Sin(flap) + 0.125f) * 0.8f;
        var tipZ = (MathF.Sin(flap + 2f) + 0.5f) * 0.75f;
        AddRotation(pose, "left_wing", wingX, wingY, wingZ);
        AddRotation(pose, "left_wing_skin", wingX, wingY, wingZ);
        AddRotation(pose, "left_wing_tip", 0f, 0f, tipZ);
        AddRotation(pose, "left_wing_tip_skin", 0f, 0f, tipZ);
        AddRotation(pose, "right_wing", wingX, -wingY, -wingZ);
        AddRotation(pose, "right_wing_skin", wingX, -wingY, -wingZ);
        AddRotation(pose, "right_wing_tip", 0f, 0f, -tipZ);
        AddRotation(pose, "right_wing_tip_skin", 0f, 0f, -tipZ);

        // Legs: front bind xRot 1.3 / -0.5 / 0.75, hind bind xRot 1.0 / 0.5 / 0.75; delta = assignment - bind.
        AddRotation(pose, "left_front_leg", legDelta, 0f, 0f);
        AddRotation(pose, "left_front_leg_tip", -legDelta, 0f, 0f);
        AddRotation(pose, "left_front_foot", legDelta, 0f, 0f);
        AddRotation(pose, "right_front_leg", legDelta, 0f, 0f);
        AddRotation(pose, "right_front_leg_tip", -legDelta, 0f, 0f);
        AddRotation(pose, "right_front_foot", legDelta, 0f, 0f);
        AddRotation(pose, "left_hind_leg", legDelta, 0f, 0f);
        AddRotation(pose, "left_hind_leg_tip", legDelta, 0f, 0f);
        AddRotation(pose, "left_hind_foot", legDelta, 0f, 0f);
        AddRotation(pose, "right_hind_leg", legDelta, 0f, 0f);
        AddRotation(pose, "right_hind_leg_tip", legDelta, 0f, 0f);
        AddRotation(pose, "right_hind_foot", legDelta, 0f, 0f);
    }

    private static void AddRotation(EntityPose pose, string part, float x, float y, float z) =>
        pose.Add(part, new PoseOffset(Vector3.Zero, new Vector3(x, y, z), Vector3.Zero));

    /// <summary>
    /// Per-entity flap clock. Public so the animation tests can drive and inspect it, mirroring the Guardian state.
    /// </summary>
    /// <remarks>
    /// Only the reference's common flight branch is derivable here. The sitting, inWall and death branches need the
    /// dragon phase / collision / death flags that the tick context does not carry, so they are left unported.
    /// </remarks>
    public sealed class State : IEntityAnimationState, IEntityTickAnimationState
    {
        /// <summary>Current-tick <c>flapTime</c> (reference <c>EnderDragon.flapTime</c>).</summary>
        public float FlapTime { get; private set; }

        /// <summary>Previous-tick <c>flapTime</c> (reference <c>EnderDragon.oFlapTime</c>).</summary>
        public float PreviousFlapTime { get; private set; }

        /// <summary>Reference <c>Mth.lerp(partialTicks, oFlapTime, flapTime)</c>.</summary>
        public float FlapTimeAt(float partialTick) => PreviousFlapTime + ((FlapTime - PreviousFlapTime) * partialTick);

        public void OnTick(in EntityAnimationTickContext context)
        {
            PreviousFlapTime = FlapTime;
            var flapSpeed = 0.2f / ((context.HorizontalDistance * 10f) + 1f);
            flapSpeed *= MathF.Pow(2f, context.VerticalDistance);
            FlapTime += flapSpeed;
        }
    }
}
