// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Rabbit animation: only the derivable base head pose is implemented. The reference <c>RabbitModel.setupAnim</c> applies
/// two baked <c>AnimationState</c> clips (HOP and IDLE_HEAD_TILT) and otherwise lets the head look along the render-state
/// pitch/yaw — the head-look path is the part that can be reproduced without the local timers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Blocked (reported, not faked — HOP/IDLE_HEAD_TILT are not applied):</b>
/// <list type="bullet">
/// <item><b>Hop timing:</b> <c>Rabbit.jumpTicks</c>/<c>jumpDuration</c> are set by the client entity's jump control and the
/// server's entity event id 1, and advanced in <c>aiStep</c>; the per-tick event/aiStep ordering is not represented
/// exactly on <see cref="EntityAnimationTickContext"/>. Using ground/vertical transitions would be an approximation, so
/// the hop clip is intentionally not driven (this avoids faking the bounce).</item>
/// <item><b>Idle timing:</b> <c>idleAnimationTimeout = random.nextInt(40) + 180</c> is seeded from the entity's private
/// RNG, and <c>shouldPlayIdleAnimation</c> also depends on leash/no-AI state — neither is on the frame. So the idle head
/// tilt cannot start at the exact tick.</item>
/// <item><b>Baby:</b> babies use <c>BabyRabbitModel</c> / <c>BabyRabbitAnimation</c> (different geometry/keys); our
/// definition is the adult mesh, so baby is not reproduced.</item>
/// </list>
/// Because the idle clip cannot start, the reference's "idle not started" branch applies, so the head always looks along
/// the pitch/yaw here — the derivable subset.
/// </para>
/// </remarks>
public sealed class RabbitAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["idle"];

    private const float DegreesToRadians = MathF.PI / 180f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
            Vector3.Zero));

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose) =>
        Apply(state, frame, pose);

    private sealed class State : IEntityAnimationState
    {
    }
}
