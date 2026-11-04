// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Registries;

namespace Sharpcraft.Common.Network;

/// <summary>The known subset of an item stack's <c>DataComponentPatch</c> that the client consumes at this stage.</summary>
public readonly record struct ItemComponentInfo(
    bool Opaque = false,
    bool HasChargedProjectiles = false,
    int Damage = 0,
    int MaxDamage = 0,
    int RepairCost = 0,
    bool Unbreakable = false,
    int QuickChargeLevel = 0)
{
    /// <summary>True when some component payload could not be decoded and the rest of the packet was skipped.</summary>
    public bool HasUnknownComponents => Opaque;
}

/// <summary>
/// Decodes the item-stack component patch used by <c>set_equipment</c>. The wire format has no generic skip, so only a
/// curated set of component stream codecs is implemented; encountering any other positive component marks the stack
/// opaque and stops the caller rather than desynchronising the packet.
/// </summary>
internal static class ItemComponentPatchReader
{
    private const int MaxDepth = 4;

    private static readonly int MaxStackSize = DataComponentTypes.GetId("max_stack_size");
    private static readonly int MaxDamage = DataComponentTypes.GetId("max_damage");
    private static readonly int Damage = DataComponentTypes.GetId("damage");
    private static readonly int Unbreakable = DataComponentTypes.GetId("unbreakable");
    private static readonly int Enchantments = DataComponentTypes.GetId("enchantments");
    private static readonly int RepairCost = DataComponentTypes.GetId("repair_cost");
    private static readonly int ChargedProjectiles = DataComponentTypes.GetId("charged_projectiles");
    private static readonly int QuickCharge = EnchantmentTypes.GetId("quick_charge");
    private static readonly int CustomName = DataComponentTypes.GetId("custom_name");
    private static readonly int ItemName = DataComponentTypes.GetId("item_name");
    private static readonly int Lore = DataComponentTypes.GetId("lore");
    private static readonly int Rarity = DataComponentTypes.GetId("rarity");
    private static readonly int ItemModel = DataComponentTypes.GetId("item_model");
    private static readonly int GlintOverride = DataComponentTypes.GetId("enchantment_glint_override");
    private static readonly int AttributeModifiers = DataComponentTypes.GetId("attribute_modifiers");
    private static readonly int CustomModelData = DataComponentTypes.GetId("custom_model_data");
    private static readonly int TooltipDisplay = DataComponentTypes.GetId("tooltip_display");
    private static readonly int UseEffects = DataComponentTypes.GetId("use_effects");
    private static readonly int Enchantable = DataComponentTypes.GetId("enchantable");
    private static readonly int AttackAnimation = DataComponentTypes.GetId("attack_animation");
    private static readonly int InteractAnimation = DataComponentTypes.GetId("interact_animation");
    private static readonly int DamageType = DataComponentTypes.GetId("damage_type");
    private static readonly int Food = DataComponentTypes.GetId("food");
    private static readonly int StoredEnchantments = DataComponentTypes.GetId("stored_enchantments");
    private static readonly int Dye = DataComponentTypes.GetId("dye");
    private static readonly int DyedColor = DataComponentTypes.GetId("dyed_color");
    private static readonly int Trim = DataComponentTypes.GetId("trim");
    private static readonly int MinimumAttackCharge = DataComponentTypes.GetId("minimum_attack_charge");
    private static readonly int CreativeSlotLock = DataComponentTypes.GetId("creative_slot_lock");
    private static readonly int IntangibleProjectile = DataComponentTypes.GetId("intangible_projectile");
    private static readonly int DamageResistant = DataComponentTypes.GetId("damage_resistant");
    private static readonly int Glider = DataComponentTypes.GetId("glider");
    private static readonly int AdditionalTradeCost = DataComponentTypes.GetId("additional_trade_cost");
    private static readonly int PotionDurationScale = DataComponentTypes.GetId("potion_duration_scale");
    private static readonly int Instrument = DataComponentTypes.GetId("instrument");
    private static readonly int OminousBottleAmplifier = DataComponentTypes.GetId("ominous_bottle_amplifier");
    private static readonly int Waxed = DataComponentTypes.GetId("waxed");
    private static readonly int Tool = DataComponentTypes.GetId("tool");
    private static readonly int Weapon = DataComponentTypes.GetId("weapon");
    private static readonly int Equippable = DataComponentTypes.GetId("equippable");
    private static readonly int Repairable = DataComponentTypes.GetId("repairable");
    private static readonly int BundleContents = DataComponentTypes.GetId("bundle_contents");
    private static readonly int BlocksAttacks = DataComponentTypes.GetId("blocks_attacks");
    private static readonly int Consumable = DataComponentTypes.GetId("consumable");
    private static readonly int PotionContents = DataComponentTypes.GetId("potion_contents");
    private static readonly int Fireworks = DataComponentTypes.GetId("fireworks");
    private static readonly int FireworkExplosion = DataComponentTypes.GetId("firework_explosion");
    private static readonly int BannerPatterns = DataComponentTypes.GetId("banner_patterns");

