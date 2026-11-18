// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;
using Sharpcraft.AuthLib;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.World;

public sealed partial class EntityTracker
{
    // ItemStack.hasFoil(): an explicit glint override wins, otherwise enchantment presence decides.
    private static bool HasFoil(ItemComponentInfo components) =>
        components.GlintOverride ?? components.HasEnchantments;

    public ReadOnlySpan<EntityRenderState> Interpolate(float alpha)
    {
        var t = Math.Clamp(alpha, 0f, 1f);
        _renderStates.Clear();

        foreach (var entity in _entities.Values)
        {
            EntityVariantSpecs.TryGet(entity.TypeId, out var spec);
            var stateTicks = GameTime - entity.StateStartTick;
            var hidingInShell = spec.ShellHideStates
                && (entity.VariantId == 2
                    || (entity.VariantId == 1 && stateTicks > 5)
                    || (entity.VariantId == 3 && stateTicks < 26));
            var tint = default(System.Numerics.Vector3);
            var patternTint = default(System.Numerics.Vector3);
            if (entity.TypeId == 140)
            {
                tint = DyeTint((entity.VariantId >> 16) & 0xFF);
                patternTint = DyeTint((entity.VariantId >> 24) & 0xFF);
            }

            // Derived humanoid-render-state inputs (reference HumanoidMobRenderer.extractHumanoidRenderState).
            var fallFlying = entity.Presentation.IsFallFlying;
            var speedValue = 1f;
            if (fallFlying)
            {
                var velocityLengthSqr = (float)((entity.VelocityX * entity.VelocityX) + (entity.VelocityY * entity.VelocityY) + (entity.VelocityZ * entity.VelocityZ));
                speedValue = velocityLengthSqr / 0.2f;
                speedValue *= speedValue * speedValue;
                if (speedValue < 1f)
                {
                    speedValue = 1f;
                }
            }

            var pose = entity.Presentation.Pose;
            var visuallySwimming = pose == 3 || (pose == 1 && !fallFlying);

            // Wolf armor crackiness (reference Crackiness.WOLF_ARMOR over body-slot durability; 0 none, 1 low, 2 medium, 3 high).
            var crackiness = 0;
            var bodyComponents = entity.EquipmentComponentsAt(6);
            if (entity.EquipmentItemAt(6) > 0 && bodyComponents.MaxDamage > 0)
            {
                var remaining = (float)(bodyComponents.MaxDamage - bodyComponents.Damage) / bodyComponents.MaxDamage;
                crackiness = remaining < 0.32f ? 3 : remaining < 0.69f ? 2 : remaining < 0.95f ? 1 : 0;
            }

            PlayerSkinTextures? playerSkin = null;
            if (entity.TypeId == 159 && entity.Uuid != Guid.Empty && PlayerSkinLookup is { } skinLookup)
            {
                playerSkin = skinLookup(entity.Uuid);
            }

            // With no usable profile body texture the reference falls back to the UUID-indexed default skin, which
            // also decides the arm width. Keep the Slim flag and the body texture on the same default.
            var hasCustomBody = playerSkin?.Body is not null;
            var slim = hasCustomBody ? playerSkin!.Value.Slim : DefaultPlayerSkin.Get(entity.Uuid).Slim;
            var dragon = entity.TypeId == EnderDragonModelDefinition.EnderDragonEntityTypeId;
            var ticksUsingItem = entity.Presentation.IsUsingItem ? entity.Presentation.UseItemTicks + t : 0f;
            var mainArm = entity.TypeId == 159
                ? (entity.MainArm == 0 ? 0 : 1)
                : (entity.Presentation.IsLeftHanded ? 0 : 1);
            var usingArm = entity.Presentation.UseItemHand == 0 ? mainArm : 1 - mainArm;
            var useItemId = entity.EquipmentItemAt(entity.Presentation.UseItemHand);
            var heldOnHeadItemId = entity.TypeId == 159
                && entity.Presentation.IsUsingItem
                && useItemId > 0
                && EntityModelResolver.Items?.ById(useItemId) is { } heldItem
                && heldItem.Id.Path == "spyglass"
                ? useItemId
                : -1;

            _renderStates.Add(new EntityRenderState(
                entity.Id,
                entity.TypeId,
                (float)(entity.PreviousX + ((entity.X - entity.PreviousX) * t)),
                (float)(entity.PreviousY + ((entity.Y - entity.PreviousY) * t)),
                (float)(entity.PreviousZ + ((entity.Z - entity.PreviousZ) * t)),
                Wrap360(LerpAngle(entity.Presentation.BodyYawO, entity.Presentation.BodyYaw, t)),
                entity.PreviousPitch + ((entity.Pitch - entity.PreviousPitch) * t),
                VariantId: entity.VariantId,
                ItemEntityItemId: entity.ItemStackItemId,
                ItemEntityCount: entity.ItemStackCount,
                IsTame: spec.TameMask != 0 && (entity.Flags & spec.TameMask) != 0,
                IsAngry: spec.AngerAccessorIndex >= 0 && entity.AngerEndTime > GameTime,
                IsSleeping: (spec.SleepingMask != 0 && (entity.Flags & spec.SleepingMask) != 0) || entity.Presentation.IsSleeping,
                HasNectar: spec.NectarMask != 0 && (entity.Flags & spec.NectarMask) != 0,
                IsCold: spec.ColdMask != 0 && (entity.Flags & spec.ColdMask) != 0,
                IsCharging: spec.ChargingMask != 0 && (entity.Flags & spec.ChargingMask) != 0,
                IsHidingInShell: hidingInShell,
                Tint: tint,
                PatternTint: patternTint,
                IsBaby: entity.IsBaby,
                CollarTint: DyeTint(entity.CollarColor >= 0 ? entity.CollarColor : 14),
                HasCollar: spec.CollarAccessorIndex >= 0 && spec.TameMask != 0 && (entity.Flags & spec.TameMask) != 0,
                IsPowered: entity.IsPowered,
                IsSmall: entity.TypeId == ArmorStandModelDefinition.ArmorStandEntityTypeId && (entity.ArmorStandFlags & 1) != 0,
                ShowArms: entity.TypeId == ArmorStandModelDefinition.ArmorStandEntityTypeId && (entity.ArmorStandFlags & 4) != 0,
                ShowBasePlate: entity.TypeId != ArmorStandModelDefinition.ArmorStandEntityTypeId || (entity.ArmorStandFlags & 8) == 0,
                ShowHat: entity.TypeId != ArmorStandModelDefinition.ArmorStandEntityTypeId,
                HeadPose: entity.HeadPose,
                BodyPose: entity.BodyPose,
                LeftArmPose: entity.LeftArmPose,
                RightArmPose: entity.RightArmPose,
                LeftLegPose: entity.LeftLegPose,
                RightLegPose: entity.RightLegPose,
                MainArm: mainArm,
                LeftTicksUsingItem: entity.Presentation.IsUsingItem && usingArm == 0 ? ticksUsingItem : 0f,
                RightTicksUsingItem: entity.Presentation.IsUsingItem && usingArm == 1 ? ticksUsingItem : 0f,
                SwingDurationTicks: entity.Presentation.Swing.DurationTicks,
                IsSwinging: entity.Presentation.Swing.IsSwinging,
                HeldOnHeadItemId: heldOnHeadItemId,
                Markings: entity.Markings,
                Equipment: new EntityEquipment(
                    entity.EquipmentItemAt(0), entity.EquipmentItemAt(1), entity.EquipmentItemAt(2), entity.EquipmentItemAt(3),
                    entity.EquipmentItemAt(4), entity.EquipmentItemAt(5), entity.EquipmentItemAt(6), entity.EquipmentItemAt(7)),
                AgeInTicks: GameTime + t,
                WalkAnimationPos: entity.Presentation.Walk.Position(t),
                WalkAnimationSpeed: entity.Presentation.Walk.Speed(t),
                Flap: entity.Presentation.Flap.Flap(t),
                FlapSpeed: entity.Presentation.Flap.Speed(t),
                IsOnGround: entity.Presentation.IsOnGround,
                IsInWater: entity.Presentation.IsInWater,
                IsPassenger: _vehicleOf.ContainsKey(entity.Id),
                IsLeashed: entity.Presentation.IsLeashed,
                IsVehicle: _passengers.ContainsKey(entity.Id),
                IsSitting: entity.Presentation.IsSitting,
                IsMoving: entity.Presentation.IsMoving,
                HeadYaw: Wrap360(LerpAngle(entity.Presentation.HeadYawO, entity.Presentation.HeadYaw, t)),
                SwingAnimation: entity.Presentation.Swing.Animation(t),
                IsAggressive: entity.Presentation.IsAggressive,
                MainHandItemId: entity.EquipmentItemAt(0),
                MainHandGlint: HasFoil(entity.EquipmentComponentsAt(0)),
                ArmPose: entity.Animation is IEntityArmPoseAnimationState armPose ? armPose.ArmPose : 0,
                IsUsingItem: entity.Presentation.IsUsingItem,
                // Reference LivingEntity.getTicksUsingItem(partialTicks): integer ticks plus the partial tick, so the
                // spear and crossbow animations advance smoothly instead of stepping once per tick.
                TicksUsingItem: ticksUsingItem,
                OffHandItemId: entity.EquipmentItemAt(1),
                OffHandGlint: HasFoil(entity.EquipmentComponentsAt(1)),
                MainHandChargedCrossbow: entity.EquipmentComponentsAt(0).HasChargedProjectiles,
                MainHandQuickChargeLevel: entity.EquipmentComponentsAt(0).QuickChargeLevel,
                IsLeftHanded: entity.Presentation.IsLeftHanded,
                Squish: entity.Presentation.OldSquish + ((entity.Presentation.Squish - entity.Presentation.OldSquish) * t),
                CubeSize: entity.Presentation.CubeSize,
                AttachFace: entity.Presentation.AttachFace,
                IsCrouching: entity.Presentation.IsCrouching,
                IsPouncing: entity.Presentation.IsPouncing,
                IsFaceplanted: entity.Presentation.IsFaceplanted,
                Health: entity.Presentation.Health,
                UnhappyCounter: entity.Presentation.UnhappyCounter,
                SneezeCounter: entity.Presentation.SneezeCounter,
                IsStanding: entity.Presentation.IsStanding,
                JumpCompletion: entity.Presentation.RabbitHop?.Completion(t) ?? 0f,
                IsJumping: entity.Presentation.RabbitHop?.IsJumping ?? false,
                StandScale: entity.Presentation.PolarStand?.Scale(t) ?? 0f,
                IsEating: entity.Presentation.IsEating,
                IsRolling: entity.Presentation.IsRolling,
                IsOnBack: entity.Presentation.IsOnBack,
                SitAmount: entity.Presentation.PandaPose?.SitAmount(t) ?? 0f,
                LieOnBackAmount: entity.Presentation.PandaPose?.LieOnBackAmount(t) ?? 0f,
                RollAmount: entity.Presentation.PandaPose?.RollAmount(t) ?? 0f,
                IsPartyParrot: entity.Presentation.IsPartyParrot,
                IsWorried: entity.Presentation.IsWorried,
                IsThundering: World?.IsThundering ?? false,
                WitherHeadYaw1: entity.Presentation.WitherHeads?.Yaw(0) ?? 0f,
                WitherHeadPitch1: entity.Presentation.WitherHeads?.Pitch(0) ?? 0f,
                WitherHeadYaw2: entity.Presentation.WitherHeads?.Yaw(1) ?? 0f,
                WitherHeadPitch2: entity.Presentation.WitherHeads?.Pitch(1) ?? 0f,
                IsIdleHeadTiltStarted: entity.Presentation.RabbitIdle?.IsStarted ?? false,
                IdleHeadTiltAgeTicks: (entity.Presentation.RabbitIdle?.IsStarted ?? false)
                    ? entity.Presentation.RabbitIdle!.AgeTicks((int)GameTime) + t
                    : 0f,
                Scale: entity.Presentation.Scale,
                MaxHealth: entity.MaxHealth,
                SpeedValue: speedValue,
                SwimAmount: entity.Presentation.SwimAmountAt(t),
                IsVisuallySwimming: visuallySwimming,
                IsFallFlying: fallFlying,
                ElytraRotX: entity.Presentation.Elytra.GetRotX(t),
                ElytraRotY: entity.Presentation.Elytra.GetRotY(t),
                ElytraRotZ: entity.Presentation.Elytra.GetRotZ(t),
                UseItemHand: entity.Presentation.UseItemHand,
                SwingHand: entity.Presentation.Swing.Hand,
                SwingType: entity.Presentation.Swing.AnimationType,
                IsSprinting: entity.Presentation.IsSprinting,
                LieDownAmount: entity.Presentation.LieDownAmountO + ((entity.Presentation.LieDownAmount - entity.Presentation.LieDownAmountO) * t),
                LieDownAmountTail: entity.Presentation.LieDownAmountTailO + ((entity.Presentation.LieDownAmountTail - entity.Presentation.LieDownAmountTailO) * t),
                RelaxStateOneAmount: entity.Presentation.RelaxStateOneAmountO + ((entity.Presentation.RelaxStateOneAmount - entity.Presentation.RelaxStateOneAmountO) * t),
                Crackiness: crackiness,
                TentacleAngle: entity.Presentation.Tentacle?.Angle(t) ?? 0f,
                IsResting: entity.Presentation.IsResting,
                Animation: entity.Animation,
                AnimationPartialTick: t,
                HasMainHandItem: entity.EquipmentItemAt(0) > 0,
                HasOffHandItem: entity.EquipmentItemAt(1) > 0,
                EquipmentAssetIds: entity.EquipmentAssetIds,
                EquipmentDyeColors: entity.EquipmentDyeColors,
                EquipmentGlintOverrides: entity.EquipmentGlintOverrides,
                EquipmentHasEnchantments: entity.EquipmentHasEnchantments,
                EquipmentHasBannerPatterns: entity.EquipmentHasBannerPatterns,
                EquipmentBannerPatternLayers: entity.EquipmentBannerPatternLayers,
                EquipmentTrimMaterials: entity.EquipmentTrimMaterials,
                EquipmentTrimPatterns: entity.EquipmentTrimPatterns,
                PlayerBodyTexture: playerSkin?.Body?.Path,
                PlayerCapeTexture: playerSkin?.Cape?.Path,
                PlayerElytraTexture: playerSkin?.Elytra?.Path,
                Slim: slim,
                Uuid: entity.Uuid,
                DragonFlightHistory: dragon ? entity.DragonFlightHistory : null,
                DragonIsLandingOrTakingOff: dragon && (entity.DragonPhase is 3 or 4),
                DragonIsSitting: dragon && (entity.DragonPhase is 5 or 6 or 7),
                DragonDistanceToEgg: dragon ? DragonDistanceToEgg(entity) : 0,
                IsHoldingPotion: entity.TypeId == WitchModelDefinition.WitchEntityTypeId && IsPotionItem(entity.EquipmentItemAt(0))));
        }

        return CollectionsMarshal.AsSpan(_renderStates);
    }

