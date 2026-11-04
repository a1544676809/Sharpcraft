// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>GuardianModel.setupAnim</c> together with the client-side clocks from <c>Guardian.aiStep</c>. The head
/// looks along the render-state pitch/yaw; the twelve spikes are repositioned by the withdrawal factor
/// <c>(1 - spikesAnimation)·0.55</c> around their bind pivots; and the three tail segments fan out by
/// <c>sin(tailAnimation)·π·(0.05/0.1/0.15)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Eye look is blocked.</b> The reference moves the eye from <c>lookAtPosition</c>/<c>lookDirection</c>/<c>eyePosition</c>
/// (camera/look-at target, resolved in the renderer). Those inputs are not on <see cref="EntityAnimationFrame"/> and
/// cannot be reconstructed from an entity tick, so the eye is intentionally left at its bind pose rather than faked.
/// </para>
/// <para>
/// The spike pivots are additive over the bind pose: the reference writes the absolute position
/// <c>SPIKE_* [i]·offset</c> (x/z) and <c>16 + SPIKE_Y[i]·offset</c> (y) with
/// <c>offset = 1 + cos(ageInTicks·1.5 + i)·0.01 - withdrawal</c>. Removing the bind (offset 1) leaves
/// <c>SPIKE_* [i]·(offset - 1)</c>; the mesher stores pivots y-up, so the y delta is negated (x and z keep the reference
/// sign, matching the other procedural providers).
/// </para>
/// <para>
/// The tail and spikes clocks are presentation derivations of the reference client entity: a locally random tail phase
/// advanced by a speed that depends on water/movement, plus a spikes factor that randomises out of water and eases
/// toward 0 (moving) or 1 (still) in water.
/// </para>
/// </remarks>
public sealed class GuardianAnimationProvider(int typeId) : IEntityAnimationProvider
{
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference <c>withdrawal = (1 - spikesAnimation) * 0.55</c>.</summary>
    private const float SpikeWithdrawal = 0.55f;

    /// <summary>Horizontal distance (blocks/tick) above which the entity is treated as moving.</summary>
    private const float IsMovingThreshold = 0.01f;

    // Reference SPIKE_X/Y/Z tables (index 0..11).
    private static readonly float[] SpikeX = [0f, 0f, 8f, -8f, -8f, 8f, 8f, -8f, 0f, 0f, 8f, -8f];
    private static readonly float[] SpikeY = [-8f, -8f, -8f, -8f, 0f, 0f, 0f, 0f, 8f, 8f, 8f, 8f];
    private static readonly float[] SpikeZ = [8f, -8f, 0f, 0f, -8f, -8f, 8f, 8f, 8f, -8f, 0f, 0f];

    // Precomputed so the hot path never concatenates strings; index i is "spike{i}".
    private static readonly string[] SpikeNames =
    [
        "spike0", "spike1", "spike2", "spike3", "spike4", "spike5",
        "spike6", "spike7", "spike8", "spike9", "spike10", "spike11"
    ];

    private static readonly string[] NominalClips = ["idle", "swim"];

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var clocks = state as State;
        var partial = frame.PartialTick;
        var tailAnimation = clocks?.TailAnimation(partial) ?? 0f;
        var spikesAnimation = clocks?.SpikesAnimation(partial) ?? 0f;
        ApplyPose(frame.AgeInTicks, frame.Pitch, frame.Yaw, tailAnimation, spikesAnimation, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        // Procedural clips have no keyframes; "seconds" drives the tail phase and picks the spike posture for a preview.
        switch (clip)
        {
            case "idle":
                ApplyPose(frame.AgeInTicks, frame.Pitch, frame.Yaw, seconds * 2.5f, 1f, pose);
                break;
            case "swim":
                ApplyPose(frame.AgeInTicks, frame.Pitch, frame.Yaw, seconds * 10f, 0f, pose);
                break;
        }
    }

