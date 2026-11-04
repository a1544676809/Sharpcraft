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

        // Unknown positive component: no generic skip exists, so the caller must stop after this slot.
        hasUnknown = true;
        info = info with { Opaque = true };
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