    private static bool IsPotionItem(int itemId) =>
        EntityModelResolver.Items?.ById(itemId) is { } item &&
        string.Equals(item.Id.Path, "potion", StringComparison.Ordinal);

    /// <summary>
    /// Squared distance from the dragon to the End podium egg at (0, heightmap, 0). The fight origin is currently
    /// assumed to be the world origin; the client does not synchronise the fight object.
    /// </summary>
    private double DragonDistanceToEgg(TrackedEntity entity)
    {
        var height = 0;
        if (World is { } world)
        {
            var maxY = ((world.MinSectionY + world.SectionCount) * 16) - 1;
            for (var y = maxY; y >= world.MinSectionY * 16; y--)
            {
                var state = world.GetBlockState(0, y, 0);
                if (VanillaBlockRegistry.Layout.BlockAt(state) is { } block &&
                    !string.Equals(block.Path, "air", StringComparison.Ordinal))
                {
                    height = y;
                    break;
                }
            }
        }

        var dx = entity.X - 0.5;
        var dy = entity.Y - (height + 0.5);
        var dz = entity.Z - 0.5;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static bool Relative(int relatives, int bit) => (relatives & (1 << bit)) != 0;

    // Synced entity data: index 6 is Entity.DATA_POSE (Pose serializer id 20). Values are the reference Pose ordinals.
    private const int PoseAccessorIndex = 6;
    private const int PoseSerializerId = 20;

    /// <summary>Routes synced pose and creature data to the per-entity animation state when it listens.</summary>
    private bool HandleAnimationData(TrackedEntity entity, ReadOnlySpan<EntityDataItem> items)
    {
        var poseListener = entity.Animation as IEntityPoseAnimationState;
        var dataListener = entity.Animation as IEntitySyncedDataAnimationState;

        var applied = false;
        foreach (var item in items)
        {
            if (item.Value == EntityDataItem.NotAnInteger)
            {
                continue;
            }

            if (item.Index == PoseAccessorIndex && item.SerializerId == PoseSerializerId)
            {
                entity.Presentation.Pose = item.Value;
                applied = true;
            }

            if (poseListener is not null && item.Index == PoseAccessorIndex && item.SerializerId == PoseSerializerId)
            {
                poseListener.OnPoseChanged(item.Value, (int)GameTime);
                applied = true;
            }

            if (dataListener is not null)
            {
                dataListener.OnSyncedData(item.Index, item.SerializerId, item.Value);
                applied = true;
            }

            // Bat (type 100) flags byte: index 16, serializer 0, bit 0 = resting.
            if (entity.TypeId == 100 && item.Index == 16 && item.SerializerId == 0)
            {
                entity.Presentation.IsResting = (item.Value & 1) != 0;
                applied = true;
            }

            // TamableAnimal flags byte (index 18, byte serializer): bit 0 = sitting (parrot/cat/ocelot/wolf).
            if (item.Index == 18 && item.SerializerId == 0 && entity.TypeId is 21 or 92 or 99 or 152)
            {
                entity.Presentation.IsSitting = (item.Value & 1) != 0;
                applied = true;
            }

            // Fox flags byte (index 18, byte serializer): sitting/pouncing/sleeping/faceplanted/crouching bits.
            if (item.Index == 18 && item.SerializerId == 0 && entity.TypeId == 55)
            {
                entity.Presentation.IsSitting = (item.Value & 1) != 0;
                entity.Presentation.IsCrouching = (item.Value & 4) != 0;
                entity.Presentation.IsPouncing = (item.Value & 16) != 0;
                entity.Presentation.IsSleeping = (item.Value & 32) != 0;
                entity.Presentation.IsFaceplanted = (item.Value & 64) != 0;
                applied = true;
            }

            // LivingEntity health (index 9, float serializer 3).
            if (item.Index == 9 && item.SerializerId == 3 && item.HasFloat)
            {
                entity.Presentation.Health = item.FloatValue;
                applied = true;
            }

            // Panda synced counters (unhappy index 17, sneeze index 18; int serializer 1).
            if (entity.TypeId == 97 && item.SerializerId == 1 && item.Index is 17 or 18)
            {
                if (item.Index == 17)
                {
                    entity.Presentation.UnhappyCounter = item.Value;
                }
                else
                {
                    entity.Presentation.SneezeCounter = item.Value;
                }

                applied = true;
            }

            // Panda EAT_COUNTER (index 19, int serializer 1).
            if (entity.TypeId == 97 && item.Index == 19 && item.SerializerId == 1)
            {
                entity.Presentation.IsEating = item.Value > 0;
                applied = true;
            }

            // Ender Dragon phase id (DATA_PHASE, index 15, int serializer 1).
            if (entity.TypeId == EnderDragonModelDefinition.EnderDragonEntityTypeId &&
                item.Index == 15 && item.SerializerId == 1)
            {
                entity.DragonPhase = item.Value;
                applied = true;
            }

            // Wither side-head alternative targets (indices 17/18, int serializer 1).
            if (entity.TypeId == 149 && item.SerializerId == 1 && item.Index is 17 or 18)
            {
                if (item.Index == 17)
                {
                    entity.Presentation.WitherTarget1 = item.Value;
                }
                else
                {
                    entity.Presentation.WitherTarget2 = item.Value;
                }

                applied = true;
            }

            // Panda main gene byte (index 20): WORRIED id 2 drives isScared under thunder.
            if (entity.TypeId == 97 && item.Index == 20 && item.SerializerId == 0)
            {
                entity.Presentation.IsWorried = item.Value == 2;
                applied = true;
            }

            // Panda flags byte (index 22, byte serializer): bit 4 rolling, bit 8 sitting, bit 16 on-back.
            if (entity.TypeId == 97 && item.Index == 22 && item.SerializerId == 0)
            {
                entity.Presentation.IsRolling = (item.Value & 4) != 0;
                entity.Presentation.IsSitting = (item.Value & 8) != 0;
                entity.Presentation.IsOnBack = (item.Value & 16) != 0;
                applied = true;
            }

            // Polar bear standing flag (index 17, boolean serializer 8).
            if (entity.TypeId == 105 && item.Index == 17 && item.SerializerId == 8)
            {
                entity.Presentation.IsStanding = item.Value != 0;
                applied = true;
            }
            // Mob flags byte (index 15, byte serializer): bit 2 = aggressive, bit 1 = left-handed.
            if (item.Index == 15 && item.SerializerId == 0)
            {
                entity.Presentation.IsNoAi = (item.Value & 1) != 0;
                entity.Presentation.IsAggressive = (item.Value & 4) != 0;
                entity.Presentation.IsLeftHanded = (item.Value & 2) != 0;
                applied = true;
            }

            // Shulker attach face: DATA_ATTACH_FACE_ID (index 16, direction serializer 12).
            if (entity.TypeId == 115 && item.Index == 16 && item.SerializerId == 12)
            {
                entity.Presentation.AttachFace = item.Value;
                applied = true;
            }

            // Cube-mob size: AbstractCubeMob.ID_SIZE is an INT accessor after the AgeableMob prefix (index 18, serializer 1).
            if (item.Index == 18 && item.SerializerId == 1 && (entity.TypeId == 120 || entity.TypeId == 81))
            {
                entity.Presentation.CubeSize = item.Value;
                applied = true;
            }

            // Living-entity flags byte (index 8, byte serializer): bit 0 = using item, bit 1 = off-hand used.
            if (item.Index == 8 && item.SerializerId == 0)
            {
                entity.Presentation.IsUsingItem = (item.Value & 1) != 0;
                entity.Presentation.UseItemHand = (item.Value & 2) != 0 ? 1 : 0;
                applied = true;
            }

            // Entity shared flags byte (index 0, byte serializer): bit 3 = sprinting, bit 7 = elytra fall flying.
            if (item.Index == 0 && item.SerializerId == 0)
            {
                entity.Presentation.IsSprinting = (item.Value & 8) != 0;
                entity.Presentation.IsFallFlying = (item.Value & 128) != 0;
                applied = true;
            }

            // Cat IS_LYING / RELAX_STATE_ONE (boolean serializers, indices 21 and 22).
            if (entity.TypeId == 21 && item.SerializerId == 8 && item.Index is 21 or 22)
            {
                if (item.Index == 21)
                {
                    entity.Presentation.IsLying = item.Value != 0;
                }
                else
                {
                    entity.Presentation.IsRelaxStateOne = item.Value != 0;
                }

                applied = true;
            }

            // AgeableMob baby flag (index 16, boolean serializer). Handled here so it works for every ageable type,
            // including those without an EntityVariantSpec (e.g. zombies).
            if (item.Index == 16 && item.SerializerId == 8 && AgeableTypes.Contains(entity.TypeId))
            {
                entity.IsBaby = item.Value != 0;
                applied = true;
            }
        }

        return applied;
    }

    private readonly record struct WalkAnimationProfile(float DistanceToSpeed, float MaxSpeed, float Factor, float PositionScale);

    /// <summary>
    /// Per-type walk animation mapping. The default mirrors <c>LivingEntity.updateWalkAnimation</c>
    /// (target = min(distance*4, 1), factor 0.4, baby position scale 3); the Creaking overrides it with
    /// min(distance*25, 3). Type ids are literals on purpose so this layer does not depend on the render model classes.
    /// </summary>
    private static class WalkProfile
    {
        private const int CreakingTypeId = 31;

        public static WalkAnimationProfile For(int typeId, bool isBaby)
        {
            if (typeId == CreakingTypeId)
            {
                return new WalkAnimationProfile(25f, 3f, 0.4f, 1f);
            }

            return new WalkAnimationProfile(4f, 1f, 0.4f, isBaby ? 3f : 1f);
        }
    }

    private static readonly int[] DyeRgb =
    [
        16383998, 16351261, 13061821, 3847130, 16701501, 8439583, 15961002, 4673362,
        10329495, 1481884, 8991416, 3949738, 8606770, 6192150, 11546150, 1908001
    ];

    /// <summary>Entity types whose synced data index 16 is the AgeableMob baby flag.</summary>
    private static readonly HashSet<int> AgeableTypes =
    [
        4, 7, 11, 19, 21, 26, 30, 36, 39, 59, 65, 67, 79, 89, 101, 102, 103, 111, 122,
        132, 141, 143, 145, 152, 153, 154, 156, 157, 158
    ];

    private static System.Numerics.Vector3 DyeTint(int dyeId)
    {
        var rgb = DyeRgb[Math.Clamp(dyeId, 0, 15)];
        return new System.Numerics.Vector3(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }

    /// <summary>Reference <c>Entity.setXRot</c> clamps pitch to [-90, 90].</summary>
    private static float ClampPitch(float pitch) => Math.Clamp(pitch, -90f, 90f);

    /// <summary>Normalises an angle into [0, 360).</summary>
    private static float Wrap360(float degrees)
    {
        var wrapped = degrees % 360f;
        return wrapped < 0f ? wrapped + 360f : wrapped;
    }

    /// <summary>Interpolates an angle along the shortest arc, so 350° to 10° passes through 0°.</summary>
    private static float LerpAngle(float from, float to, float t)
    {
        var delta = to - from;
        delta -= 360f * MathF.Floor((delta + 180f) / 360f);
        return from + (delta * t);
    }
}
