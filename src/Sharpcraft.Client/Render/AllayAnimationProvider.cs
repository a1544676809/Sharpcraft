// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>AllayModel.setupAnim</c> plus the client-side derivations from <c>Allay.aiStep</c> /
/// <c>AllayRenderer.extractRenderState</c>. Fully procedural: the wings flap, the body tilts with the flying factor,
/// the arms fold up while holding an item, and the whole model bobs; when dancing the root/head sway and the body spins.
/// </summary>
/// <remarks>
/// <para>
/// The generic frame does not carry dancing/spinning/holding progress, so the provider keeps a per-entity
/// <see cref="State"/> that implements both <see cref="IEntitySyncedDataAnimationState"/> (DATA_DANCING synced boolean,
/// index 17 / serializer 8) and <see cref="IEntityTickAnimationState"/> (the reference <c>aiStep</c> counters). The
/// holding-item flag is taken from <see cref="EntityAnimationFrame.HasMainHandItem"/> in <see cref="Apply"/> (there is no
/// item flag on the tick context). Progress values use <see cref="EntityAnimationFrame.PartialTick"/>, matching the
/// reference <c>getHoldingItemAnimationProgress</c>/<c>getSpinningProgress</c>.
/// </para>
/// <para>
/// All reference values are absolute assignments over a zero bind rotation, so they become additive rotation offsets
/// unchanged. The only position is <c>root.y</c>; storage is y-up (the mesher negates the reference y-down pivot), so
/// the reference <c>.y +=</c> bob is emitted as a negative Position.Y offset.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.allay.AllayModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/allay/AllayModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class AllayAnimationProvider(int typeId) : IEntityAnimationProvider
{
    /// <summary>Synced data index of the reference <c>DATA_DANCING</c> boolean.</summary>
    public const int DancingDataIndex = 17;

    /// <summary>Synced data serializer id for booleans.</summary>
    public const int BooleanSerializerId = 8;

    private static readonly string[] NominalClips = ["idle", "dance", "spin"];

    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Reference <c>0.43633232F</c>: wing pitch at rest and the arm idle-bob baseline.</summary>
    private const float WingRestPitch = 0.43633232f;

    /// <summary>Reference <c>0.27925268F</c>: arm yaw while holding an item.</summary>
    private const float ArmHoldingYaw = 0.27925268f;

    /// <summary>Reference <c>MAX_HAND_HOLDING_ITEM_X_ROT_RAD</c>.</summary>
    private const float MaxHoldingArmPitch = -1.134464f;

    public int TypeId { get; } = typeId;

    public IReadOnlyList<string> Clips => NominalClips;

    public IEntityAnimationState CreateState() => new State();

    public void Apply(IEntityAnimationState state, in EntityAnimationFrame frame, EntityPose pose)
    {
        var live = state as State;

        // The holding-item flag only reaches us through the frame; remember it for the next OnTick.
        if (live is not null)
        {
            live.HasItem = frame.HasMainHandItem;
        }

        var isDancing = live?.IsDancing ?? false;
        var isSpinning = live?.IsSpinning ?? false;
        var spinningProgress = live?.SpinningProgress(frame.PartialTick) ?? 0f;
        var holdingFactor = live?.HoldingProgress(frame.PartialTick) ?? 0f;

        ApplyPose(frame, isDancing, isSpinning, spinningProgress, holdingFactor, pose);
    }

    public void ApplyClip(IEntityAnimationState state, in EntityAnimationFrame frame, string clip, float seconds, EntityPose pose)
    {
        var clipFrame = frame with { AgeInTicks = seconds * 20f };
        switch (clip)
        {
            case "dance":
                ApplyPose(clipFrame, isDancing: true, isSpinning: false, spinningProgress: 0f, holdingFactor: 0f, pose);
                break;
            case "spin":
                // Half-way through the spin window shows the root yaw plus a half-weighted sway.
                ApplyPose(clipFrame, isDancing: true, isSpinning: true, spinningProgress: 0.5f, holdingFactor: 0f, pose);
                break;
            default:
                ApplyPose(clipFrame, isDancing: false, isSpinning: false, spinningProgress: 0f, holdingFactor: 0f, pose);
                break;
        }
    }

    private static void ApplyPose(
        in EntityAnimationFrame frame,
        bool isDancing,
        bool isSpinning,
        float spinningProgress,
        float holdingFactor,
        EntityPose pose)
    {
        var ageInTicks = frame.AgeInTicks;
        var animationSpeed = frame.WalkAnimationSpeed;
        var animationPos = frame.WalkAnimationPos;

        var flapSpeed = ageInTicks * 20f * DegreesToRadians + animationPos;
        var flapAmount = MathF.Cos(flapSpeed) * MathF.PI * 0.15f + animationSpeed;
        var idleBobSpeed = ageInTicks * 9f * DegreesToRadians;
        var flyingFactor = MathF.Min(animationSpeed / 0.3f, 1f);
        var idleBobFactor = 1f - flyingFactor;

        // Head / root orientation: dancing replaces the look-at with a sway (and a full spin while spinning).
        float rootYRot = 0f;
        float rootZRot = 0f;
        if (isDancing)
        {
            var danceSpeed = ageInTicks * 8f * DegreesToRadians + animationSpeed;
            var danceFrequency = MathF.Cos(danceSpeed) * 16f * DegreesToRadians;
            var headTiltZ = MathF.Cos(danceSpeed) * 14f * DegreesToRadians;
            var headTiltY = MathF.Cos(danceSpeed) * 30f * DegreesToRadians;
            var swayWeight = 1f - spinningProgress;
            rootZRot = danceFrequency * swayWeight;
            if (isSpinning)
            {
                rootYRot = MathF.PI * 4f * spinningProgress;
            }

            pose.Add("head", new PoseOffset(Vector3.Zero, new Vector3(0f, headTiltY * swayWeight, headTiltZ * swayWeight), Vector3.Zero));
        }
        else
        {
            pose.Add("head", new PoseOffset(
                Vector3.Zero,
                new Vector3(frame.Pitch * DegreesToRadians, frame.Yaw * DegreesToRadians, 0f),
                Vector3.Zero));
        }

        // Wings: rest pitch flattens as the allay flies, the yaw comes from the flap amount.
        var wingPitch = WingRestPitch * (1f - flyingFactor);
        pose.Add("right_wing", new PoseOffset(Vector3.Zero, new Vector3(wingPitch, (-MathF.PI / 4f) + flapAmount, 0f), Vector3.Zero));
        pose.Add("left_wing", new PoseOffset(Vector3.Zero, new Vector3(wingPitch, (MathF.PI / 4f) - flapAmount, 0f), Vector3.Zero));

        // Body tilts forward with the flying factor.
        pose.Add("body", new PoseOffset(Vector3.Zero, new Vector3(flyingFactor * (MathF.PI / 4f), 0f, 0f), Vector3.Zero));

        // Arms: holding folds them up to the holding pitch; the idle bob fades out while holding/flying.
        var armPitch = holdingFactor * Lerp(flyingFactor, -MathF.PI / 3f, MaxHoldingArmPitch);
        var armIdleBobFactor = idleBobFactor * (1f - holdingFactor);
        var armIdleBobAmount = WingRestPitch
            - (MathF.Cos(idleBobSpeed + (MathF.PI * 3f / 2f)) * MathF.PI * 0.075f * armIdleBobFactor);
        pose.Add("right_arm", new PoseOffset(
            Vector3.Zero,
            new Vector3(armPitch, ArmHoldingYaw * holdingFactor, armIdleBobAmount),
            Vector3.Zero));
        pose.Add("left_arm", new PoseOffset(
            Vector3.Zero,
            new Vector3(armPitch, -ArmHoldingYaw * holdingFactor, -armIdleBobAmount),
            Vector3.Zero));

        // Root bob (reference .y +=): negated for y-up storage.
        var rootY = -MathF.Cos(idleBobSpeed) * 0.25f * idleBobFactor;
        pose.Add("root", new PoseOffset(new Vector3(0f, rootY, 0f), new Vector3(0f, rootYRot, rootZRot), Vector3.Zero));
    }

    private static float Lerp(float delta, float start, float end) => start + (end - start) * delta;

    /// <summary>
    /// Per-entity Allay state: the reference <c>aiStep</c> counters plus the synced dancing flag and the last known
    /// holding-item flag. Progress getters mirror <c>getHoldingItemAnimationProgress</c>/<c>getSpinningProgress</c>.
    /// </summary>
    private sealed class State : IEntityAnimationState, IEntitySyncedDataAnimationState, IEntityTickAnimationState
    {
        private float _holdingTicks;
        private float _holdingTicks0;
        private float _dancingTicks;
        private float _spinningTicks;
        private float _spinningTicks0;

        public bool IsDancing { get; private set; }

        /// <summary>Last known main-hand item presence, updated from the frame each render.</summary>
        public bool HasItem { get; set; }

        public bool IsSpinning => (_dancingTicks % 55f) < 15f;

        public float HoldingProgress(float partialTick) =>
            AllayAnimationProvider.Lerp(partialTick, _holdingTicks0, _holdingTicks) / 5f;

        public float SpinningProgress(float partialTick) =>
            AllayAnimationProvider.Lerp(partialTick, _spinningTicks0, _spinningTicks) / 15f;

        public void OnSyncedData(int index, int serializerId, int value)
        {
            if (index == DancingDataIndex && serializerId == BooleanSerializerId)
            {
                IsDancing = value != 0;
            }
        }

        public void OnTick(in EntityAnimationTickContext context)
        {
            // Holding-item animation: creep toward 5 while holding, back toward 0 otherwise.
            _holdingTicks0 = _holdingTicks;
            _holdingTicks = Math.Clamp(_holdingTicks + (HasItem ? 1f : -1f), 0f, 5f);

            if (IsDancing)
            {
                _dancingTicks++;
                _spinningTicks0 = _spinningTicks;
                _spinningTicks += IsSpinning ? 1f : -1f;
                _spinningTicks = Math.Clamp(_spinningTicks, 0f, 15f);
            }
            else
            {
                _dancingTicks = 0f;
                _spinningTicks = 0f;
                _spinningTicks0 = 0f;
            }
        }
    }
}
