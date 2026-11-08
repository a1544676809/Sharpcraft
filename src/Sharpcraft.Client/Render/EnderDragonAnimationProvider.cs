// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>EnderDragonModel.setupAnim</c>: the flap-clock sections (jaw, both two-segment wings with skin
/// membranes, six leg chains) plus the history-driven neck/head/body/tail chains are derived here. The model root
/// group and the non-flight flap branches remain blocked below.
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
/// <para>Blocked (not approximated):</para>
/// <list type="bullet">
/// <item><c>root.y</c> / <c>root.z</c> / <c>root.xRot</c>: the reference transforms a model root group, but
/// <see cref="EnderDragonModelDefinition"/> has no <c>root</c> part (head/neck/tail/body are top-level), so the group
/// transform cannot be reproduced per part.</item>
/// <item>The <c>flapTime</c> sitting (<c>+0.1</c>/tick), <c>inWall</c> (<c>×0.5</c>) and dying (frozen) branches: they
/// need the dragon phase / inWall / death flags; phase-derived sitting/landing flags and history are available, but the
/// remaining collision/death inputs are not carried.</item>
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
        if (frame.DragonHistory is { } history)
        {
            ApplyFlightPose(history, frame, flapTime, pose);
        }
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

        // EnderDragonModel root group: y = (bounce - 2) * 16, z = -48, xRot = bounce * 2 degrees. Pose storage is
        // y-up, so the reference y translation is negated; the mesher applies the root frame around the group origin.
        var rootY = (bounce - 2f) * 16f;
        var rootXRot = bounce * 2f * (MathF.PI / 180f);
        pose.Add("root", new PoseOffset(
            new Vector3(0f, -rootY, -48f),
            new Vector3(-rootXRot, 0f, 0f),
            Vector3.Zero));
    }

    private static void ApplyFlightPose(
        DragonFlightHistory history,
        in EntityAnimationFrame frame,
        float flapTime,
        EntityPose pose)
    {
        var start = history.Get(6, frame.PartialTick);
        var rot2 = WrapDegrees(history.Get(5, frame.PartialTick).YRot - history.Get(10, frame.PartialTick).YRot);
        var rot = WrapDegrees(history.Get(5, frame.PartialTick).YRot + (rot2 * 0.5f));

        // Neck chain: reference writes absolute x/y/z after each segment; our pose is additive over the bind pivot
        // in y-up storage, so Y is negated and Z keeps the reference sign.
        var xx = 0f;
        var yy = 20f;
        var zz = -12f;
        for (var i = 0; i < 5; i++)
        {
            var point = history.Get(5 - i, frame.PartialTick);
            var neckXRot = MathF.Cos((i * 0.45f) + flapTime) * 0.15f;
            var yRot = WrapDegrees(point.YRot - start.YRot) * Degrees * 1.5f;
            var xRot = neckXRot + (HeadPartYOffset(i, start, point, frame) * Degrees * 1.5f * 5f);
            var zRot = -WrapDegrees(point.YRot - rot) * Degrees * 1.5f;

            xx -= MathF.Sin(yRot) * MathF.Cos(xRot) * 10f;
            yy += MathF.Sin(xRot) * 10f;
            zz -= MathF.Cos(yRot) * MathF.Cos(xRot) * 10f;

            pose.Add("neck" + i, new PoseOffset(
                new Vector3(xx, -(yy - 20f), zz + 12f),
                new Vector3(xRot, yRot, zRot),
                Vector3.Zero));
        }

        var current = history.Get(0, frame.PartialTick);
        pose.Add("head", new PoseOffset(
            new Vector3(xx, -(yy - 20f), zz + 62f),
            new Vector3(
                WrapDegrees(HeadPartYOffset(6, start, current, frame)) * Degrees * 1.5f * 5f,
                WrapDegrees(current.YRot - start.YRot) * Degrees,
                -WrapDegrees(current.YRot - rot) * Degrees),
            Vector3.Zero));

        pose.Add("body", new PoseOffset(
            Vector3.Zero,
            new Vector3(0f, 0f, -rot2 * 1.5f * Degrees),
            Vector3.Zero));

        // Tail chain: bind tail0 is (0, 10, 60); the reference updates positions after assigning each segment.
        var tailStart = history.Get(11, frame.PartialTick);
        xx = 0f;
        yy = 10f;
        zz = 60f;
        var tailXRot = 0f;
        for (var i = 0; i < 12; i++)
        {
            var point = history.Get(12 + i, frame.PartialTick);
            tailXRot += MathF.Sin((i * 0.45f) + flapTime) * 0.05f;
            var yRot = (WrapDegrees(point.YRot - tailStart.YRot) * 1.5f + 180f) * Degrees;
            var xRot = tailXRot + ((float)(point.Y - tailStart.Y) * Degrees * 1.5f * 5f);
            var zRot = WrapDegrees(point.YRot - rot) * Degrees * 1.5f;

            pose.Add("tail" + i, new PoseOffset(
                new Vector3(xx, -(yy - 10f), zz - 60f),
                new Vector3(xRot, yRot, zRot),
                Vector3.Zero));

            yy += MathF.Sin(xRot) * 10f;
            zz -= MathF.Cos(yRot) * MathF.Cos(xRot) * 10f;
            xx -= MathF.Sin(yRot) * MathF.Cos(xRot) * 10f;
        }
    }

    private static float HeadPartYOffset(
        int part,
        DragonFlightHistory.Sample start,
        DragonFlightHistory.Sample point,
        in EntityAnimationFrame frame)
    {
        if (frame.DragonIsLandingOrTakingOff)
        {
            return part / MathF.Max((float)frame.DragonDistanceToEgg / 4f, 1f);
        }

        if (frame.DragonIsSitting)
        {
            return part;
        }

        return part == 6 ? 0f : (float)(point.Y - start.Y);
    }

    private static float WrapDegrees(float degrees)
    {
        var wrapped = degrees % 360f;
        if (wrapped >= 180f)
        {
            wrapped -= 360f;
        }
        else if (wrapped < -180f)
        {
            wrapped += 360f;
        }

        return wrapped;
    }

    private const float Degrees = MathF.PI / 180f;

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
            if (context.DragonDying)
            {
                return;
            }

            if (context.DragonPhase is 5 or 6 or 7)
            {
                FlapTime += 0.1f;
                return;
            }

            var flapSpeed = 0.2f / ((context.HorizontalDistance * 10f) + 1f);
            flapSpeed *= MathF.Pow(2f, context.VerticalDistance);
            FlapTime += context.DragonInWall ? flapSpeed * 0.5f : flapSpeed;
        }
    }
}
