// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;
using Sharpcraft.AuthLib;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.World;

/// <summary>One tracked entity as last reported by the server.</summary>
public sealed class TrackedEntity
{
    internal TrackedEntity(int id, int typeId, double x, double y, double z, float yaw, float pitch)
    {
        Id = id;
        TypeId = typeId;
        Presentation = new EntityPresentationState(typeId);
        Presentation.InitializeRotation(yaw);
        X = x;
        Y = y;
        Z = z;
        PreviousX = x;
        PreviousY = y;
        PreviousZ = z;
        Yaw = yaw;
        Pitch = pitch;
        PreviousYaw = yaw;
        PreviousPitch = pitch;
        HeadYaw = yaw;
        TargetX = x;
        TargetY = y;
        TargetZ = z;
        TargetYaw = yaw;
        TargetPitch = pitch;
    }

    /// <summary>Runtime entity id.</summary>
    public int Id { get; }

    /// <summary>Entity type registry id.</summary>
    public int TypeId { get; }

    /// <summary>Persistent identity, when the server sent one.</summary>
    public Guid Uuid { get; internal set; }

    /// <summary>
    /// Per-entity animation state, created once when the entity is first tracked and dropped with it. Null when the
    /// type has no animation provider or no state factory was injected.
    /// </summary>
    public IEntityAnimationState? Animation { get; internal set; }

    /// <summary>Client-side presentation state (walk/flap/tentacle/ground), advanced once per tick; never authoritative.</summary>
    public EntityPresentationState Presentation { get; }

    /// <summary>Per-tick Ender Dragon flight history; empty for every other entity type.</summary>
    public DragonFlightHistory DragonFlightHistory { get; } = new();

    /// <summary>Raw Ender Dragon phase id (0..10), used only by the renderer.</summary>
    public int DragonPhase { get; internal set; } = 10;

    /// <summary>Feet position, current tick.</summary>
    public double X { get; internal set; }

    /// <summary>Feet y, current tick.</summary>
    public double Y { get; internal set; }

    /// <summary>Feet z, current tick.</summary>
    public double Z { get; internal set; }

    /// <summary>Feet position at the previous tick, used for interpolation.</summary>
    public double PreviousX { get; internal set; }

    /// <summary>Previous y.</summary>
    public double PreviousY { get; internal set; }

    /// <summary>Previous z.</summary>
    public double PreviousZ { get; internal set; }

    /// <summary>Yaw in degrees.</summary>
    public float Yaw { get; internal set; }

    /// <summary>Pitch in degrees.</summary>
    public float Pitch { get; internal set; }

    /// <summary>Head yaw in degrees, driven by the separate rotate-head packet.</summary>
    public float HeadYaw { get; internal set; }

    /// <summary>Eye height above the feet from the vanilla entity-type dimensions table (pose/age aware); 0 when unknown.</summary>
    public float EyeHeight => (EntityDimensionsRegistry.Get(TypeId, Presentation.Pose, IsBaby)?.EyeHeight ?? 0f) * Presentation.Scale;

    /// <summary>Per-entity synced attribute map (SCALE and any other server-sent attributes).</summary>
    public TrackedAttributeMap Attributes { get; } = new();

    /// <summary>Synced max-health attribute, or 0 when the server has not sent it.</summary>
    public float MaxHealth => Attributes.Get(AttributeTypes.GetId("max_health"), 0f);

    /// <summary>World-space eye Y (feet y + eye height).</summary>
    public float EyeY => (float)Y + EyeHeight;

    /// <summary>Yaw at the previous tick.</summary>
    public float PreviousYaw { get; internal set; }

    /// <summary>Pitch at the previous tick.</summary>
    public float PreviousPitch { get; internal set; }

    /// <summary>Velocity in blocks per tick.</summary>
    public double VelocityX { get; internal set; }

    /// <summary>Velocity y.</summary>
    public double VelocityY { get; internal set; }

    /// <summary>Velocity z.</summary>
    public double VelocityZ { get; internal set; }

    /// <summary>Whether the entity reports standing on ground.</summary>
    public bool OnGround { get; internal set; }

    /// <summary>
    /// Raw appearance variant from synced entity data (registry index for registry-backed types, enum id for fixed
    /// ones). Zero means "the entity's default variant"; see docs/analysis/render/entity-variants.md.
    /// </summary>
    public int VariantId { get; internal set; }

    /// <summary>
    /// Item id carried by an item entity (<c>ItemEntity.DATA_ITEM</c>), or -1 when unknown. The item stack lives in the
    /// entity's synced data as an <c>ITEM_STACK</c> serializer, so this is the first place a dropped item becomes
    /// renderable.
    /// </summary>
    public int ItemStackItemId { get; internal set; } = -1;

    /// <summary>Stack count carried by an item entity, or zero when unknown.</summary>
    public int ItemStackCount { get; internal set; }

    /// <summary>Raw byte flag word from synced entity data (tame/nectar/sleeping bits).</summary>
    public int Flags { get; internal set; }

    /// <summary>Anger end time in game ticks, or long.MinValue when unknown.</summary>
    public long AngerEndTime { get; internal set; } = long.MinValue;

    /// <summary>Game tick at which the variant/state value last changed (for states that depend on elapsed time).</summary>
    public long StateStartTick { get; internal set; }

    /// <summary>True when the entity reports the AgeableMob baby flag (synced data index 16).</summary>
    public bool IsBaby { get; internal set; }

    /// <summary>Raw dye id of a tamed cat/wolf collar, or -1 when unknown.</summary>
    public int CollarColor { get; internal set; } = -1;

    /// <summary>Raw horse markings id (0 none, 1 white, 2 white field, 3 white dots, 4 black dots).</summary>
    public int Markings { get; internal set; }

    private readonly int[] _equipmentItemIds = [-1, -1, -1, -1, -1, -1, -1, -1];
    private readonly int[] _equipmentCounts = new int[8];
    private readonly ItemComponentInfo[] _equipmentComponents = new ItemComponentInfo[8];
    private readonly string?[] _equipmentAssetIds = new string?[8];
    private readonly int?[] _equipmentDyeColors = new int?[8];
    private readonly bool?[] _equipmentGlints = new bool?[8];
    private readonly bool?[] _equipmentHasEnchantments = new bool?[8];
    private readonly bool?[] _equipmentHasBannerPatterns = new bool?[8];
    private readonly IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?[] _equipmentBannerPatternLayers = new IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?[8];
    private readonly string?[] _equipmentTrimMaterials = new string?[8];
    private readonly string?[] _equipmentTrimPatterns = new string?[8];

    /// <summary>Item registry id in <paramref name="slot"/> (0..7), or -1 when empty.</summary>
    public int EquipmentItemAt(int slot) =>
        (uint)slot < (uint)_equipmentItemIds.Length ? _equipmentItemIds[slot] : -1;

    /// <summary>Decoded component subset for the item in <paramref name="slot"/>; default when empty.</summary>
    public ItemComponentInfo EquipmentComponentsAt(int slot) =>
        (uint)slot < (uint)_equipmentComponents.Length ? _equipmentComponents[slot] : default;

    /// <summary>Equippable asset id decoded from the slot's item component, or null when absent.</summary>
    public string? EquipmentAssetIdAt(int slot) =>
        (uint)slot < (uint)_equipmentAssetIds.Length ? _equipmentAssetIds[slot] : null;

