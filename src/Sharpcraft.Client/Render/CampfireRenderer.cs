// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the four food item slots of a campfire. Item geometry comes from
/// <see cref="BlockEntityItemMesher"/>; this renderer owns only the source slot placement.
/// </summary>
/// <remarks>
/// Source: <c>ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CampfireRenderer.java</c>
/// (<c>extractRenderState</c> lines 34-51, <c>submit</c> lines 53-72). Items use
/// <c>ItemDisplayContext.FIXED</c>, centre <c>(0.5, 0.44921875, 0.5)</c>, slot direction
/// <c>Direction.from2DDataValue((slot + facing.get2DDataValue()) % 4)</c>, Y angle
/// <c>-direction.toYRot()</c>, X 90 degrees, slot translation <c>(-0.3125, -0.3125, 0)</c> and scale
/// 0.375. Flame/spark rendering is outside this slice.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.CampfireRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/CampfireRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-BE-CAMPFIRE-FLAME", "T-47", Note = "Campfire items are drawn from the fixed NBT slot snapshot; fire, sparks and cooking/flame animation are not wired.")]
public sealed class CampfireRenderer : IBlockEntityRenderer
{
    private readonly BlockEntityItemMesher _items;
    private readonly DynamicGeometry _geometry = new();

    /// <summary>Creates a campfire renderer using the shared item mesher.</summary>
    /// <param name="items">Optional item mesher injection for tests; production uses the shared resolver.</param>
    public CampfireRenderer(BlockEntityItemMesher? items = null)
    {
        _items = items ?? new BlockEntityItemMesher();
    }

    /// <inheritdoc />
    public bool Handles(int typeId) => VanillaBlockEntityTypes.IsCampfire(typeId);

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

            var facing2D = Facing2D(entity.Facing);
            var drawn = 0;
            for (var slot = 0; slot < 4; slot++)
            {
                var item = entity.Items.Get(slot);
                if (item.IsEmpty)
                {
                    continue;
                }

                var direction2D = (slot + facing2D) % 4;
                var angle = -direction2D * 90f;
                var placement = Matrix4x4.CreateScale(0.375f)
                    * Matrix4x4.CreateTranslation(-0.3125f, -0.3125f, 0f)
                    * Matrix4x4.CreateRotationX(90f * (MathF.PI / 180f))
                    * Matrix4x4.CreateRotationY(angle * (MathF.PI / 180f))
                    * Matrix4x4.CreateTranslation(entity.X + 0.5f, entity.Y + 0.44921875f, entity.Z + 0.5f);
                drawn += _items.AppendSlot(_geometry, item, itemAtlas, ItemDisplayContext.Fixed, placement);
            }

            if (drawn > 0)
            {
                LastEntityCount++;
            }
        }

        return _geometry;
    }

    /// <summary>
    /// <c>Direction.get2DDataValue</c> for the horizontal directions: west 0, east 1, north 2,
    /// south 3. Non-horizontal falls back to north's 2.
    /// </summary>
    public static int Facing2D(byte facing) => facing switch
    {
        4 => 0, // west
        5 => 1, // east
        2 => 2, // north
        3 => 3, // south
        _ => 2
    };
}