    /// <summary>Reads one patch; returns false only when the stream is malformed (truncated).</summary>
    public static bool TryRead(ref PayloadReader reader, out ItemComponentInfo info, out bool hasUnknown)
    {
        info = default;
        hasUnknown = false;
        return TryReadPatch(ref reader, 0, ref info, ref hasUnknown);
    }

    private static bool TryReadPatch(ref PayloadReader reader, int depth, ref ItemComponentInfo info, ref bool hasUnknown)
    {
        if (reader.TryReadVarInt(out var positive) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var negative) != DecodeStatus.Success)
        {
            return false;
        }

        if (positive == 0 && negative == 0)
        {
            return true;
        }

        if (depth >= MaxDepth)
        {
            hasUnknown = true;
            info = info with { Opaque = true };
            return true;
        }

        for (var i = 0; i < positive; i++)
        {
            if (reader.TryReadVarInt(out var typeId) != DecodeStatus.Success)
            {
                return false;
            }

            if (typeId == ChargedProjectiles)
            {
                info = info with { HasChargedProjectiles = true };
            }

            if (!TryReadValue(ref reader, typeId, depth, ref info, ref hasUnknown))
            {
                return false;
            }

            if (hasUnknown)
            {
                return true;
            }
        }

        for (var i = 0; i < negative; i++)
        {
            if (reader.TryReadVarInt(out var typeId) != DecodeStatus.Success)
            {
                return false;
            }

            if (typeId == ChargedProjectiles)
            {
                info = info with { HasChargedProjectiles = false };
            }
        }