    /// <summary>All eight equippable asset ids, reused by the per-frame render state.</summary>
    public IReadOnlyList<string?> EquipmentAssetIds => _equipmentAssetIds;

    /// <summary>Dyed-colour component for one equipment slot, or null when absent.</summary>
    public int? EquipmentDyeColorAt(int slot) =>
        (uint)slot < (uint)_equipmentDyeColors.Length ? _equipmentDyeColors[slot] : null;

    /// <summary>All eight dyed-colour components, reused by the per-frame render state.</summary>
    public IReadOnlyList<int?> EquipmentDyeColors => _equipmentDyeColors;

    /// <summary>Glint override for one equipment slot, or null when the stack did not specify one.</summary>
    public bool? EquipmentGlintOverrideAt(int slot) =>
        (uint)slot < (uint)_equipmentGlints.Length ? _equipmentGlints[slot] : null;

    /// <summary>All eight glint overrides, reused by the per-frame render state.</summary>
    public IReadOnlyList<bool?> EquipmentGlintOverrides => _equipmentGlints;

    /// <summary>Whether one equipment slot's item has enchantments, when known.</summary>
    public bool? EquipmentHasEnchantmentsAt(int slot) =>
        (uint)slot < (uint)_equipmentHasEnchantments.Length ? _equipmentHasEnchantments[slot] : null;

    /// <summary>All eight enchantment-presence flags, reused by the per-frame render state.</summary>
    public IReadOnlyList<bool?> EquipmentHasEnchantments => _equipmentHasEnchantments;

    /// <summary>
    /// Whether one equipment slot's item carries banner patterns or a base colour. The reference shield renderer picks
    /// between the patterned and unpatterned base sprite from exactly this condition.
    /// </summary>
    public bool? EquipmentHasBannerPatternsAt(int slot) =>
        (uint)slot < (uint)_equipmentHasBannerPatterns.Length ? _equipmentHasBannerPatterns[slot] : null;

    /// <summary>All eight banner-pattern-presence flags, reused by the per-frame render state.</summary>
    public IReadOnlyList<bool?> EquipmentHasBannerPatterns => _equipmentHasBannerPatterns;

    /// <summary>Decoded banner pattern layers for one equipment slot, or null when the item carries none.</summary>
    public IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>? EquipmentBannerPatternsAt(int slot) =>
        (uint)slot < (uint)_equipmentBannerPatternLayers.Length ? _equipmentBannerPatternLayers[slot] : null;

    /// <summary>All eight decoded banner pattern layer lists, reused by the per-frame render state.</summary>
    public IReadOnlyList<IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?> EquipmentBannerPatternLayers =>
        _equipmentBannerPatternLayers;

    /// <summary>Trim material registry id for one equipment slot, or null when the item has no trim.</summary>
    public string? EquipmentTrimMaterialAt(int slot) =>
        (uint)slot < (uint)_equipmentTrimMaterials.Length ? _equipmentTrimMaterials[slot] : null;

    /// <summary>All eight trim material ids, reused by the per-frame render state.</summary>
    public IReadOnlyList<string?> EquipmentTrimMaterials => _equipmentTrimMaterials;

    /// <summary>Trim pattern registry id for one equipment slot, or null when the item has no trim.</summary>
    public string? EquipmentTrimPatternAt(int slot) =>
        (uint)slot < (uint)_equipmentTrimPatterns.Length ? _equipmentTrimPatterns[slot] : null;

    /// <summary>All eight trim pattern ids, reused by the per-frame render state.</summary>
    public IReadOnlyList<string?> EquipmentTrimPatterns => _equipmentTrimPatterns;

    /// <summary>Stack count in <paramref name="slot"/>; 0 when empty.</summary>
    public int EquipmentCountAt(int slot) =>
        (uint)slot < (uint)_equipmentCounts.Length ? _equipmentCounts[slot] : 0;

    public void ApplyEquipment(ReadOnlySpan<EquipmentItem> items)
    {
        foreach (var item in items)
        {
            if ((uint)item.Slot >= (uint)_equipmentItemIds.Length)
            {
                continue;
            }

            _equipmentItemIds[item.Slot] = item.IsEmpty ? -1 : item.ItemId;
            _equipmentCounts[item.Slot] = item.IsEmpty ? 0 : item.Count;
            _equipmentComponents[item.Slot] = item.IsEmpty ? default : item.Components;
            _equipmentAssetIds[item.Slot] = item.IsEmpty ? null : item.Components.EquippableAssetId;
            _equipmentDyeColors[item.Slot] = item.IsEmpty ? null : item.Components.DyedColor;
            _equipmentGlints[item.Slot] = item.IsEmpty ? null : item.Components.GlintOverride;
            _equipmentHasEnchantments[item.Slot] = item.IsEmpty ? null : item.Components.HasEnchantments;
            _equipmentTrimMaterials[item.Slot] = item.IsEmpty ? null : item.Components.TrimMaterialId;
            _equipmentTrimPatterns[item.Slot] = item.IsEmpty ? null : item.Components.TrimPatternId;
            _equipmentHasBannerPatterns[item.Slot] = item.IsEmpty
                ? null
                : (item.Components.BannerPatternLayers is { Count: > 0 } || item.Components.BaseColorId is not null);
            _equipmentBannerPatternLayers[item.Slot] = item.IsEmpty ? null : item.Components.BannerPatternLayers;
            EquipmentUpdates++;
        }
    }

    /// <summary>Number of equipment slot updates applied, for diagnostics.</summary>
    public int EquipmentUpdates { get; private set; }

    /// <summary>
    /// Authoritative target position. Vanilla does not snap a remote entity to the position the server reports:
    /// it approaches it over a few ticks, which is why the rendered entity trails slightly behind the
    /// authoritative one (docs/specs/network/entity-sync.md 9.4).
    /// </summary>
    public double TargetX { get; internal set; }

    /// <summary>Target y.</summary>
    public double TargetY { get; internal set; }

    /// <summary>Target z.</summary>
    public double TargetZ { get; internal set; }

    /// <summary>Target yaw in degrees.</summary>
    public float TargetYaw { get; internal set; }

    /// <summary>Target pitch in degrees.</summary>
    public float TargetPitch { get; internal set; }

    /// <summary>Ticks of interpolation left before the entity sits on its target.</summary>
    public int LerpSteps { get; internal set; }

    /// <summary>Copies the current state into the previous one, so a later update interpolates from here.</summary>
    private readonly List<InterpStep> _steps = new();
    private double _lastStepX, _lastStepY, _lastStepZ;
    private float _lastStepYaw, _lastStepPitch;
    private float _currentStepTicks = 1f, _remainingTicks, _interpSpeed = 1f;

    private readonly record struct InterpStep(double X, double Y, double Z, float Yaw, float Pitch, int TickOffset);

    /// <summary>Port of SteppedInterpolationHandler.startInterpolating: enqueue a move step of tickOffset = update interval.</summary>
    internal void EnqueueInterpolation(double x, double y, double z, float yaw, float pitch, int tickOffset)
    {
        if (_steps.Count == 0)
        {
            _lastStepX = X;
            _lastStepY = Y;
            _lastStepZ = Z;
            _lastStepYaw = Yaw;
            _lastStepPitch = Pitch;
            _currentStepTicks = 1f;
        }

        _steps.Add(new InterpStep(x, y, z, yaw, pitch, tickOffset));
        _remainingTicks += tickOffset;
    }

