// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.World;


/// <summary>One fixed item slot read from a block entity's <c>Items</c> list.</summary>
/// <param name="ItemId">Numeric item registry id, or -1 when the slot is empty.</param>
/// <param name="Count">Stack count; zero for an empty slot.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.ItemStack", "ref://mc-26.3-analysis/src/net/minecraft/world/item/ItemStack.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "item registry id and count from a ContainerHelper slot entry")]
public readonly record struct BlockEntityItemSlot(int ItemId, int Count)
{
    /// <summary>An empty slot; no item id and no count.</summary>
    public static readonly BlockEntityItemSlot Empty = new(-1, 0);

    /// <summary>Whether this slot has no drawable item.</summary>
    public bool IsEmpty => ItemId < 0 || Count <= 0;
}

/// <summary>
/// Fixed four-slot block entity inventory snapshot. Shelf uses the first three slots; campfire uses
/// all four. The struct is deliberately a value type so a render state does not allocate a list per
/// block entity.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.NonNullList", "ref://mc-26.3-analysis/src/net/minecraft/core/NonNullList.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "fixed four-slot snapshot of ContainerHelper Items entries")]
public readonly record struct BlockEntityItemSlots(
    BlockEntityItemSlot Slot0 = default,
    BlockEntityItemSlot Slot1 = default,
    BlockEntityItemSlot Slot2 = default,
    BlockEntityItemSlot Slot3 = default)
{
    /// <summary>Maximum number of slots in a block entity inventory snapshot.</summary>
    public const int Capacity = 4;

    /// <summary>Returns one slot by index 0..3, or an empty slot when out of range.</summary>
    public BlockEntityItemSlot Get(int index) => index switch
    {
        0 => Slot0,
        1 => Slot1,
        2 => Slot2,
        3 => Slot3,
        _ => BlockEntityItemSlot.Empty
    };

    /// <summary>Returns a copy with one slot replaced; out-of-range indices leave the value unchanged.</summary>
    public BlockEntityItemSlots With(int index, BlockEntityItemSlot slot) => index switch
    {
        0 => this with { Slot0 = slot },
        1 => this with { Slot1 = slot },
        2 => this with { Slot2 = slot },
        3 => this with { Slot3 = slot },
        _ => this
    };
}

/// <summary>
/// Brushable block entity hit direction, using the reference <c>Direction</c> 3D ordinal.
/// <see cref="None"/> represents an absent <c>hit_direction</c> tag.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.core.Direction", "ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Direction 3D ordinals 0..5 used by Direction.LEGACY_ID_CODEC; None for absent")]
public enum HitDirection : sbyte
{
    None = -1,
    Down = 0,
    Up = 1,
    North = 2,
    South = 3,
    West = 4,
    East = 5
}

/// <summary>Static brushable block data read from NBT.</summary>
/// <param name="Item">The single item stored in the block entity, or an empty slot.</param>
/// <param name="HitDirection">The latest brush hit direction, or <see cref="HitDirection.None"/> when absent.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.BrushableBlockEntity", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/BrushableBlockEntity.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "read-only item and hit_direction extracted from block entity NBT")]
public readonly record struct BrushableData(BlockEntityItemSlot Item, HitDirection HitDirection);

/// <param name="Messages">Four lines, top to bottom.</param>
/// <param name="Colour">Dye colour name, black when the tag omits it.</param>
/// <param name="Glowing">Whether the text glows.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.SignText", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignText.java")]
public readonly record struct SignTextData(IReadOnlyList<string> Messages, string Colour, bool Glowing);

/// <summary>Everything a sign renderer needs from the block entity.</summary>
/// <param name="Front">Front face.</param>
/// <param name="Back">Back face.</param>
/// <param name="Waxed">Whether the sign has been waxed.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.SignBlockEntity", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignBlockEntity.java")]
public readonly record struct SignData(SignTextData Front, SignTextData Back, bool Waxed);

/// <summary>
/// Typed reads of block entity NBT, one family at a time. The transport already parses the tag into
/// <see cref="BlockEntityRecord.Tag"/>; this is the part that knows which fields each family has
/// (docs/specs/world/block-entity-data.md).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.SignBlockEntity", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/SignBlockEntity.java")]
public static class BlockEntityData
{
    /// <summary>Lines whose component was richer than plain text and could not be read.</summary>
    public static int UnsupportedComponentCount { get; private set; }

