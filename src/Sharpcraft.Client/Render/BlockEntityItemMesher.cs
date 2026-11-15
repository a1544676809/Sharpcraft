// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Shared block-entity item drawing. It resolves an item id through the existing item-model path,
/// collects quads through <see cref="ItemEntityMesher.CollectQuads"/> and emits them through
/// <see cref="ItemEntityMesher.EmitQuads"/>; no item transform or UV logic is duplicated here.
/// </summary>
/// <remarks>
/// The block entity renderer already owns placement math (shelf slot offsets / campfire slots), so
/// this mesher only applies the item model's own placement and display transform on top of the
/// caller-supplied slot matrix.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.ShelfRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/ShelfRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class BlockEntityItemMesher
{
    /// <summary>Maximum fixed item slots supported by the block-entity snapshot.</summary>
    public const int MaxSlots = BlockEntityItemSlots.Capacity;

    private readonly ItemModelResolver? _models;
    private readonly List<ItemModelQuad> _quads = [];

    /// <summary>Creates a mesher. Passing null uses the resolver installed by the window host.</summary>
    /// <param name="models">Item model resolver, or null to use <see cref="EntityModelResolver.ItemModels"/>.</param>
    public BlockEntityItemMesher(ItemModelResolver? models = null)
    {
        _models = models;
    }

    private ItemModelResolver? Models => _models ?? EntityModelResolver.ItemModels;

    /// <summary>
    /// Extents of an item in one display context, including its item-model placement transforms.
    /// Returns null when the item cannot be resolved; the caller should then skip the slot.
    /// </summary>
    public ItemModelExtents? ExtentsFor(int itemId, ItemDisplayContext context)
    {
        if (itemId < 0 || Models is not { } models || EntityModelResolver.Items?.ById(itemId) is not { } item)
        {
            return null;
        }

        var placements = models.ResolveAll(item.Id.ToString());
        if (placements.Count == 0)
        {
            return null;
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var placement in placements)
        {
            var extents = ItemModelExtents.ForDisplay(placement.Model, context).Transformed(placement.Transform);
            min = Vector3.Min(min, extents.Min);
            max = Vector3.Max(max, extents.Max);
        }

        return new ItemModelExtents(min, max);
    }

    /// <summary>
    /// Adds every resolved sub-model of one item slot to <paramref name="geometry"/>. Returns the
    /// number of quads written; an empty slot or unresolved item leaves the geometry untouched.
    /// </summary>
    public int AppendSlot(
        DynamicGeometry geometry,
        BlockEntityItemSlot slot,
        ItemAtlas atlas,
        ItemDisplayContext context,
        in Matrix4x4 placement)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(atlas);

        if (slot.IsEmpty || Models is not { } models || EntityModelResolver.Items?.ById(slot.ItemId) is not { } item)
        {
            return 0;
        }

        var placements = models.ResolveAll(item.Id.ToString());
        if (placements.Count == 0)
        {
            return 0;
        }

        var grass = models.GrassColormap;
        var written = 0;
        foreach (var placementModel in placements)
        {
            _quads.Clear();
            ItemEntityMesher.CollectQuads(_quads, placementModel.Model, atlas);
            if (_quads.Count == 0)
            {
                continue;
            }

            var display = placementModel.Model.Display.TryGetValue(context, out var transform)
                ? transform.ToMatrix()
                : ItemDisplayTransform.None.ToMatrix();
            var world = placementModel.Transform * display * placement;
            var vertexFloats = geometry.VertexFloats;
            var indexCount = geometry.IndexCount;
            written += ItemEntityMesher.EmitQuads(
                geometry,
                _quads,
                world,
                atlas,
                placementModel.Model,
                grass,
                ref vertexFloats,
                ref indexCount);
            geometry.VertexFloats = vertexFloats;
            geometry.IndexCount = indexCount;
        }

        return written;
    }

    /// <summary>Texture paths one slot needs for the item atlas pre-pass.</summary>
    public static IEnumerable<string> ResolveTextures(int itemId)
    {
        if (itemId < 0 || EntityModelResolver.ItemModels is not { } models || EntityModelResolver.Items?.ById(itemId) is not { } item)
        {
            yield break;
        }

        foreach (var placement in models.ResolveAll(item.Id.ToString()))
        {
            foreach (var texture in ItemEntityMesher.ResolutionTextures(placement.Model))
            {
                yield return texture;
            }
        }
    }
}