    /// <summary>Game tick when the last move/teleport packet for this entity was applied (adaptive step size).</summary>
    public long LastMoveTick { get; internal set; }

    internal float RemainingTicks => _remainingTicks;

    internal float CurrentStepTicks => _currentStepTicks;

    /// <summary>Diagnostics: last move packet's raw Y delta and total move packets seen.</summary>
    public double LastDeltaY { get; internal set; }

    /// <summary>Diagnostics: number of position-bearing move packets applied.</summary>
    public long MovePackets { get; internal set; }

    /// <summary>Diagnostics: Y taken from the AddEntity packet when this entity spawned.</summary>
    public double SpawnY { get; internal set; }

    /// <summary>Diagnostics: largest absolute per-packet Y delta ever seen.</summary>
    public double MaxAbsDeltaY { get; internal set; }

    /// <summary>Reference VecDeltaCodec base: absolute position the next relative move delta is decoded against.</summary>
    public double CodecBaseX { get; internal set; }

    /// <inheritdoc cref="CodecBaseX"/>
    public double CodecBaseY { get; internal set; }

    /// <inheritdoc cref="CodecBaseX"/>
    public double CodecBaseZ { get; internal set; }

    /// <summary>Diagnostics: number of move packets with a non-zero Y delta.</summary>
    public long NonZeroDeltaYCount { get; internal set; }

    /// <summary>Port of SteppedInterpolationHandler.doInterpolate + advance: move one tick along the queued steps.</summary>
    internal void StepInterpolation()
    {
        if (_steps.Count == 0 && _remainingTicks <= 0f)
        {
            return;
        }

        while (_steps.Count > 0)
        {
            var step = _steps[0];
            if (_currentStepTicks < step.TickOffset)
            {
                var a = _currentStepTicks / step.TickOffset;
                X = LerpD(_lastStepX, step.X, a);
                Y = LerpD(_lastStepY, step.Y, a);
                Z = LerpD(_lastStepZ, step.Z, a);
                Yaw = Wrap360L(LerpAngleL(_lastStepYaw, step.Yaw, a));
                Pitch = LerpF(_lastStepPitch, step.Pitch, a);
                break;
            }

            _currentStepTicks -= step.TickOffset;
            _lastStepX = step.X;
            _lastStepY = step.Y;
            _lastStepZ = step.Z;
            _lastStepYaw = step.Yaw;
            _lastStepPitch = step.Pitch;
            _steps.RemoveAt(0);
            if (_steps.Count == 0)
            {
                X = _lastStepX;
                Y = _lastStepY;
                Z = _lastStepZ;
                Yaw = Wrap360L(_lastStepYaw);
                Pitch = _lastStepPitch;
            }
        }

        Advance(1f, EntityTracker.InterpolationSteps);
    }

    private void Advance(float ticks, int steps)
    {
        var targetSpeed = MathF.Max(_remainingTicks / steps, 1f);
        _interpSpeed = LerpF(_interpSpeed, targetSpeed, 1f / steps);
        if (ticks * _interpSpeed < _remainingTicks)
        {
            ticks *= _interpSpeed;
        }
        else
        {
            ticks = _remainingTicks;
            _interpSpeed = 1f;
        }

        _currentStepTicks += ticks;
        _remainingTicks -= ticks;
    }

    private static double LerpD(double from, double to, float a) => from + ((to - from) * a);

    internal void ClearInterpolation()
    {
        _steps.Clear();
        _remainingTicks = 0f;
        _currentStepTicks = 1f;
        _interpSpeed = 1f;
    }

    private static float LerpF(float from, float to, float a) => from + ((to - from) * a);

    private static float Wrap360L(float d) { var v = d % 360f; return v < 0f ? v + 360f : v; }

    private static float ShortL(float d) { var v = d % 360f; if (v >= 180f) v -= 360f; if (v < -180f) v += 360f; return v; }

    private static float LerpAngleL(float a, float b, float t) => Wrap360L(a + (ShortL(b - a) * t));

    internal void PushPrevious()
    {
        PreviousX = X;
        PreviousY = Y;
        PreviousZ = Z;
        PreviousYaw = Yaw;
        PreviousPitch = Pitch;
    }
}

