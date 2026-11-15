// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the three item slots of a shelf. The item geometry is emitted by
/// <see cref="BlockEntityItemMesher"/> through the existing item-model pipeline; this renderer owns
/// only the shelf slot placement from <c>ShelfRenderer.java</c>.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ShelfRenderer.java</c>
/// (<c>extractRenderState</c> lines 34-57, <c>submit</c> lines 59-93). Slot offset is
/// <c>(slot - 1) * 0.3125</c>, centre is <c>(0.5, 0.5, 0.5)</c>, scale is 0.25 and
/// <c>align_items_to_bottom</c> subtracts 0.25 from Y and aligns to the item's bottom.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.ShelfRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ShelfRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-SHELF-ITEMS", "T-47", Note = "Shelf item stacks are drawn from the fixed NBT slot snapshot; live container updates and shelf movement animation are not wired.")]
public sealed class ShelfRenderer : IBlockEntityRenderer
{
    private readonly BlockEntityItemMesher _items;
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Creates a shelf renderer using the shared item mesher.</summary>
    /// <param name="items">Optional item mesher injection for tests; production uses the shared resolver.</param>
    public ShelfRenderer(BlockEntityItemMesher? items = null)
    {
        _items = items ?? new BlockEntityItemMesher();
    }

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsShelf(typeId);

    /// <inheritdoc />
    public int LastEntityCount { get; private set; }

    /// <inheritdoc />
    public DynamicGeometry Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
        Build(entities, atlas, itemAtlas: null);

    /// <inheritdoc />
    public DynamicGeometry Build(
        ReadOnlySpan<BlockEntityRenderState> entities,
        ItemAtlas atlas,
        ItemAtlas? itemAtlas)
    {
        _geometry.Clear();
        LastEntityCount = 0;
        if (itemAtlas is null)
        {
            return _geometry;
        }

        for (var index = 0; index < entities.Length; index++)
        {
            var entity = entities[index];
            if (!Handles(entity.TypeId))
            {
                continue;
            }

            var drawn = 0;
            for (var slot = 0; slot < 3; slot++)
            {
                var item = entity.Items.Get(slot);
                if (item.IsEmpty)
                {
                    continue;
                }

                var extents = _items.ExtentsFor(item.ItemId, ItemDisplayContext.OnShelf);
                if (extents is null)
                {
                    continue;
                }

                var offsetY = -extents.Value.Min.Y;
                if (!entity.ShelfAlignItemsToBottom)
                {
                    offsetY += -(extents.Value.Max.Y - extents.Value.Min.Y) * 0.5f;
                }

                var placement = Matrix4x4.CreateTranslation(0f, offsetY, 0f)
                    * Matrix4x4.CreateScale(0.25f)
                    * Matrix4x4.CreateTranslation((slot - 1) * 0.3125f, entity.ShelfAlignItemsToBottom ? -0.25f : 0f, -0.25f)
                    * Matrix4x4.CreateRotationY(FacingYRot(entity.Facing) * (MathF.PI / 180f))
                    * Matrix4x4.CreateTranslation(entity.X + 0.5f, entity.Y + 0.5f, entity.Z + 0.5f);
                drawn += _items.AppendSlot(_geometry, item, itemAtlas, ItemDisplayContext.OnShelf, placement);
            }

            if (drawn > 0)
            {
                LastEntityCount++;
            }
        }

        return _geometry;
    }

    /// <summary>
    /// Source <c>ShelfRenderer.submit</c>: horizontal facing uses <c>-facing.toYRot()</c>; a
    /// non-horizontal fallback is 180 degrees. Direction ordinal 2..5 maps north/south/west/east.
    /// </summary>
    public static float FacingYRot(byte facing) => facing switch
    {
        2 => -180f, // north
        3 => 0f,    // south
        4 => -90f,  // west
        5 => -270f, // east
        _ => 180f
    };
}
