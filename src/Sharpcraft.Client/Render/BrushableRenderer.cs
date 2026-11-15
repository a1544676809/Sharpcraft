// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the static item revealed by a brushable block. Item geometry and display transforms are
/// delegated to <see cref="BlockEntityItemMesher"/>; this renderer owns only the source placement
/// from BrushableBlockRenderer's submit path.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BrushableBlockRenderer.java</c>
/// (<c>submit</c> lines 52-67, <c>translations</c> lines 69-93). The source gating is
/// <c>dustProgress &gt; 0 &amp;&amp; hitDirection != null &amp;&amp; item non-empty</c>; item display
/// context is <c>ItemDisplayContext.FIXED</c>; transform order is translate(0,0.5,0), direction
/// translation, Y 75 degrees, east/west extra 90+11 degrees, then scale 0.5.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BrushableBlockRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BrushableBlockRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-BRUSHABLE-FX", "T-47", Note = "Brushable item is drawn from the static NBT snapshot; brushing particles, sounds and progress animation are not wired.")]
public sealed class BrushableRenderer : IBlockEntityRenderer
{
    private readonly BlockEntityItemMesher _items;
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Creates a brushable renderer using the shared item mesher.</summary>
    /// <param name="items">Optional item mesher injection for tests; production uses the shared resolver.</param>
    public BrushableRenderer(BlockEntityItemMesher? items = null)
    {
        _items = items ?? new BlockEntityItemMesher();
    }

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsBrushable(typeId);

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
            if (!Handles(entity.TypeId) || entity.Dusted == 0 || entity.HitDirection == HitDirection.None)
            {
                continue;
            }

            var item = entity.Items.Slot0;
            if (item.IsEmpty)
            {
                continue;
            }

            var translation = Translations(entity.HitDirection, entity.Dusted);
            var extraYaw = entity.HitDirection is HitDirection.East or HitDirection.West ? 101f : 11f;
            var placement = Matrix4x4.CreateScale(0.5f)
                * Matrix4x4.CreateRotationY((75f + extraYaw) * (MathF.PI / 180f))
                * Matrix4x4.CreateTranslation(translation.X, 0.5f + translation.Y, translation.Z)
                * Matrix4x4.CreateTranslation(entity.X, entity.Y, entity.Z);
            if (_items.AppendSlot(_geometry, item, itemAtlas, ItemDisplayContext.Fixed, placement) > 0)
            {
                LastEntityCount++;
            }
        }

        return _geometry;
    }

    /// <summary>
    /// Source <c>translations(Direction, completionState)</c>: base centre (0.5, 0, 0.5), then
    /// <c>completionState / 10 * 0.75</c> along the hit direction.
    /// </summary>
    public static Vector3 Translations(HitDirection direction, byte dusted)
    {
        var completion = dusted / 10.0f * 0.75f;
        return direction switch
        {
            HitDirection.East => new Vector3(0.73f + completion, 0f, 0.5f),
            HitDirection.West => new Vector3(0.25f - completion, 0f, 0.5f),
            HitDirection.Up => new Vector3(0.5f, 0.25f + completion, 0.5f),
            HitDirection.Down => new Vector3(0.5f, -0.23f - completion, 0.5f),
            HitDirection.North => new Vector3(0.5f, 0f, 0.25f - completion),
            HitDirection.South => new Vector3(0.5f, 0f, 0.73f + completion),
            _ => new Vector3(0.5f, 0f, 0.5f)
        };
    }
}