/// <summary>What the renderer needs for one entity this frame.</summary>
/// <param name="Id">Runtime entity id.</param>
/// <param name="TypeId">Entity type registry id.</param>
/// <param name="X">Interpolated feet x.</param>
/// <param name="Y">Interpolated feet y.</param>
/// <param name="Z">Interpolated feet z.</param>
/// <param name="Yaw">Interpolated yaw in degrees.</param>
/// <param name="Pitch">Interpolated pitch in degrees.</param>
/// <param name="ShowHat">Whether the player hat overlay is visible.</param>
/// <param name="ShowJacket">Whether the player jacket overlay is visible.</param>
/// <param name="ShowLeftSleeve">Whether the left sleeve overlay is visible.</param>
/// <param name="ShowRightSleeve">Whether the right sleeve overlay is visible.</param>
/// <param name="ShowLeftPants">Whether the left pants overlay is visible.</param>
/// <param name="ShowRightPants">Whether the right pants overlay is visible.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.state.EntityRenderState", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/state/EntityRenderState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public readonly record struct EntityRenderState(
    int Id,
    int TypeId,
    float X,
    float Y,
    float Z,
    float Yaw,
    float Pitch,
    bool ShowHat = true,
    bool ShowJacket = true,
    bool ShowLeftSleeve = true,
    bool ShowRightSleeve = true,
    bool ShowLeftPants = true,
    bool ShowRightPants = true,
    bool HasChest = false,
    bool HasEgg = false,
    int VariantId = 0,
    int ItemEntityItemId = -1,
    int ItemEntityCount = 0,
    bool IsTame = false,
    bool IsAngry = false,
    bool IsSleeping = false,
    bool HasNectar = false,
    bool IsCold = false,
    bool IsCharging = false,
    bool IsHidingInShell = false,
    System.Numerics.Vector3 Tint = default,
    System.Numerics.Vector3 PatternTint = default,
    bool IsBaby = false,
    System.Numerics.Vector3 CollarTint = default,
    bool HasCollar = false,
    int Markings = 0,
    EntityEquipment Equipment = default,
    float AgeInTicks = 0f,
    float WalkAnimationPos = 0f,
    float WalkAnimationSpeed = 0f,
    float Flap = 0f,
    float FlapSpeed = 0f,
    float TentacleAngle = 0f,
    bool IsResting = false,
    IEntityAnimationState? Animation = null,
    float AnimationPartialTick = 0f,
    bool HasMainHandItem = false,
    bool HasOffHandItem = false,
    bool IsOnGround = false,
    bool IsInWater = false,
    bool IsPassenger = false,
    bool IsLeashed = false,
    bool IsVehicle = false,
    bool IsSitting = false,
    bool IsMoving = false,
    float HeadYaw = 0f,
    float SwingAnimation = 0f,
    bool IsAggressive = false,
    int MainHandItemId = -1,
    int ArmPose = 0,
    bool IsUsingItem = false,
    float TicksUsingItem = 0f,
    int OffHandItemId = -1,
    bool MainHandChargedCrossbow = false,
    int MainHandQuickChargeLevel = 0,
    bool IsLeftHanded = false,
    float Squish = 0f,
    int CubeSize = 1,
    int AttachFace = -1,
    bool IsCrouching = false,
    bool IsPouncing = false,
    bool IsFaceplanted = false,
    float Health = 0f,
    int UnhappyCounter = 0,
    int SneezeCounter = 0,
    bool IsStanding = false,
    float JumpCompletion = 0f,
    bool IsJumping = false,
    float StandScale = 0f,
    bool IsEating = false,
    bool IsRolling = false,
    bool IsOnBack = false,
    float SitAmount = 0f,
    float LieOnBackAmount = 0f,
    float RollAmount = 0f,
    bool IsPartyParrot = false,
    bool IsWorried = false,
    bool IsThundering = false,
    float WitherHeadYaw1 = 0f,
    float WitherHeadPitch1 = 0f,
    float WitherHeadYaw2 = 0f,
    float WitherHeadPitch2 = 0f,
    bool IsIdleHeadTiltStarted = false,
    float IdleHeadTiltAgeTicks = 0f,
    float Scale = 1f,
    float MaxHealth = 0f,
    float SpeedValue = 1f,
    float SwimAmount = 0f,
    bool IsVisuallySwimming = false,
    bool IsFallFlying = false,
    int UseItemHand = 0,
    int SwingHand = 0,
    int SwingType = 0,
    bool IsSprinting = false,
    float LieDownAmount = 0f,
    float LieDownAmountTail = 0f,
    float RelaxStateOneAmount = 0f,
    int Crackiness = 0,
    float ElytraRotX = 0f,
    float ElytraRotY = 0f,
    float ElytraRotZ = 0f,
    IReadOnlyList<string?>? EquipmentAssetIds = null,
    IReadOnlyList<int?>? EquipmentDyeColors = null,
    IReadOnlyList<bool?>? EquipmentGlintOverrides = null,
    IReadOnlyList<bool?>? EquipmentHasEnchantments = null,
    IReadOnlyList<bool?>? EquipmentHasBannerPatterns = null,
    IReadOnlyList<IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>?>? EquipmentBannerPatternLayers = null,
    IReadOnlyList<string?>? EquipmentTrimMaterials = null,
    IReadOnlyList<string?>? EquipmentTrimPatterns = null,
    string? PlayerBodyTexture = null,
    string? PlayerCapeTexture = null,
    string? PlayerElytraTexture = null,
    bool ShowCape = true,
    bool Slim = false,
    Guid Uuid = default,
    DragonFlightHistory? DragonFlightHistory = null,
    bool DragonIsLandingOrTakingOff = false,
    bool DragonIsSitting = false,
    double DragonDistanceToEgg = 0,
    bool IsHoldingPotion = false)
{
    /// <summary>Equippable asset id for one equipment slot, when the item supplied one.</summary>
    public string? EquipmentAssetIdAt(int slot) =>
        EquipmentAssetIds is { } ids && (uint)slot < (uint)ids.Count ? ids[slot] : null;

    /// <summary>Dyed-colour component for one equipment slot, when the item supplied one.</summary>
    public int? EquipmentDyeColorAt(int slot) =>
        EquipmentDyeColors is { } colors && (uint)slot < (uint)colors.Count ? colors[slot] : null;

    /// <summary>Glint override for one equipment slot, when the item supplied one.</summary>
    public bool? EquipmentGlintOverrideAt(int slot) =>
        EquipmentGlintOverrides is { } glints && (uint)slot < (uint)glints.Count ? glints[slot] : null;

    /// <summary>Enchantment-presence flag for one equipment slot, when known.</summary>
    public bool? EquipmentHasEnchantmentsAt(int slot) =>
        EquipmentHasEnchantments is { } enchanted && (uint)slot < (uint)enchanted.Count ? enchanted[slot] : null;

    /// <summary>Banner-pattern-presence flag for one equipment slot, when known.</summary>
    public bool? EquipmentHasBannerPatternsAt(int slot) =>
        EquipmentHasBannerPatterns is { } patterned && (uint)slot < (uint)patterned.Count ? patterned[slot] : null;

    /// <summary>Decoded banner pattern layers for one equipment slot, or null when the item carries none.</summary>
    public IReadOnlyList<Sharpcraft.Common.Network.BannerPatternLayer>? EquipmentBannerPatternsAt(int slot) =>
        EquipmentBannerPatternLayers is { } layers && (uint)slot < (uint)layers.Count ? layers[slot] : null;

    /// <summary>Trim material registry id for one equipment slot, when the item supplied one.</summary>
    public string? EquipmentTrimMaterialAt(int slot) =>
        EquipmentTrimMaterials is { } materials && (uint)slot < (uint)materials.Count ? materials[slot] : null;

    /// <summary>Trim pattern registry id for one equipment slot, when the item supplied one.</summary>
    public string? EquipmentTrimPatternAt(int slot) =>
        EquipmentTrimPatterns is { } patterns && (uint)slot < (uint)patterns.Count ? patterns[slot] : null;

    /// <summary>
    /// Player texture used by equipment layers marked <c>use_player_texture</c>. The reference prefers an explicit
    /// elytra texture and falls back to the cape only while the cape model part is shown (WingsLayer.getPlayerElytraTexture);
    /// a non-player entity has no avatar skin and therefore no override.
    /// </summary>
    public string? PlayerElytraOverride =>
        TypeId != 159 // Entity type 159 is the player (EntityPlaceholderMesher.PlayerEntityTypeId).
            ? null
            : !string.IsNullOrEmpty(PlayerElytraTexture)
                ? PlayerElytraTexture
                : ShowCape && !string.IsNullOrEmpty(PlayerCapeTexture)
                    ? PlayerCapeTexture
                    : null;
}

/// <summary>
/// Keeps the entities the server told us about (docs/specs/network/entity-sync.md 5).
/// </summary>
/// <remarks>
/// The server is authoritative: movement arrives as either a delta or an absolute teleport, and removals are
/// explicit. Updates for ids we never saw are normal around a reconnect and are counted instead of thrown.
/// <para>
/// The interpolation buffer is reused, so a steady frame allocates nothing; that is asserted by a test because
/// the interface path is a hot path under principle 3.6.
/// </para>
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.multiplayer.ClientLevel", "ref://mc-26.3-analysis/src/net/minecraft/client/multiplayer/ClientLevel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Merges ClientLevel/Entity/SteppedInterpolationHandler responsibilities; position sync per T-639/T-640")]
public sealed partial class EntityTracker
{
    /// <summary>
    /// <c>minecraft:item</c> registry id. Derived from the registration order in
    /// <c>ref://mc-26.3-analysis/src/net/minecraft/world/entity/EntityTypes.java</c>; the same derivation yields 148 for
    /// witch and 152 for wolf, which match the ids already used across this repo.
    /// </summary>
    public const int ItemEntityTypeId = 72;

    /// <summary>Accessor index of <c>ItemEntity.DATA_ITEM</c> (Entity owns 0-7, ItemEntity defines this next).</summary>
    public const int ItemEntityItemAccessorIndex = 8;
    /// <summary>Upper bound on tracked entities; a runaway server must not grow this dictionary without limit.</summary>
    public const int MaxTrackedEntities = 4096;

