// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// The one registry of block entity renderers, mirroring the reference's table of
/// <c>BlockEntityType</c> to renderer factory. Adding a block entity means adding one entry here and
/// its textures to the sheet; nothing else in the frame path needs to know about the type.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BlockEntityRenderers", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BlockEntityRenderers.java")]
public static class BlockEntityRenderers
{
    /// <summary>Creates the renderers, one instance each so their buffers survive across frames.</summary>
    public static IBlockEntityRenderer[] Create() =>
    [
        new ChestBlockEntityRenderer(),
        new ShulkerBoxBlockEntityRenderer(),
        new BellBlockEntityRenderer(),
        new LecternRenderer(),
        new EnchantTableRenderer(),
        new ShelfRenderer(),
        new CampfireRenderer(),
        new BrushableRenderer(),
        new SkullRenderer(),
        new DecoratedPotBlockEntityRenderer(),
        new CopperGolemStatueBlockEntityRenderer()
    ];

    /// <summary>Whether any registered renderer draws this block entity type.</summary>
    public static bool Handles(int typeId, IReadOnlyList<IBlockEntityRenderer> renderers)
    {
        for (var i = 0; i < renderers.Count; i++)
        {
            if (renderers[i].Handles(typeId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adapts the chest mesher to the renderer contract.</summary>
    private sealed class ChestBlockEntityRenderer : IBlockEntityRenderer
    {
        private readonly ChestMesher _mesher = new();

        public bool Handles(int typeId) => VanillaBlockEntityTypes.IsChestLike(typeId);

        public int LastEntityCount => _mesher.LastEntityCount;

        public DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
            _mesher.Build(entities, atlas);
    }

    /// <summary>Adapts the shulker box mesher to the renderer contract.</summary>
    private sealed class ShulkerBoxBlockEntityRenderer : IBlockEntityRenderer
    {
        private readonly ShulkerBoxMesher _mesher = new();

        public bool Handles(int typeId) => VanillaBlockEntityTypes.IsShulkerBox(typeId);

        public int LastEntityCount => _mesher.LastEntityCount;

        public DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
            _mesher.Build(entities, atlas);
    }

    /// <summary>Adapts the bell mesher to the renderer contract.</summary>
    private sealed class BellBlockEntityRenderer : IBlockEntityRenderer
    {
        private readonly BellMesher _mesher = new();

        public bool Handles(int typeId) => typeId == VanillaBlockEntityTypes.Bell;

        public BlockEntityRenderMode RenderMode => BlockEntityRenderMode.Solid;

        public int LastEntityCount => _mesher.LastEntityCount;

        public DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
            _mesher.Build(entities, atlas);
    }

    /// <summary>Adapts the decorated pot mesher to the renderer contract.</summary>
    private sealed class DecoratedPotBlockEntityRenderer : IBlockEntityRenderer
    {
        private readonly DecoratedPotMesher _mesher = new();

        public bool Handles(int typeId) => typeId == VanillaBlockEntityTypes.DecoratedPot;

        public BlockEntityRenderMode RenderMode => BlockEntityRenderMode.Solid;

        public int LastEntityCount => _mesher.LastEntityCount;

        public DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
            _mesher.Build(entities, atlas);
    }

    /// <summary>Adapts the copper golem statue mesher to the renderer contract.</summary>
    private sealed class CopperGolemStatueBlockEntityRenderer : IBlockEntityRenderer
    {
        private readonly CopperGolemStatueMesher _mesher = new();

        public bool Handles(int typeId) => typeId == VanillaBlockEntityTypes.CopperGolemStatue;

        public BlockEntityRenderMode RenderMode => BlockEntityRenderMode.Solid;

        public int LastEntityCount => _mesher.LastEntityCount;

        public DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) =>
            _mesher.Build(entities, atlas);
    }
}
