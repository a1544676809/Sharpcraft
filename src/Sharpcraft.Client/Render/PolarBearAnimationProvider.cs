// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>PolarBearModel.setupAnim</c>. The base pose is <c>QuadrupedModel.setupAnim</c> (head look plus the four
/// legs' walk swing). The standing pose then adds the reference <c>standScale²</c> body/head/front-leg rears.
/// </summary>
/// <remarks>
/// <para>
/// <b>Standing is settled-only.</b> The reference <c>standScale</c> is a client-lerped value
/// (<c>Mth.lerp(partialTick, prev, current) / 6</c>) where <c>clientSideStandAnimation</c> ramps by 1 per tick toward 0…6.
/// <see cref="EntityAnimationFrame"/> only carries <c>IsStanding</c>, not that counter, so the 6-tick ease-in/out cannot be
/// reproduced. Once settled the counter is clamped (6 when standing, 0 when not), so
/// <c>standScale = IsStanding ? 1 : 0</c> reproduces the settled reference pose exactly; the transient ramp is a known
/// approximation, not a fake settled pose. <c>bodyAgeScale = state.ageScale</c> is reconstructed from
/// <c>IsBaby</c> (<c>0.5</c> for babies, else <c>1</c>, matching <c>LivingEntity.getAgeScale</c>).
/// </para>
/// <para>
/// The flat model splits the reference <c>head</c> part's extra cubes (mouth, ears) and the reference <c>body</c> part's
/// second cube (<c>body_upper</c>) into separate root parts with the same pivot, so those siblings receive the head/body
/// offsets too; that is required to rotate/translate them with their reference parent.
/// </para>
/// <para>
/// Rotations are additive offsets equal to the reference deltas. Positions follow the repository y-up convention: the
/// vertical delta is negated, x/z keep the reference sign.
/// </para>
/// </remarks>
public sealed class PolarBearAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    private static readonly string[] NominalClips = ["idle", "walk", "stand"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose) =>
        ApplyPose(frame, frame.IsStanding, pose);

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        switch (clip)
        {
            case "walk":
                ApplyPose(frame with { WalkAnimationPos = seconds * 10f, WalkAnimationSpeed = 1f }, isStanding: false, pose);
                break;
            case "stand":
                ApplyPose(frame with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, isStanding: true, pose);
                break;
            case "idle":
                ApplyPose(frame with { WalkAnimationPos = 0f, WalkAnimationSpeed = 0f }, isStanding: false, pose);
                break;
        }
    }

    private static void ApplyPose(in EntityAnimationFrame frame, bool isStanding, EntityPose pose)
    {
        // Quadruped base.
        var phase = frame.WalkAnimationPos * 0.6662f;
        var amplitude = 1.4f * frame.WalkAnimationSpeed;
        var rightHind = MathF.Cos(phase) * amplitude;
        var leftHind = MathF.Cos(phase + MathF.PI) * amplitude;
        var rightFront = leftHind;
        var leftFront = rightHind;

        var headX = frame.Pitch * DegreesToRadians;
        var headY = frame.Yaw * DegreesToRadians;
        var headYPos = 0f;
        var headZPos = 0f;

        var bodyRotX = 0f;
        var bodyYPos = 0f;

        var frontLegDeg = 0f;          // shared extra xRot from standing
        var frontLegYPos = 0f;
        var frontLegZPos = 0f;

        if (isStanding)
        {
            var bodyAgeScale = frame.IsBaby ? 0.5f : 1f;
            var standScale = 1f; // settled standing
            var s = standScale * standScale;

            // head: xRot += s·π·0.15; y -= s·24; z += s·13 (position y negated for y-up storage).
            headX += s * MathF.PI * 0.15f;
            headYPos = s * 24f;
            headZPos = s * 13f;

            // body: xRot -= s·π·0.35; y += s·ageScale·2 (position y negated).
            bodyRotX = -s * MathF.PI * 0.35f;
            bodyYPos = -(s * bodyAgeScale * 2f);

            // front legs: xRot -= s·π·0.45; y -= s·ageScale·20; z += s·ageScale·4 (position y negated).
            frontLegDeg = -s * MathF.PI * 0.45f;
            frontLegYPos = s * bodyAgeScale * 20f;
            frontLegZPos = s * bodyAgeScale * 4f;
        }

        // Head group: the reference head part owns the mouth/ear cubes; drive the split siblings with the same offsets.
        var headRotation = new Vector3(headX, headY, 0f);
        var headPosition = new Vector3(0f, headYPos, headZPos);
        pose.Add("head", new PoseOffset(headPosition, headRotation, Vector3.Zero));
        pose.Add("mouth", new PoseOffset(headPosition, headRotation, Vector3.Zero));
        pose.Add("right_ear", new PoseOffset(headPosition, headRotation, Vector3.Zero));
        pose.Add("left_ear", new PoseOffset(headPosition, headRotation, Vector3.Zero));

        // Body group: both reference body cubes share the standing rear.
        var bodyOffset = new PoseOffset(new Vector3(0f, bodyYPos, 0f), new Vector3(bodyRotX, 0f, 0f), Vector3.Zero);
        pose.Add("body", bodyOffset);
        pose.Add("body_upper", bodyOffset);

        // Legs: walk swing; the front pair additionally rears while standing.
        pose.Add("right_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(rightHind, 0f, 0f), Vector3.Zero));
        pose.Add("left_hind_leg", new PoseOffset(Vector3.Zero, new Vector3(leftHind, 0f, 0f), Vector3.Zero));
        var frontPosition = new Vector3(0f, frontLegYPos, frontLegZPos);
        pose.Add("right_front_leg", new PoseOffset(frontPosition, new Vector3(rightFront + frontLegDeg, 0f, 0f), Vector3.Zero));
        pose.Add("left_front_leg", new PoseOffset(frontPosition, new Vector3(leftFront + frontLegDeg, 0f, 0f), Vector3.Zero));
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