    /// <summary>Items whose <c>id</c> or <c>count</c> could not be parsed or resolved.</summary>
    public static int UnsupportedItemCount { get; private set; }

    /// <summary>Item entries whose <c>Slot</c> was outside the fixed four-slot snapshot.</summary>
    public static int UnsupportedItemSlotCount { get; private set; }

    /// <summary>
    /// Reads a block entity's <c>Items</c> list. The source ItemStackWithSlot/ItemStack codecs default
    /// <c>Slot</c> to 0 and <c>count</c> to 1; a present count must be an integer in 1..99. Entries
    /// with an unknown item id, malformed fields or an out-of-range slot are skipped and counted;
    /// missing or empty slots stay empty. The method never throws for malformed NBT.
    /// </summary>
    /// <param name="record">Block entity record carrying the optional tag.</param>
    /// <param name="registry">Item registry used to turn the string id into the numeric id.</param>
    /// <param name="slots">Fixed slot snapshot.</param>
    /// <returns>Whether an <c>Items</c> list was present.</returns>
    public static bool TryReadItems(BlockEntityRecord record, ItemRegistry registry, out BlockEntityItemSlots slots)
    {
        ArgumentNullException.ThrowIfNull(registry);
        slots = default;

        if (record.Tag is not { } tag || tag.GetList("Items") is not { } list)
        {
            return false;
        }

        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is not NbtCompound entry)
            {
                UnsupportedItemCount++;
                continue;
            }

            var slotTag = entry.Get("Slot");
            if (!TryReadSmallInt(slotTag, fallback: 0, out var slot))
            {
                UnsupportedItemSlotCount++;
                continue;
            }

            if (slot is < 0 or >= BlockEntityItemSlots.Capacity)
            {
                UnsupportedItemSlotCount++;
                continue;
            }

            var id = entry.GetString("id");
            var countTag = entry.Get("count");
            if (!TryReadSmallInt(countTag, fallback: 1, out var count) || count is < 1 or > 99)
            {
                UnsupportedItemCount++;
                continue;
            }

            if (id is null)
            {
                UnsupportedItemCount++;
                continue;
            }

            var itemId = Sharpcraft.Common.Identifiers.Identifier.TryParse(id, out var identifier)
                ? registry.GetId(identifier)
                : -1;
            if (itemId < 0)
            {
                UnsupportedItemCount++;
                continue;
            }

