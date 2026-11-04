// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>BeeModel.setupAnim</c> plus the client-side <c>Bee.rollAmount</c> clock. While airborne the wings flap on
/// zRot and the legs tuck to π/4; when calm and airborne the body bobs and the legs/antennae follow; a positive
/// <c>rollAmount</c> rotates the root <c>bone</c> toward the stinger-down roll angle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stinger visibility is blocked.</b> The reference sets <c>stinger.visible = state.hasStinger</c>. That flag (=
/// <c>!hasStung()</c>, the data-flags bit 4) is derivable, but the additive pose model has no visibility channel and
/// hiding the stinger by scaling/translation would be a fake, so the stinger is left untouched (always visible).
/// </para>
/// <para>
/// Roll clock: the synced <c>DATA_FLAGS_ID</c> byte is entity-data index 18 (BYTE serializer 0); the <c>FLAG_ROLL</c>
/// mask (value 2) means rolling.
/// Per tick <c>rollAmount</c> moves toward 1 (+0.2) while rolling and toward 0 (−0.24) otherwise, clamped to [0, 1];
/// the render value is lerped across the partial tick. The wing bind yRot is ±0.2618, which the reference zeroes while
/// flying, so both wings' yRot get the corresponding additive offset.
/// </para>
/// </remarks>
public sealed class BeeAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference wing pitch (flap) rate: <c>ageInTicks · 120.32113°</c>.</summary>
    private const float WingFlapDegreesPerTick = 120.32113f;

    /// <summary>Reference wing bind yRot magnitude (<c>PartPose ... ±0.2618F</c>).</summary>
    private const float WingBindY = 0.2618f;

    /// <summary>Reference stinger-down roll target (<c>Mth.rotLerpRad(rollAmount, bone.xRot, 3.0915928F)</c>).</summary>
    private const float RollTarget = 3.0915928f;

    /// <summary><c>Bee.DATA_FLAGS_ID</c> accessor index (byte) — after Entity/Living/Mob/AgeableMob fields.</summary>
    private const int BeeFlagsIndex = 18;

    private const int ByteSerializerId = 0;

    /// <summary>Reference <c>FLAG_ROLL</c> bit in the bee flags byte.</summary>
    private const int FlagRoll = 2;

    private static readonly string[] NominalClips = ["idle", "fly", "roll"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var roll = (state as State)?.RollAmount(frame.PartialTick) ?? 0f;
        ApplyPose(frame.AgeInTicks, frame.IsOnGround, frame.IsAngry, roll, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var age = seconds * 20f;
        switch (clip)
        {
            case "idle":
                ApplyPose(age, isOnGround: true, isAngry: false, rollAmount: 0f, pose);
                break;
            case "fly":
                ApplyPose(age, isOnGround: false, isAngry: false, rollAmount: 0f, pose);
                break;
            case "roll":
                ApplyPose(age, isOnGround: false, isAngry: true, rollAmount: 1f, pose);
                break;
        }
    }

    private static void ApplyPose(float ageInTicks, bool isOnGround, bool isAngry, float rollAmount, EntityPose pose)
    {
        var boneXRot = 0f;
        var boneYOffset = 0f;

        if (!isOnGround)
        {
            // Wings flap: right zRot = cos(age·120.32113°)·π·0.15, left mirrors; both yRot are zeroed (from ±0.2618).
            var wingFlap = MathF.Cos(ageInTicks * WingFlapDegreesPerTick * DegreesToRadians) * MathF.PI * 0.15f;
            pose.Add("right_wing", new PoseOffset(Vector3.Zero, new Vector3(0f, WingBindY, wingFlap), Vector3.Zero));
            pose.Add("left_wing", new PoseOffset(Vector3.Zero, new Vector3(0f, -WingBindY, -wingFlap), Vector3.Zero));

            // Legs tuck to π/4; when calm the bob overrides the front/back legs and adds the body bob + antennae.
            var frontLegX = MathF.PI / 4f;
            var backLegX = MathF.PI / 4f;
            const float midLegX = MathF.PI / 4f;

            if (!isAngry)
            {
                var bobSpeed = MathF.Cos(ageInTicks * 0.18f);
                boneXRot = 0.1f + (bobSpeed * MathF.PI * 0.025f);
                // reference bone.y is y-down and decremented, so the y-up storage offset is +cos·0.9.
                boneYOffset = MathF.Cos(ageInTicks * 0.18f) * 0.9f;
                frontLegX = (-bobSpeed * MathF.PI * 0.1f) + (MathF.PI / 8f);
                backLegX = (-bobSpeed * MathF.PI * 0.05f) + (MathF.PI / 4f);

                var antennaX = bobSpeed * MathF.PI * 0.03f;
                pose.Add("left_antenna", new PoseOffset(Vector3.Zero, new Vector3(antennaX, 0f, 0f), Vector3.Zero));
                pose.Add("right_antenna", new PoseOffset(Vector3.Zero, new Vector3(antennaX, 0f, 0f), Vector3.Zero));
            }

            pose.Add("front_legs", new PoseOffset(Vector3.Zero, new Vector3(frontLegX, 0f, 0f), Vector3.Zero));
            pose.Add("middle_legs", new PoseOffset(Vector3.Zero, new Vector3(midLegX, 0f, 0f), Vector3.Zero));
            pose.Add("back_legs", new PoseOffset(Vector3.Zero, new Vector3(backLegX, 0f, 0f), Vector3.Zero));
        }

        if (rollAmount > 0f)
        {
            boneXRot = RotLerpRad(rollAmount, boneXRot, RollTarget);
        }

        pose.Add("bone", new PoseOffset(new Vector3(0f, boneYOffset, 0f), new Vector3(boneXRot, 0f, 0f), Vector3.Zero));

        // Stinger visibility is blocked (no visibility channel); the stinger is left untouched.
    }

    /// <summary>Reference <c>Mth.rotLerpRad</c>: shortest-arc interpolation in radians.</summary>
    private static float RotLerpRad(float delta, float from, float to)
    {
        var diff = to - from;
        while (diff < -MathF.PI)
        {
            diff += MathF.PI * 2f;
        }

        while (diff >= MathF.PI)
        {
            diff -= MathF.PI * 2f;
        }

        return from + (delta * diff);
    }

    /// <summary>
    /// Per-entity Bee state: the synced flags byte (rolling bit) and the client-side <c>rollAmount</c> clock.
    /// </summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState
    {
        private int _flags;
        private float _roll;
        private float _rollPrevious;

        private bool IsRolling => (_flags & FlagRoll) != 0;

        /// <summary>Reference <c>getRollAmount(partial)</c>: lerp from the previous tick to the current.</summary>
        public float RollAmount(float partialTick) => _rollPrevious + ((_roll - _rollPrevious) * partialTick);

        public void OnSyncedData(int index, int serializerId, int value)
        {
            if (index != BeeFlagsIndex || serializerId != ByteSerializerId)
            {
                return;
            }

            _flags = value & 0xFF;
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            // Reference updateRollAmount: +0.2 while rolling, −0.24 otherwise, clamped to [0, 1].
            _rollPrevious = _roll;
            if (IsRolling)
            {
                _roll = MathF.Min(1f, _roll + 0.2f);
            }
            else
            {
                _roll = MathF.Max(0f, _roll - 0.24f);
            }
        }
    }
}