    /// <summary>
    /// Ticks a remote entity takes to reach the position the server reported. Vanilla passes three to
    /// <c>lerpPositionAndRotationStep</c> for remote movement
    /// (docs/specs/network/entity-sync.md 9.4).
    /// </summary>
    public const int InterpolationSteps = 3;

    private readonly Dictionary<int, TrackedEntity> _entities = [];
    private readonly List<EntityRenderState> _renderStates = [];

    /// <summary>
    /// Injected factory that creates a per-entity animation state for a type id, or null when the type has no
    /// animation. The host wires this to <c>EntityAnimationRegistry.CreateState</c> so the world layer stays free of
    /// any rendering dependency; tests that do not care about animation leave it null.
    /// </summary>
    public Func<int, IEntityAnimationState?>? AnimationStateFactory { get; set; }

    /// <summary>
    /// Optional player-skin lookup keyed by profile UUID. The host wires it to the skin cache; null means only the
    /// default player texture is available, which is also what the reference falls back to.
    /// </summary>
    public Func<Guid, PlayerSkinTextures?>? PlayerSkinLookup { get; set; }

    /// <summary>
    /// Optional read-only world used by the presentation layer for local queries (currently <c>isInWater</c>). Set by
    /// the host; null means "unknown", and the derived flag stays false.
    /// </summary>
    public ClientWorld? World { get; set; }

    // Passenger graph (from set_passengers). Low-frequency updates; arrays are fine.
    private readonly Dictionary<int, int[]> _passengers = [];
    private readonly Dictionary<int, int> _vehicleOf = [];

    /// <summary>Entities currently tracked.</summary>
    public int Count => _entities.Count;

    /// <summary>Looks up the persistent identity for a runtime entity id, when the server supplied one.</summary>
    public bool TryGetUuid(int id, out Guid uuid)
    {
        if (_entities.TryGetValue(id, out var entity) && entity.Uuid != Guid.Empty)
        {
            uuid = entity.Uuid;
            return true;
        }

        uuid = Guid.Empty;
        return false;
    }

    /// <summary>Updates whose entity id was unknown; expected around reconnects.</summary>
    public int UnknownUpdates { get; private set; }

    /// <summary>Additions rejected because the table was full.</summary>
    public int OverflowDrops { get; private set; }

    /// <summary>Movement updates applied.</summary>
    public int MovesApplied { get; private set; }

    /// <summary>Teleports applied.</summary>
    public int TeleportsApplied { get; private set; }

    /// <summary>Entities removed by the server.</summary>
    public int Removals { get; private set; }

    /// <summary>Variant values applied from synced entity data.</summary>
    public int VariantUpdates { get; private set; }

    /// <summary>Introduces or replaces an entity.</summary>
    /// <param name="added">Parsed add-entity packet.</param>
    /// <returns>True when the entity is now tracked.</returns>
    public bool Add(AddedEntity added)
    {
        ArgumentNullException.ThrowIfNull(added);

        if (!_entities.TryGetValue(added.Id, out var entity))
        {
            if (_entities.Count >= MaxTrackedEntities)
            {
                OverflowDrops++;
                return false;
            }

            entity = new TrackedEntity(
                added.Id,
                added.TypeId,
                added.X,
                added.Y,
                added.Z,
                added.Yaw,
                added.Pitch);
            entity.Animation = AnimationStateFactory?.Invoke(added.TypeId);
            _entities[added.Id] = entity;
        }

        entity.Uuid = added.Uuid;
        entity.PushPrevious();
        entity.X = added.X;
        entity.Y = added.Y;
        entity.Z = added.Z;
        entity.Yaw = added.Yaw;
        entity.Pitch = ClampPitch(added.Pitch);
        entity.HeadYaw = added.HeadYaw;
        entity.TargetX = added.X;
        entity.TargetY = added.Y;
        entity.TargetZ = added.Z;
        entity.TargetYaw = added.Yaw;
        entity.TargetPitch = added.Pitch;
        entity.LerpSteps = 0;
        entity.SpawnY = added.Y;
        entity.CodecBaseX = added.X;
        entity.CodecBaseY = added.Y;
        entity.CodecBaseZ = added.Z;
        entity.LastMoveTick = GameTime - InterpolationSteps;
        entity.VelocityX = added.VelocityX;
        entity.VelocityY = added.VelocityY;
        entity.VelocityZ = added.VelocityZ;
        return true;
    }