    private static void ApplyPose(float ageInTicks, float pitch, float yaw, float tailAnimation, float spikesAnimation, EntityPose pose)
    {
        // Head look: reference head.yRot = yaw, head.xRot = pitch.
        pose.Add("head", new PoseOffset(
            Vector3.Zero,
            new Vector3(pitch * DegreesToRadians, yaw * DegreesToRadians, 0f),
            Vector3.Zero));

        // Spikes: withdrawal pulls them toward the body; positions are additive offsets from the bind pivots.
        var withdrawal = (1f - spikesAnimation) * SpikeWithdrawal;
        for (var i = 0; i < SpikeNames.Length; i++)
        {
            var offset = (1f + (MathF.Cos((ageInTicks * 1.5f) + i) * 0.01f)) - withdrawal;
            var delta = offset - 1f;
            pose.Add(SpikeNames[i], new PoseOffset(
                new Vector3(SpikeX[i] * delta, -(SpikeY[i] * delta), SpikeZ[i] * delta),
                Vector3.Zero,
                Vector3.Zero));
        }

        // Tail fan-out; all three segments share sin(tailAnimation) with 0.05/0.1/0.15 factors.
        var swim = MathF.Sin(tailAnimation);
        pose.Add("tail0", new PoseOffset(Vector3.Zero, new Vector3(0f, swim * MathF.PI * 0.05f, 0f), Vector3.Zero));
        pose.Add("tail1", new PoseOffset(Vector3.Zero, new Vector3(0f, swim * MathF.PI * 0.1f, 0f), Vector3.Zero));
        pose.Add("tail2", new PoseOffset(Vector3.Zero, new Vector3(0f, swim * MathF.PI * 0.15f, 0f), Vector3.Zero));

        // Eye look is blocked (needs camera/target inputs); the eye is deliberately left at the bind pose.
    }

    /// <summary>
    /// Per-entity Guardian state: the client-side tail/spikes clocks from <c>Guardian.aiStep</c>. Public so the
    /// derived <see cref="TailAnimation(float)"/> / <see cref="SpikesAnimation(float)"/> values can be inspected by the
    /// animation tests, mirroring <see cref="Sharpcraft.Client.World.SquidTentacleAnimationState"/>.
    /// </summary>
    public sealed class State : IEntityAnimationState, IEntityTickAnimationState
    {
        private readonly Random _random = new();
        private float _tail;
        private float _tailPrevious;
        private float _tailSpeed;
        private float _spikes;
        private float _spikesPrevious;

        public State()
        {
            // Reference constructor: clientSideTailAnimation = random.nextFloat(); O = current.
            _tail = _random.NextSingle();
            _tailPrevious = _tail;
        }

        /// <summary>Reference <c>getTailAnimation(partial)</c>: lerp from the previous tick to the current.</summary>
        public float TailAnimation(float partialTick) => _tailPrevious + ((_tail - _tailPrevious) * partialTick);

        /// <summary>Reference <c>getSpikesAnimation(partial)</c>: lerp from the previous tick to the current.</summary>
        public float SpikesAnimation(float partialTick) => _spikesPrevious + ((_spikes - _spikesPrevious) * partialTick);

        public void OnTick(in EntityAnimationTickContext context)
        {
            var moving = context.HorizontalDistance > IsMovingThreshold;

            // Tail speed: 2 out of water, ramps toward 0.5 while moving in water, eases toward 0.125 when still in water.
            _tailPrevious = _tail;
            if (!context.IsInWater)
            {
                _tailSpeed = 2f;
            }
            else if (moving)
            {
                if (_tailSpeed < 0.5f)
                {
                    _tailSpeed = 4f;
                }
                else
                {
                    _tailSpeed += (0.5f - _tailSpeed) * 0.1f;
                }
            }
            else
            {
                _tailSpeed += (0.125f - _tailSpeed) * 0.2f;
            }

            _tail += _tailSpeed;

            // Spikes: randomise out of water, retract toward 0 while moving in water, extend toward 1 when still.
            _spikesPrevious = _spikes;
            if (!context.IsInWater)
            {
                _spikes = _random.NextSingle();
            }
            else if (moving)
            {
                _spikes += (0f - _spikes) * 0.25f;
            }
            else
            {
                _spikes += (1f - _spikes) * 0.06f;
            }
        }
    }
}
