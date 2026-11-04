// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>WardenModel.setupAnim</c>. The always-on pose is driven purely by the generic frame: head look from
/// <see cref="EntityAnimationFrame.Pitch"/>/<see cref="EntityAnimationFrame.Yaw"/>, the idle wobble from
/// <see cref="EntityAnimationFrame.AgeInTicks"/>, the walk cycle from
/// <see cref="EntityAnimationFrame.WalkAnimationPos"/>/<see cref="EntityAnimationFrame.WalkAnimationSpeed"/>, and the
/// tendril sway from a per-entity factor.
/// </summary>
/// <remarks>
/// The reference also plays six <c>AnimationState</c> clips every frame (attack, sonic boom, dig, emerge, roar, sniff).
/// Those clocks do not exist on the generic render-state frame yet, so <see cref="Apply"/> cannot drive them; they are
/// exposed through <see cref="ApplyClip"/> for the sandbox/tooling path only. Likewise <see cref="Apply"/>'s tendril
/// sway reads a state-held factor that defaults to 0 because the render state carries no <c>tendrilAnimation</c> input
/// yet, so tendrils stay at rest until that field is wired. All contributions are additive pose offsets, matching the
/// reference's offset-style accumulators against a zero bind pose.
/// </remarks>
public sealed class WardenAnimationProvider : IEntityAnimationProvider
{
    private static readonly string[] NominalClips = ["attack", "sonic_boom", "dig", "emerge", "roar", "sniff"];

    public WardenAnimationProvider(int typeId) => TypeId = typeId;

    public int TypeId { get; }

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        // Head look target: xRot is pitch, yRot is yaw (both stored in degrees).
        var headPitch = frame.Pitch * (MathF.PI / 180f);
        var headYaw = frame.Yaw * (MathF.PI / 180f);

        // Walk cycle.
        var speedModifier = MathF.Min(0.5f, 3f * frame.WalkAnimationSpeed);
        var adjustedPos = frame.WalkAnimationPos * 0.8662f;
        var adjustedPosCosine = MathF.Cos(adjustedPos);
        var adjustedPosSine = MathF.Sin(adjustedPos);
        var speedModifierWithMin = MathF.Min(0.35f, speedModifier);

        // Idle wobble.
        var scaledAge = frame.AgeInTicks * 0.1f;
        var wobbleCosine = MathF.Cos(scaledAge);
        var wobbleSine = MathF.Sin(scaledAge);

        var headRotation = new Vector3(
            headPitch
                + (1.2f * MathF.Cos(adjustedPos + (MathF.PI / 2f)) * speedModifierWithMin)
                + (0.06f * wobbleSine),
            headYaw,
            (0.3f * adjustedPosSine * speedModifier)
                + (0.06f * wobbleCosine));
        pose.Add("head", new PoseOffset(Vector3.Zero, headRotation, Vector3.Zero));

        pose.Add("body", new PoseOffset(
            Vector3.Zero,
            new Vector3(
                (adjustedPosCosine * speedModifierWithMin) + (0.025f * wobbleCosine),
                0f,
                (0.1f * adjustedPosSine * speedModifier) + (0.025f * wobbleSine)),
            Vector3.Zero));

        pose.Add("left_leg", new PoseOffset(Vector3.Zero, new Vector3(adjustedPosCosine * speedModifier, 0f, 0f), Vector3.Zero));
        pose.Add("right_leg", new PoseOffset(Vector3.Zero, new Vector3(MathF.Cos(adjustedPos + MathF.PI) * speedModifier, 0f, 0f), Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(Vector3.Zero, new Vector3(-(0.8f * adjustedPosCosine * speedModifier), 0f, 0f), Vector3.Zero));
        pose.Add("right_arm", new PoseOffset(Vector3.Zero, new Vector3(-(0.8f * adjustedPosSine * speedModifier), 0f, 0f), Vector3.Zero));

        // Tendrils: state-held factor defaults to 0 until the render state exposes tendrilAnimation.
        var tendrilFactor = (state as State)?.TendrilAnimation ?? 0f;
        if (tendrilFactor != 0f)
        {
            var tendrilXRot = tendrilFactor * (MathF.Cos(frame.AgeInTicks * 2.25f) * MathF.PI * 0.1f);
            pose.Add("left_tendril", new PoseOffset(Vector3.Zero, new Vector3(tendrilXRot, 0f, 0f), Vector3.Zero));
            pose.Add("right_tendril", new PoseOffset(Vector3.Zero, new Vector3(-tendrilXRot, 0f, 0f), Vector3.Zero));
        }
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var animation = clip switch
        {
            "attack" => WardenAnimations.AttackBaked,
            "sonic_boom" => WardenAnimations.SonicBoomBaked,
            "dig" => WardenAnimations.DigBaked,
            "emerge" => WardenAnimations.EmergeBaked,
            "roar" => WardenAnimations.RoarBaked,
            "sniff" => WardenAnimations.SniffBaked,
            _ => null
        };
        animation?.Apply(seconds, 1f, pose);
    }

    /// <summary>
    /// Per-entity Warden animation state. <see cref="TendrilAnimation"/> mirrors the reference render state's tendril
    /// sway multiplier; it stays 0 because no trajectory/anger input feeds it yet.
    /// </summary>
    private sealed class State : IEntityAnimationState
    {
        public float TendrilAnimation { get; set; }
    }
}