    /// <summary>Applies a delta movement or a rotation-only update.</summary>
    /// <param name="move">Parsed movement.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyMove(in EntityMove move)
    {
        if (!_entities.TryGetValue(move.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.PushPrevious();

        entity.OnGround = move.OnGround;
        if (move.HasPosition && move.Steps is { Count: > 0 } steps)
        {
            // Reference VecDelta.Stepped.decode: decode each fixed-point delta against the running codec base, then feed
            // every absolute step (with its own tick span) to SteppedInterpolationHandler.addSteps.
            foreach (var step in steps)
            {
                entity.TargetX = step.DeltaX != 0 ? entity.CodecBaseX + (step.DeltaX / 4096.0) : entity.CodecBaseX;
                entity.TargetY = step.DeltaY != 0 ? entity.CodecBaseY + (step.DeltaY / 4096.0) : entity.CodecBaseY;
                entity.TargetZ = step.DeltaZ != 0 ? entity.CodecBaseZ + (step.DeltaZ / 4096.0) : entity.CodecBaseZ;
                entity.CodecBaseX = entity.TargetX;
                entity.CodecBaseY = entity.TargetY;
                entity.CodecBaseZ = entity.TargetZ;
                var stepTicks = Math.Clamp(step.Ticks > 0 ? step.Ticks : 1, 1, 20);
                entity.EnqueueInterpolation(entity.TargetX, entity.TargetY, entity.TargetZ,
                    move.HasRotation ? move.Yaw : entity.TargetYaw,
                    move.HasRotation ? ClampPitch(move.Pitch) : entity.TargetPitch,
                    stepTicks);
            }
        }
        else
        {
            if (move.HasPosition)
            {
                // Reference VecDeltaCodec.decode: absolute = codecBase + delta (delta 0 keeps the axis), then base = end.
                entity.TargetX = move.DeltaX != 0d ? entity.CodecBaseX + move.DeltaX : entity.CodecBaseX;
                entity.TargetY = move.DeltaY != 0d ? entity.CodecBaseY + move.DeltaY : entity.CodecBaseY;
                entity.TargetZ = move.DeltaZ != 0d ? entity.CodecBaseZ + move.DeltaZ : entity.CodecBaseZ;
                entity.CodecBaseX = entity.TargetX;
                entity.CodecBaseY = entity.TargetY;
                entity.CodecBaseZ = entity.TargetZ;
            }

            if (move.HasRotation)
            {
                entity.TargetYaw = move.Yaw;
                entity.TargetPitch = ClampPitch(move.Pitch);
            }

            var moveTicks = move.HasPosition && move.Ticks > 0
                ? Math.Clamp(move.Ticks, 1, 20)
                : Math.Clamp((int)(GameTime - entity.LastMoveTick), 1, InterpolationSteps);
            entity.EnqueueInterpolation(entity.TargetX, entity.TargetY, entity.TargetZ, entity.TargetYaw, entity.TargetPitch, moveTicks);
        }

        entity.LastMoveTick = GameTime;
        entity.LastDeltaY = move.DeltaY;
        entity.MovePackets++;
        if (Math.Abs(move.DeltaY) > 1e-6)
        {
            entity.NonZeroDeltaYCount++;
            entity.MaxAbsDeltaY = Math.Max(entity.MaxAbsDeltaY, Math.Abs(move.DeltaY));
        }
        MovesApplied++;
        return true;
    }

    private void EnqueueMove(TrackedEntity entity)
    {
        var offset = (int)Math.Clamp(GameTime - entity.LastMoveTick, 1, InterpolationSteps);
        entity.LastMoveTick = GameTime;
        entity.EnqueueInterpolation(entity.TargetX, entity.TargetY, entity.TargetZ, entity.TargetYaw, entity.TargetPitch, offset);
    }

    /// <summary>Applies a high-precision entity position sync (id 35): resets the codec base to the absolute position and
    /// either snaps (far) or enqueues the path for interpolation, matching ClientPacketListener.handleEntityPositionSync.</summary>
    public bool ApplyPositionSync(in EntityPositionSyncUpdate sync)
    {
        if (!_entities.TryGetValue(sync.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.PushPrevious();
        var dx = sync.X - entity.X;
        var dy = sync.Y - entity.Y;
        var dz = sync.Z - entity.Z;
        var tooBigToInterpolate = (dx * dx) + (dy * dy) + (dz * dz) > 4096.0;
        var yaw = Wrap360(sync.Yaw);
        var pitch = ClampPitch(sync.Pitch);

        entity.CodecBaseX = sync.X;
        entity.CodecBaseY = sync.Y;
        entity.CodecBaseZ = sync.Z;
        entity.TargetX = sync.X;
        entity.TargetY = sync.Y;
        entity.TargetZ = sync.Z;
        entity.TargetYaw = yaw;
        entity.TargetPitch = pitch;
        entity.OnGround = sync.OnGround;

        if (tooBigToInterpolate)
        {
            entity.X = sync.X;
            entity.Y = sync.Y;
            entity.Z = sync.Z;
            entity.Yaw = yaw;
            entity.Pitch = pitch;
            entity.ClearInterpolation();
        }
        else if (sync.IsStepped && sync.Steps is { Count: > 0 } steps)
        {
            foreach (var step in steps)
            {
                entity.EnqueueInterpolation(step.X, step.Y, step.Z, yaw, pitch, Math.Clamp(step.TickOffset, 1, 20));
            }
        }
        else
        {
            entity.EnqueueInterpolation(sync.X, sync.Y, sync.Z, yaw, pitch, InterpolationSteps);
        }

        return true;
    }

    /// <summary>Applies an absolute repositioning, honouring the per-component relative flags.</summary>
    /// <param name="teleport">Parsed teleport.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyTeleport(in EntityTeleport teleport)
    {
        if (!_entities.TryGetValue(teleport.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.PushPrevious();

        entity.TargetX = Relative(teleport.Relatives, 0) ? entity.TargetX + teleport.X : teleport.X;
        entity.TargetY = Relative(teleport.Relatives, 1) ? entity.TargetY + teleport.Y : teleport.Y;
        entity.TargetZ = Relative(teleport.Relatives, 2) ? entity.TargetZ + teleport.Z : teleport.Z;
        entity.TargetYaw = Wrap360(Relative(teleport.Relatives, 3) ? entity.TargetYaw + teleport.Yaw : teleport.Yaw);
        entity.TargetPitch = ClampPitch(Relative(teleport.Relatives, 4) ? entity.TargetPitch + teleport.Pitch : teleport.Pitch);
        entity.CodecBaseX = entity.TargetX;
        entity.CodecBaseY = entity.TargetY;
        entity.CodecBaseZ = entity.TargetZ;
        EnqueueMove(entity);

        entity.VelocityX = teleport.VelocityX;
        entity.VelocityY = teleport.VelocityY;
        entity.VelocityZ = teleport.VelocityZ;
        entity.OnGround = teleport.OnGround;
        TeleportsApplied++;
        return true;
    }

    /// <summary>Applies a head rotation.</summary>
    /// <param name="id">Entity id.</param>
    /// <param name="headYaw">Head yaw in degrees.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyHeadYaw(int id, float headYaw)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.HeadYaw = headYaw;
        return true;
    }

    /// <summary>Applies an absolute velocity.</summary>
    /// <param name="motion">Parsed velocity.</param>
    /// <returns>True when the entity was known.</returns>
    public bool ApplyMotion(in EntityMotion motion)
    {
        if (!_entities.TryGetValue(motion.Id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.VelocityX = motion.X;
        entity.VelocityY = motion.Y;
        entity.VelocityZ = motion.Z;
        return true;
    }

    /// <summary>
    /// Applies the variant field from a <c>set_entity_data</c> packet, if this entity's type has one.
    /// </summary>
    /// <summary>Applies one set_equipment update to the tracked entity with <paramref name="id"/>.</summary>
    public bool ApplyEquipment(int id, ReadOnlySpan<EquipmentItem> items)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            return false;
        }

        entity.ApplyEquipment(items);
        return true;
    }

    /// <summary>Routes one clientbound <c>entity_event</c> (packet 34) to the entity's animation state, if it listens.</summary>
    /// <param name="id">Runtime entity id.</param>
    /// <param name="eventId">Event id (reference <c>EntityEvent</c> constant).</param>
    /// <returns>True when the entity was tracked.</returns>
    public bool ApplyEntityEvent(int id, int eventId)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        if (entity.Animation is IEntityEventAnimationState listener)
        {
            listener.OnEntityEvent(eventId, (int)GameTime);
        }

        entity.Presentation.Tentacle?.OnEntityEvent(eventId);
        entity.Presentation.RabbitHop?.OnEntityEvent(eventId);
        return true;
    }

    /// <summary>Applies the SCALE attribute value from an <c>update_attributes</c> packet.</summary>
    /// <summary>Replaces the entity's synced attribute map and refreshes the SCALE presentation field.</summary>
    public bool ApplyAttributes(int id, ReadOnlySpan<AttributeValue> values)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Attributes.Replace(values);
        entity.Presentation.Scale = entity.Attributes.Get(AttributeTypes.GetId("scale"), 1f);
        return true;
    }

    /// <summary>Convenience for tests/tools: sets the SCALE attribute only.</summary>
    public bool ApplyAttributes(int id, float scale)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Presentation.Scale = scale;
        return true;
    }

    /// <summary>Applies a <c>set_entity_link</c> packet (leash attach/detach) to the source entity.</summary>
    public bool ApplyEntityLink(int sourceId, int destinationId)
    {
        if (!_entities.TryGetValue(sourceId, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Presentation.IsLeashed = destinationId != 0;
        return true;
    }

    /// <summary>Applies a jukebox level event (1010 start / 1011 stop) to nearby parrots.</summary>
    public bool ApplyLevelEvent(int type, long packedPosition, int data)
    {
        if (type is not (1010 or 1011))
        {
            return false;
        }

        var (bx, by, bz) = BlockPositionCodec.UnpackBlockPosition(packedPosition);
        var play = type == 1010;
        foreach (var entity in _entities.Values)
        {
            if (entity.TypeId != 99)
            {
                continue;
            }

            // Reference AABB(blockPos).inflate(3.0) around the block, then the per-tick 3.46 center check.
            if (entity.X >= bx - 3 && entity.X <= bx + 4 &&
                entity.Y >= by - 3 && entity.Y <= by + 4 &&
                entity.Z >= bz - 3 && entity.Z <= bz + 4)
            {
                entity.Presentation.IsPartyParrot = play;
                entity.Presentation.PartyJukebox = play ? packedPosition : long.MinValue;
            }
        }

        return true;
    }

    /// <summary>Applies a <c>swing_animation</c> packet (clientbound 123) to the entity's arm-swing clock.</summary>
    public bool ApplySwingAnimation(int id, int hand, int animationType, int durationTicks)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        entity.Presentation.Swing.StartIfAble(hand, animationType, durationTicks);
        return true;
    }

    /// <summary>Applies a <c>set_passengers</c> update (the vehicle's new passenger list).</summary>
    /// <param name="vehicleId">Vehicle entity id.</param>
    /// <param name="passengers">Passenger entity ids (empty clears the list).</param>
    public bool ApplyPassengers(int vehicleId, ReadOnlySpan<int> passengers)
    {
        if (_passengers.TryGetValue(vehicleId, out var old))
        {
            foreach (var previous in old)
            {
                _vehicleOf.Remove(previous);
            }
        }

        if (passengers.Length == 0)
        {
            _passengers.Remove(vehicleId);
            return true;
        }

        var copy = passengers.ToArray();
        _passengers[vehicleId] = copy;
        foreach (var passenger in copy)
        {
            _vehicleOf[passenger] = vehicleId;
        }

        return true;
    }

    /// <param name="id">Entity id the packet named.</param>
    /// <param name="items">Decoded data items.</param>
    /// <returns>True when a variant value was found and stored.</returns>
    public bool ApplyEntityData(int id, ReadOnlySpan<EntityDataItem> items)
    {
        if (!_entities.TryGetValue(id, out var entity))
        {
            UnknownUpdates++;
            return false;
        }

        var applied = HandleAnimationData(entity, items);

        // Item entities carry their stack in synced data. The accessor index is 8 because Entity itself defines eight
        // accessors (0-7) and ItemEntity extends Entity directly (docs/analysis/render/entity-variants.md 2.1).
        if (entity.TypeId == ItemEntityTypeId)
        {
            foreach (var item in items)
            {
                if (item.Index == ItemEntityItemAccessorIndex && item.ItemId >= 0)
                {
                    entity.ItemStackItemId = item.ItemId;
                    entity.ItemStackCount = item.ItemCount;
                    applied = true;
                }
            }
        }

        if (!EntityVariantSpecs.TryGet(entity.TypeId, out var spec))
        {
            return applied;
        }

        foreach (var item in items)
        {
            if (spec.AccessorIndex >= 0 && item.Index == spec.AccessorIndex && item.Value != EntityDataItem.NotAnInteger)
            {
                var variantValue = item.Value & spec.ValueMask;
                if (variantValue != entity.VariantId)
                {
                    entity.StateStartTick = GameTime;
                }

                entity.VariantId = variantValue;
                if (spec.MarkingsShift >= 0)
                {
                    entity.Markings = (item.Value >> spec.MarkingsShift) & 0xFF;
                }

                VariantUpdates++;
                applied = true;
            }
            else if (spec.FlagsAccessorIndex >= 0 && item.Index == spec.FlagsAccessorIndex && item.Value != EntityDataItem.NotAnInteger)
            {
                entity.Flags = item.Value;
                applied = true;
            }
            else if (spec.AngerAccessorIndex >= 0 && item.Index == spec.AngerAccessorIndex && item.LongValue != EntityDataItem.NotALong)
            {
                entity.AngerEndTime = item.LongValue;
                applied = true;
            }
            else if (item.Index == 16 && item.SerializerId == 8 && AgeableTypes.Contains(entity.TypeId))
            {
                entity.IsBaby = item.Value != 0;
                applied = true;
            }
            else if (spec.CollarAccessorIndex >= 0 && item.Index == spec.CollarAccessorIndex && item.Value != EntityDataItem.NotAnInteger)
            {
                entity.CollarColor = item.Value;
                applied = true;
            }
        }

        return applied;
    }

    /// <summary>Game time used to decide whether an anger end time is still in the future.</summary>
    public long GameTime { get; set; }

    /// <summary>Removes entities the server dropped.</summary>
    /// <param name="ids">Entity ids.</param>
    /// <returns>How many were actually tracked.</returns>
    public int Remove(ReadOnlySpan<int> ids)
    {
        var removed = 0;
        foreach (var id in ids)
        {
            if (_entities.Remove(id))
            {
                removed++;
            }
        }

        Removals += removed;
        return removed;
    }

    /// <summary>
    /// Advances every entity one tick towards its target, the way vanilla moves a remote entity: a fraction
    /// 1/steps of the remaining distance per tick, so the rendered entity trails the authoritative one by a few
    /// ticks instead of jumping (docs/specs/network/entity-sync.md 9.4).
    /// </summary>
    public void Tick()
    {
        foreach (var entity in _entities.Values)
        {
            entity.PushPrevious();

            entity.StepInterpolation();

            if (entity.TypeId == EnderDragonModelDefinition.EnderDragonEntityTypeId)
            {
                entity.DragonFlightHistory.Record(entity.Y, entity.Yaw);
            }

            // Horizontal distance actually travelled this tick drives the walk animation, mirroring the reference
            // calculateEntityAnimation (uses the movement delta, not the commanded velocity).
            var dx = (float)(entity.X - entity.PreviousX);
            var dz = (float)(entity.Z - entity.PreviousZ);
            var dy = (float)(entity.Y - entity.PreviousY);
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            entity.Presentation.TickBodyRotation(entity.Yaw, entity.HeadYaw, ((dx * dx) + (dz * dz)) > 2.5E-7f, 75f);
            var profile = WalkProfile.For(entity.TypeId, entity.IsBaby);
            entity.Presentation.Walk.Update(
                MathF.Min(distance * profile.DistanceToSpeed, profile.MaxSpeed),
                profile.Factor,
                profile.PositionScale);

            // Client-side presentation derivations (ADR-0022). Passenger tracking does not exist yet, so the flap
            // clock uses "not on ground" as the airborne signal (documented deviation for ridden parrots).
            entity.Presentation.IsOnGround = entity.OnGround;
            entity.Presentation.Flap.Update(!entity.OnGround);
            entity.Presentation.IsMoving = distance > 3.0E-4f;
            // Reference LivingEntity.tick -> elytraAnimationState.tick(); drives wing rotations from pose + velocity.
            entity.Presentation.Elytra.Tick(
                entity.Presentation.IsFallFlying,
                entity.Presentation.IsCrouching,
                (float)entity.VelocityX,
                (float)entity.VelocityY,
                (float)entity.VelocityZ);
            if (entity.TypeId == 120 || entity.TypeId == 81)
            {
                var presentation = entity.Presentation;
                presentation.OldSquish = presentation.Squish;
                presentation.Squish += (presentation.TargetSquish - presentation.Squish) * 0.5f;
                if (presentation.IsOnGround && !presentation.WasOnGround)
                {
                    presentation.TargetSquish = -0.5f;
                }
                else if (!presentation.IsOnGround && presentation.WasOnGround)
                {
                    presentation.TargetSquish = 1f;
                }

                // Reference AbstractCubeMob.tick decays the squish target by 0.6 every tick.
                presentation.TargetSquish *= 0.6f;
                presentation.WasOnGround = presentation.IsOnGround;
            }
            entity.Presentation.IsInWater = World?.IsWaterAt(
                (int)MathF.Floor((float)entity.X),
                (int)MathF.Floor((float)entity.Y),
                (int)MathF.Floor((float)entity.Z)) ?? false;
            var swimmingPose = entity.Presentation.Pose;
            entity.Presentation.UpdateSwimAmount(swimmingPose == 3 || (swimmingPose == 1 && !entity.Presentation.IsFallFlying));

            // Cat lie-down / relax clocks (reference Cat.updateLieDownAmount / updateRelaxStateOneAmount).
            if (entity.TypeId == 21)
            {
                var cat = entity.Presentation;
                cat.LieDownAmountO = cat.LieDownAmount;
                cat.LieDownAmountTailO = cat.LieDownAmountTail;
                cat.RelaxStateOneAmountO = cat.RelaxStateOneAmount;
                if (cat.IsLying)
                {
                    cat.LieDownAmount = MathF.Min(1f, cat.LieDownAmount + 0.15f);
                    cat.LieDownAmountTail = MathF.Min(1f, cat.LieDownAmountTail + 0.08f);
                }
                else
                {
                    cat.LieDownAmount = MathF.Max(0f, cat.LieDownAmount - 0.22f);
                    cat.LieDownAmountTail = MathF.Max(0f, cat.LieDownAmountTail - 0.13f);
                }

                cat.RelaxStateOneAmount = cat.IsRelaxStateOne
                    ? MathF.Min(1f, cat.RelaxStateOneAmount + 0.1f)
                    : MathF.Max(0f, cat.RelaxStateOneAmount - 0.13f);
            }
            entity.Presentation.Tentacle?.Update(entity.Presentation.IsInWater);
            entity.Presentation.RabbitHop?.Tick();
            entity.Presentation.RabbitIdle?.Tick((int)GameTime, entity.Presentation.IsNoAi, entity.Presentation.IsLeashed, entity.Presentation.RabbitHop?.IsJumping ?? false);
            entity.Presentation.PolarStand?.Tick(entity.Presentation.IsStanding);
            entity.Presentation.PandaPose?.Tick(entity.Presentation.IsSitting, entity.Presentation.IsOnBack, entity.Presentation.IsRolling);
            if (entity.TypeId == 99 && entity.Presentation.IsPartyParrot && entity.Presentation.PartyJukebox != long.MinValue)
            {
                var (bx, by, bz) = BlockPositionCodec.UnpackBlockPosition(entity.Presentation.PartyJukebox);
                var jx = entity.X - (bx + 0.5);
                var jy = entity.Y - (by + 0.5);
                var jz = entity.Z - (bz + 0.5);
                if ((jx * jx) + (jy * jy) + (jz * jz) > 3.46 * 3.46)
                {
                    entity.Presentation.IsPartyParrot = false;
                    entity.Presentation.PartyJukebox = long.MinValue;
                }
            }

            entity.Presentation.Swing.Tick();
            if (entity.Presentation.IsUsingItem)
            {
                entity.Presentation.UseItemTicks++;
            }
            else
            {
                entity.Presentation.UseItemTicks = 0;
            }

            if (entity.Animation is IEntityTickAnimationState tickState)
            {
                tickState.OnTick(new EntityAnimationTickContext(
                    (int)GameTime,
                    distance,
                    entity.Presentation.IsInWater,
                    entity.EquipmentItemAt(0),
                    dy,
                    entity.Presentation.IsOnGround,
                    DragonPhase: entity.DragonPhase,
                    DragonDying: entity.TypeId == EnderDragonModelDefinition.EnderDragonEntityTypeId && entity.DragonPhase == 9));
            }
        }

        UpdateWitherHeads();
    }

    private void UpdateWitherHeads()
    {
        Span<WitherHeadAim> aims = stackalloc WitherHeadAim[2];
        Span<int> targets = stackalloc int[2];
        foreach (var entity in _entities.Values)
        {
            if (entity.TypeId != 149 || entity.Presentation.WitherHeads is not { } heads)
            {
                continue;
            }

            targets[0] = entity.Presentation.WitherTarget1;
            targets[1] = entity.Presentation.WitherTarget2;
            for (var i = 0; i < 2; i++)
            {
                aims[i] = default;
                var targetId = targets[i];
                if (targetId <= 0 || !_entities.TryGetValue(targetId, out var target))
                {
                    continue;
                }

                // Reference getHeadX/Y/Z for head index i+1 (side heads): ±1.3 blocks around the body, height 2.2.
                var angle = (entity.Yaw + (180f * (i + 1))) * (MathF.PI / 180f);
                var hx = entity.X + (MathF.Cos(angle) * 1.3);
                var hy = entity.Y + 2.2;
                var hz = entity.Z + (MathF.Sin(angle) * 1.3);
                var dx = target.X - hx;
                var dy = target.EyeY - hy;
                var dz = target.Z - hz;
                var sd = MathF.Sqrt((float)((dx * dx) + (dz * dz)));
                var targetYaw = (float)(Math.Atan2(dz, dx) * 180.0 / Math.PI) - 90f;
                var targetPitch = (float)(-(Math.Atan2(dy, sd) * 180.0 / Math.PI));
                aims[i] = new WitherHeadAim(true, targetYaw, targetPitch);
            }

            heads.Tick(entity.Yaw, aims);
        }
    }

    /// <summary>Forgets every tracked entity.</summary>
    public void Clear() => _entities.Clear();

    /// <summary>Looks up an entity.</summary>
    /// <param name="id">Entity id.</param>
    /// <param name="entity">Tracked entity when found.</param>
    public bool TryGet(int id, out TrackedEntity? entity) => _entities.TryGetValue(id, out entity);

    /// <summary>Eye Y of another tracked entity, using the vanilla dimensions table (for target aiming).</summary>
    public bool TryGetEyeY(int id, out float eyeY)
    {
        if (_entities.TryGetValue(id, out var entity))
        {
            eyeY = entity.EyeY;
            return true;
        }

        eyeY = 0f;
        return false;
    }

    /// <summary>
    /// Builds the per-frame render states, interpolating between the previous and current tick. The returned span
    /// is valid until the next call and is reused, so a steady frame allocates nothing.
    /// </summary>
    /// <param name="alpha">Fraction into the current tick, 0..1.</param>
    /// <summary>Read-only interpolation snapshot for host-side diagnostics (no allocation beyond the caller's builder).</summary>
    public int AppendInterpDebug(System.Text.StringBuilder sb, int limit)
    {
        var n = 0;
        foreach (var pair in _entities)
        {
            if (n >= limit)
            {
                break;
            }

            var e = pair.Value;
            sb.Append($"[{e.Id} t{e.TypeId} Y={e.Y:F2}->{e.TargetY:F2} spawnY={e.SpawnY:F2} maxdY={e.MaxAbsDeltaY:F3} nz={e.NonZeroDeltaYCount} rem={e.RemainingTicks:F2} cur={e.CurrentStepTicks:F2} dY={e.LastDeltaY:F3} n={e.MovePackets}] ");
            n++;
        }

        return n;
    }
}
