// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reference <c>ItemStackRenderState.FoilType</c>: no foil, the plain item glint, or the compass/clock special glint
/// that samples a decal coordinate instead of the sprite UV.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.item.ItemStackRenderState.FoilType", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/item/ItemStackRenderState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public enum ItemFoilType : byte
{
    /// <summary>No foil: the item submits through its own render type.</summary>
    None = 0,

    /// <summary>Standard foil: the glint samples <c>TextureMat * UV0</c> (item scale 8.0).</summary>
    Standard = 1,

    /// <summary>Special foil: the glint samples <c>TextureMat * UV3</c>, where UV3 is a decal projection.</summary>
    Special = 2
}

/// <summary>
/// Source foils for an item stack: the component-driven foil decision plus the compass/clock "special animated
/// texture" test that selects <see cref="ItemFoilType.Special"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.item.CuboidItemModelWrapper", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/item/CuboidItemModelWrapper.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "hasFoil + hasSpecialAnimatedTexture decide the foil type; a null model id means the stack has no drawable model")]
public static class ItemFoil
{
    /// <summary>Item tag that marks the compasses whose glint is the special decal variant.</summary>
    public static readonly Identifier CompassesTag = new("minecraft", "compasses");

    /// <summary>The clock item; the reference compares against <c>Items.CLOCK</c> rather than a tag.</summary>
    public static readonly Identifier ClockItem = new("minecraft", "clock");

    /// <summary>
    /// Resolves one hand's foil type from its item components. <c>hasFoil</c> is the reference rule — an explicit
    /// glint override wins, otherwise enchantment presence decides — and only a foil stack can be special.
    /// </summary>
    public static ItemFoilType Resolve(in ItemComponentInfo components, int itemId)
    {
        var foil = components.GlintOverride ?? components.HasEnchantments;
        if (!foil)
        {
            return ItemFoilType.None;
        }

        return HasSpecialAnimatedTexture(itemId) ? ItemFoilType.Special : ItemFoilType.Standard;
    }

    /// <summary>Resolves the foil type for an item that already supplied its registry id.</summary>
    public static ItemFoilType ResolveItem(int itemId)
    {
        if (itemId <= 0 || EntityModelResolver.Items?.ById(itemId) is not { } item)
        {
            return ItemFoilType.None;
        }

        return HasSpecialAnimatedTexture(item.Id) ? ItemFoilType.Special : ItemFoilType.Standard;
    }

    /// <summary>Reference <c>CuboidItemModelWrapper.hasSpecialAnimatedTexture</c>: compasses or the clock.</summary>
    public static bool HasSpecialAnimatedTexture(int itemId) =>
        itemId > 0
        && EntityModelResolver.Items?.ById(itemId) is { } item
        && HasSpecialAnimatedTexture(item.Id);

    /// <summary>Reference <c>CuboidItemModelWrapper.hasSpecialAnimatedTexture</c> on a resolved item id.</summary>
    public static bool HasSpecialAnimatedTexture(Identifier item) =>
        item == ClockItem || EntityModelResolver.ItemTags.IsInTag(CompassesTag, item);
}