            slots = slots.With(slot, new BlockEntityItemSlot(itemId, count));
        }

        return true;
    }

    /// <summary>
    /// Reads the brushable block's single <c>item</c> and <c>hit_direction</c>. The item uses the
    /// source ItemStack codec shape (<c>id</c> plus optional <c>count</c>, default one); a missing or
    /// malformed item becomes an empty slot. <c>hit_direction</c> uses Direction's legacy 3D ordinal
    /// 0..5 and is <see cref="HitDirection.None"/> when absent or malformed.
    /// </summary>
    /// <param name="record">Block entity record.</param>
    /// <param name="registry">Item registry used to resolve the item id.</param>
    /// <param name="data">Decoded brushable item and hit direction.</param>
    /// <returns>Whether the record carried an item or hit_direction tag.</returns>
    public static bool TryReadBrushable(BlockEntityRecord record, ItemRegistry registry, out BrushableData data)
    {
        ArgumentNullException.ThrowIfNull(registry);
        data = new BrushableData(BlockEntityItemSlot.Empty, HitDirection.None);

        if (record.TypeId != Sharpcraft.Common.World.VanillaBlockEntityTypes.BrushableBlock)
        {
            return false;
        }

        if (record.Tag is not { } tag)
        {
            return false;
        }

        var hasItem = TryReadItem(tag.GetCompound("item"), registry, out var item);
        var direction = ReadHitDirection(tag);
        data = new BrushableData(item, direction);
        return hasItem || tag.Contains("hit_direction");
    }

    /// <summary>Reads only the brushable block's single item slot.</summary>
    public static bool TryReadBrushableItem(BlockEntityRecord record, ItemRegistry registry, out BlockEntityItemSlot item)
    {
        ArgumentNullException.ThrowIfNull(registry);
        item = BlockEntityItemSlot.Empty;
        return record.TypeId == Sharpcraft.Common.World.VanillaBlockEntityTypes.BrushableBlock
            && record.Tag is { } tag
            && TryReadItem(tag.GetCompound("item"), registry, out item);
    }

    /// <summary>Reads only the brushable block's hit direction; absent or malformed gives None.</summary>
    public static bool TryReadHitDirection(BlockEntityRecord record, out HitDirection direction)
    {
        direction = record.TypeId == Sharpcraft.Common.World.VanillaBlockEntityTypes.BrushableBlock && record.Tag is { } tag
            ? ReadHitDirection(tag)
            : HitDirection.None;
        return direction != HitDirection.None;
    }

    private static HitDirection ReadHitDirection(NbtCompound tag) =>
        tag.Get("hit_direction") is NbtByte value && value.Value is >= 0 and <= 5
            ? (HitDirection)value.Value
            : HitDirection.None;

    private static bool TryReadItem(NbtCompound? item, ItemRegistry registry, out BlockEntityItemSlot slot)
    {
        slot = BlockEntityItemSlot.Empty;
        if (item is null)
        {
            return false;
        }

        var id = item.GetString("id");
        if (!TryReadSmallInt(item.Get("count"), fallback: 1, out var count) || count is < 1 or > 99 || id is null)
        {
            UnsupportedItemCount++;
            return false;
        }

        var itemId = Sharpcraft.Common.Identifiers.Identifier.TryParse(id, out var identifier)
            ? registry.GetId(identifier)
            : -1;
        if (itemId < 0)
        {
            UnsupportedItemCount++;
            return false;
        }

        slot = new BlockEntityItemSlot(itemId, count);
        return true;
    }

    /// <summary>Reads the shelf's <c>align_items_to_bottom</c> flag; false when absent or malformed.</summary>
    /// <param name="record">Block entity record.</param>
    /// <param name="alignItemsToBottom">Whether the shelf aligns items to its bottom.</param>
    /// <returns>Whether the tag carried a readable boolean.</returns>
    public static bool TryReadShelfAlign(BlockEntityRecord record, out bool alignItemsToBottom)
    {
        alignItemsToBottom = false;
        if (record.Tag is not { } tag || tag.Get("align_items_to_bottom") is not NbtByte value)
        {
            return false;
        }

        alignItemsToBottom = value.Value != 0;
        return true;
    }

    private static bool TryReadSmallInt(NbtTag? tag, int fallback, out int value)
    {
        if (tag is null)
        {
            value = fallback;
            return true;
        }

        switch (tag)
        {
            case NbtByte byteTag:
                value = byteTag.Value;
                return true;
            case NbtShort shortTag:
                value = shortTag.Value;
                return true;
            case NbtInt intTag:
                value = intTag.Value;
                return true;
            default:
                value = fallback;
                return false;
        }
    }

    /// <summary>Reads a sign's front and back text.</summary>
    /// <param name="record">Block entity as it arrived with the chunk.</param>
    /// <param name="data">The sign data when the record carries a front face.</param>
    /// <returns>Whether the record looks like a sign.</returns>
    public static bool TryReadSign(BlockEntityRecord record, out SignData data)
    {
        data = default;

        if (record.Tag is not { } tag || !TryReadSignText(tag.GetCompound("front_text"), out var front))
        {
            return false;
        }

        // A sign always carries both faces; a missing back face is read as blank rather than failing.
        TryReadSignText(tag.GetCompound("back_text"), out var back);
        data = new SignData(front, back, tag.GetBool("is_waxed"));
        return true;
    }

    private static bool TryReadSignText(NbtCompound? text, out SignTextData data)
    {
        data = default;

        if (text is null)
        {
            return false;
        }

        var messages = new string[4];
        Array.Fill(messages, string.Empty);
        if (text.GetList("messages") is { } list)
        {
            for (var i = 0; i < messages.Length && i < list.Items.Count; i++)
            {
                messages[i] = Flatten(list.Items[i]);
            }
        }

        data = new SignTextData(messages, text.GetString("color") ?? "black", text.GetBool("has_glowing_text"));
        return true;
    }
    /// <summary>Flattens one component to its plain characters through the shared component reader.</summary>
    private static string Flatten(NbtTag tag)
    {
        var runs = Sharpcraft.Common.Network.ComponentRuns.Flatten(tag);
        if (runs.Length == 0)
        {
            UnsupportedComponentCount++;
            return string.Empty;
        }

        if (runs.Length == 1)
        {
            return runs[0].Text;
        }

        var text = new System.Text.StringBuilder();
        foreach (var run in runs)
        {
            text.Append(run.Text);
        }

        return text.ToString();
    }
}