        return true;
    }

    private static bool TryReadValue(ref PayloadReader reader, int typeId, int depth, ref ItemComponentInfo info, ref bool hasUnknown)
    {
        if (typeId == MaxStackSize)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == MaxDamage)
        {
            if (reader.TryReadVarInt(out var value) != DecodeStatus.Success)
            {
                return false;
            }

            info = info with { MaxDamage = value };
            return true;
        }

        if (typeId == Damage)
        {
            if (reader.TryReadVarInt(out var value) != DecodeStatus.Success)
            {
                return false;
            }

            info = info with { Damage = value };
            return true;
        }

        if (typeId == Unbreakable)
        {
            // Unit component: no payload.
            info = info with { Unbreakable = true };
            return true;
        }

        if (typeId == RepairCost)
        {
            if (reader.TryReadVarInt(out var value) != DecodeStatus.Success)
            {
                return false;
            }

            info = info with { RepairCost = value };
            return true;
        }

        if (typeId == Enchantments)
        {
            return TrySkipEnchantments(ref reader, ref info);
        }

        if (typeId == ChargedProjectiles)
        {
            return TrySkipChargedProjectiles(ref reader, depth, ref hasUnknown);
        }

        if (typeId == CustomName || typeId == ItemName)
        {
            // Text component: transmitted as one anonymous NBT tag.
            return NbtTagReader.TrySkip(ref reader);
        }

        if (typeId == Lore)
        {
            if (reader.TryReadVarInt(out var lineCount) != DecodeStatus.Success || lineCount < 0)
            {
                return false;
            }

            for (var i = 0; i < lineCount; i++)
            {
                if (!NbtTagReader.TrySkip(ref reader))
                {
                    return false;
                }
            }

            return true;
        }

        if (typeId == Rarity)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == ItemModel)
        {
            return reader.TryReadString(out _) == DecodeStatus.Success;
        }

        if (typeId == GlintOverride)
        {
            return reader.TryReadBool(out _) == DecodeStatus.Success;
        }

        if (typeId == AttributeModifiers)
        {
            return TrySkipAttributeModifiers(ref reader);
        }

        if (typeId == CustomModelData)
        {
            return TrySkipCustomModelData(ref reader);
        }

        if (typeId == TooltipDisplay)
        {
            return TrySkipTooltipDisplay(ref reader);
        }

        if (typeId == UseEffects)
        {
            return reader.TryReadBool(out _) == DecodeStatus.Success &&
                   reader.TryReadBool(out _) == DecodeStatus.Success &&
                   reader.TryReadFloat(out _) == DecodeStatus.Success;
        }

        if (typeId == Enchantable)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == AttackAnimation || typeId == InteractAnimation)
        {
            // SwingAnimation: type id + duration.
            return reader.TryReadVarInt(out _) == DecodeStatus.Success &&
                   reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == DamageType)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == Food)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success &&
                   reader.TryReadFloat(out _) == DecodeStatus.Success &&
                   reader.TryReadBool(out _) == DecodeStatus.Success;
        }

        if (typeId == StoredEnchantments)
        {
            return TrySkipEnchantments(ref reader, ref info);
        }

        if (typeId == Dye)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == DyedColor)
        {
            return reader.TryReadInt(out _) == DecodeStatus.Success;
        }

        if (typeId == Trim)
        {
            // material + pattern, both registry holders.
            return reader.TryReadVarInt(out _) == DecodeStatus.Success &&
                   reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == MinimumAttackCharge)
        {
            return reader.TryReadFloat(out _) == DecodeStatus.Success;
        }

        if (typeId == CreativeSlotLock)
        {
            return reader.TryReadBool(out _) == DecodeStatus.Success;
        }

        if (typeId == IntangibleProjectile || typeId == Glider || typeId == Waxed)
        {
            // Unit components: no payload.
            return true;
        }

        if (typeId == DamageResistant)
        {
            return reader.TryReadString(out _) == DecodeStatus.Success;
        }

        if (typeId == AdditionalTradeCost || typeId == OminousBottleAmplifier || typeId == Instrument)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success;
        }

        if (typeId == PotionDurationScale)
        {
            return reader.TryReadFloat(out _) == DecodeStatus.Success;
        }

        if (typeId == Tool)
        {
            return TrySkipTool(ref reader, depth, ref hasUnknown);
        }

        if (typeId == Weapon)
        {
            return reader.TryReadVarInt(out _) == DecodeStatus.Success &&
                   reader.TryReadFloat(out _) == DecodeStatus.Success;
        }

        if (typeId == Equippable)
        {
            return TrySkipEquippable(ref reader);
        }

        if (typeId == Repairable)
        {
            return TrySkipHolderSet(ref reader);
        }

        if (typeId == BundleContents)
        {
            return TrySkipItemStackList(ref reader, depth, ref hasUnknown);
        }

        if (typeId == BlocksAttacks)
        {
            return TrySkipBlocksAttacks(ref reader);
        }

        if (typeId == Consumable)
        {
            return TrySkipConsumable(ref reader);
        }

        if (typeId == PotionContents)
        {
            return TrySkipPotionContents(ref reader);
        }

        if (typeId == Fireworks)
        {
            return TrySkipFireworks(ref reader);
        }

        if (typeId == FireworkExplosion)
        {
            return TrySkipFireworkExplosion(ref reader);
        }

        if (typeId == BannerPatterns)
        {
            return TrySkipBannerPatterns(ref reader);
        }

        // Unknown positive component: no generic skip exists, so the caller must stop after this slot.
        hasUnknown = true;
        info = info with { Opaque = true };
        return true;
    }

    private static bool TrySkipAttributeModifiers(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            // Entry: attribute holder id, modifier(id, amount, operation), slot group, display.
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success ||       // attribute
                reader.TryReadString(out _) != DecodeStatus.Success ||       // modifier id
                reader.TryReadDouble(out _) != DecodeStatus.Success ||       // amount
                reader.TryReadVarInt(out _) != DecodeStatus.Success ||       // operation
                reader.TryReadVarInt(out _) != DecodeStatus.Success)         // slot group
            {
                return false;
            }

            if (reader.TryReadVarInt(out var display) != DecodeStatus.Success)
            {
                return false;
            }

            if (display == 2 && !NbtTagReader.TrySkip(ref reader)) // override text
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipBlocksAttacks(ref PayloadReader reader)
    {
        if (reader.TryReadFloat(out _) != DecodeStatus.Success ||
            reader.TryReadFloat(out _) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var reductions) != DecodeStatus.Success ||
            reductions < 0)
        {
            return false;
        }

        for (var i = 0; i < reductions; i++)
        {
            if (reader.TryReadFloat(out _) != DecodeStatus.Success ||
                reader.TryReadBool(out var hasType) != DecodeStatus.Success ||
                (hasType && !TrySkipHolderSet(ref reader)) ||
                reader.TryReadFloat(out _) != DecodeStatus.Success ||
                reader.TryReadFloat(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        // ItemDamageFunction: threshold, base, factor.
        if (reader.TryReadFloat(out _) != DecodeStatus.Success ||
            reader.TryReadFloat(out _) != DecodeStatus.Success ||
            reader.TryReadFloat(out _) != DecodeStatus.Success)
        {
            return false;
        }

        if (reader.TryReadBool(out var hasBypassed) != DecodeStatus.Success ||
            (hasBypassed && !TrySkipHolderSet(ref reader)) ||
            reader.TryReadBool(out var hasBlockSound) != DecodeStatus.Success ||
            (hasBlockSound && reader.TryReadVarInt(out _) != DecodeStatus.Success) ||
            reader.TryReadBool(out var hasDisableSound) != DecodeStatus.Success ||
            (hasDisableSound && reader.TryReadVarInt(out _) != DecodeStatus.Success))
        {
            return false;
        }

        return true;
    }

    private static bool TrySkipConsumable(ref PayloadReader reader)
    {
        if (reader.TryReadFloat(out _) != DecodeStatus.Success ||
            reader.TryReadVarInt(out _) != DecodeStatus.Success || // ItemUseAnimation
            reader.TryReadVarInt(out _) != DecodeStatus.Success || // sound
            reader.TryReadBool(out _) != DecodeStatus.Success ||  // particles
            reader.TryReadVarInt(out var effects) != DecodeStatus.Success ||
            effects < 0)
        {
            return false;
        }

        for (var i = 0; i < effects; i++)
        {
            if (reader.TryReadVarInt(out var type) != DecodeStatus.Success)
            {
                return false;
            }

            switch (type)
            {
                case 0: // apply_effects: effect list + probability
                    if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
                    {
                        return false;
                    }

                    for (var e = 0; e < count; e++)
                    {
                        if (!TrySkipMobEffect(ref reader))
                        {
                            return false;
                        }
                    }

                    if (reader.TryReadFloat(out _) != DecodeStatus.Success)
                    {
                        return false;
                    }

                    break;
                case 1: // remove_effects: holder set
                    if (!TrySkipHolderSet(ref reader))
                    {
                        return false;
                    }

                    break;
                case 2: // clear_all_effects: no payload
                    break;
                case 3: // teleport_randomly: float + bool
                    if (reader.TryReadFloat(out _) != DecodeStatus.Success ||
                        reader.TryReadBool(out _) != DecodeStatus.Success)
                    {
                        return false;
                    }

                    break;
                case 4: // play_sound: sound id
                    if (reader.TryReadVarInt(out _) != DecodeStatus.Success)
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TrySkipMobEffect(ref PayloadReader reader)
    {
        // effect id, then Details (recursive hidden effect).
        return reader.TryReadVarInt(out _) == DecodeStatus.Success && TrySkipMobEffectDetails(ref reader, 0);
    }

    private static bool TrySkipMobEffectDetails(ref PayloadReader reader, int depth)
    {
        if (depth > MaxDepth ||
            reader.TryReadVarInt(out _) != DecodeStatus.Success || // amplifier
            reader.TryReadVarInt(out _) != DecodeStatus.Success || // duration
            reader.TryReadBool(out _) != DecodeStatus.Success ||  // ambient
            reader.TryReadBool(out _) != DecodeStatus.Success ||  // show particles
            reader.TryReadBool(out _) != DecodeStatus.Success)    // show icon
        {
            return false;
        }

        if (reader.TryReadBool(out var hasHidden) != DecodeStatus.Success)
        {
            return false;
        }

        return !hasHidden || TrySkipMobEffectDetails(ref reader, depth + 1);
    }

    private static bool TrySkipPotionContents(ref PayloadReader reader)
    {
        if (reader.TryReadBool(out var hasPotion) != DecodeStatus.Success ||
            (hasPotion && reader.TryReadVarInt(out _) != DecodeStatus.Success) ||
            reader.TryReadBool(out var hasColor) != DecodeStatus.Success ||
            (hasColor && reader.TryReadInt(out _) != DecodeStatus.Success) ||
            reader.TryReadVarInt(out var effects) != DecodeStatus.Success ||
            effects < 0)
        {
            return false;
        }

        for (var i = 0; i < effects; i++)
        {
            if (!TrySkipMobEffect(ref reader))
            {
                return false;
            }
        }

        if (reader.TryReadBool(out var hasName) != DecodeStatus.Success)
        {
            return false;
        }

        return !hasName || reader.TryReadString(out _) == DecodeStatus.Success;
    }

    private static bool TrySkipFireworks(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out var flight) != DecodeStatus.Success || flight < 0 ||
            reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!TrySkipFireworkExplosion(ref reader))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipFireworkExplosion(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out _) != DecodeStatus.Success) // shape
        {
            return false;
        }

        for (var list = 0; list < 2; list++) // colors, fade colors
        {
            if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (reader.TryReadInt(out _) != DecodeStatus.Success)
                {
                    return false;
                }
            }
        }

        return reader.TryReadBool(out _) == DecodeStatus.Success &&
               reader.TryReadBool(out _) == DecodeStatus.Success;
    }

    private static bool TrySkipBannerPatterns(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success || // pattern holder
                reader.TryReadVarInt(out _) != DecodeStatus.Success)   // dye color
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipHolderSet(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out var countMinusOne) != DecodeStatus.Success)
        {
            return false;
        }

        var count = countMinusOne - 1;
        if (count == -1)
        {
            // Named tag set: one identifier string.
            return reader.TryReadString(out _) == DecodeStatus.Success;
        }

        if (count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success) // registry holder id
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipTool(ref PayloadReader reader, int depth, ref bool hasUnknown)
    {
        if (reader.TryReadVarInt(out var ruleCount) != DecodeStatus.Success || ruleCount < 0)
        {
            return false;
        }

        for (var i = 0; i < ruleCount; i++)
        {
            if (!TrySkipHolderSet(ref reader)) // blocks
            {
                return false;
            }

            if (reader.TryReadBool(out var hasSpeed) != DecodeStatus.Success)
            {
                return false;
            }

            if (hasSpeed && reader.TryReadFloat(out _) != DecodeStatus.Success)
            {
                return false;
            }

            if (reader.TryReadBool(out var hasCorrect) != DecodeStatus.Success)
            {
                return false;
            }

            if (hasCorrect && reader.TryReadBool(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        return reader.TryReadFloat(out _) == DecodeStatus.Success &&
               reader.TryReadVarInt(out _) == DecodeStatus.Success &&
               reader.TryReadBool(out _) == DecodeStatus.Success;
    }

    private static bool TrySkipEquippable(ref PayloadReader reader)
    {
        // slot, equip sound
        if (reader.TryReadVarInt(out _) != DecodeStatus.Success ||
            reader.TryReadVarInt(out _) != DecodeStatus.Success)
        {
            return false;
        }

        // optional asset id (resource key = identifier string)
        if (reader.TryReadBool(out var hasAsset) != DecodeStatus.Success)
        {
            return false;
        }

        if (hasAsset && reader.TryReadString(out _) != DecodeStatus.Success)
        {
            return false;
        }

        // optional camera overlay (identifier)
        if (reader.TryReadBool(out var hasOverlay) != DecodeStatus.Success)
        {
            return false;
        }

        if (hasOverlay && reader.TryReadString(out _) != DecodeStatus.Success)
        {
            return false;
        }

        // optional allowed-entity holder set
        if (reader.TryReadBool(out var hasAllowed) != DecodeStatus.Success)
        {
            return false;
        }

        if (hasAllowed && !TrySkipHolderSet(ref reader))
        {
            return false;
        }

        // five booleans (dispensable, swappable, damageOnHurt, equipOnInteract, canBeSheared) then shearing sound.
        for (var i = 0; i < 5; i++)
        {
            if (reader.TryReadBool(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        return reader.TryReadVarInt(out _) == DecodeStatus.Success;
    }

    private static bool TrySkipItemStackList(ref PayloadReader reader, int depth, ref bool hasUnknown)
    {
        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!TrySkipItemStackTemplate(ref reader, depth, ref hasUnknown))
            {
                return false;
            }

            if (hasUnknown)
            {
                return true;
            }
        }

        return true;
    }

    private static bool TrySkipItemStackTemplate(ref PayloadReader reader, int depth, ref bool hasUnknown)
    {
        if (reader.TryReadVarInt(out _) != DecodeStatus.Success || // item id
            reader.TryReadVarInt(out _) != DecodeStatus.Success)   // count
        {
            return false;
        }

        var nested = default(ItemComponentInfo);
        return TryReadPatch(ref reader, depth + 1, ref nested, ref hasUnknown);
    }

    private static bool TrySkipTooltipDisplay(ref PayloadReader reader)
    {
        if (reader.TryReadBool(out _) != DecodeStatus.Success ||
            reader.TryReadVarInt(out var count) != DecodeStatus.Success ||
            count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipCustomModelData(ref PayloadReader reader)
    {
        // floats, flags, strings, colors: one VarInt-counted list each.
        if (reader.TryReadVarInt(out var floats) != DecodeStatus.Success || floats < 0)
        {
            return false;
        }

        for (var i = 0; i < floats; i++)
        {
            if (reader.TryReadFloat(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        if (reader.TryReadVarInt(out var flags) != DecodeStatus.Success || flags < 0)
        {
            return false;
        }

        for (var i = 0; i < flags; i++)
        {
            if (reader.TryReadBool(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        if (reader.TryReadVarInt(out var strings) != DecodeStatus.Success || strings < 0)
        {
            return false;
        }

        for (var i = 0; i < strings; i++)
        {
            if (reader.TryReadString(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        if (reader.TryReadVarInt(out var colors) != DecodeStatus.Success || colors < 0)
        {
            return false;
        }

        for (var i = 0; i < colors; i++)
        {
            if (reader.TryReadInt(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkipEnchantments(ref PayloadReader reader, ref ItemComponentInfo info)
    {
        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (reader.TryReadVarInt(out var enchantmentId) != DecodeStatus.Success ||
                reader.TryReadVarInt(out var level) != DecodeStatus.Success)
            {
                return false;
            }

            if (enchantmentId == QuickCharge)
            {
                info = info with { QuickChargeLevel = level };
            }
        }

        return true;
    }

    private static bool TrySkipChargedProjectiles(ref PayloadReader reader, int depth, ref bool hasUnknown)
    {
        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success || // item registry id
                reader.TryReadVarInt(out _) != DecodeStatus.Success)   // stack count
            {
                return false;
            }

            var nested = default(ItemComponentInfo);
            if (!TryReadPatch(ref reader, depth + 1, ref nested, ref hasUnknown))
            {
                return false;
            }

            if (hasUnknown)
            {
                return true;
            }
        }

        return true;
    }
}
