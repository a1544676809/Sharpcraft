// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Client.Render;

/// <summary>The chunk layer a block's geometry belongs to.</summary>
public enum BlockLayer
{
    /// <summary>Fully opaque, drawn with depth writes.</summary>
    Solid,

    /// <summary>Alpha tested holes, drawn without blending.</summary>
    Cutout,

    /// <summary>Blended and sorted, drawn last with depth writes off.</summary>
    Translucent
}

/// <summary>
/// Decides a block's chunk layer the way the reference does.
/// </summary>
/// <remarks>
/// The rule is not a per block name table: a material that is explicitly translucent goes to the translucent layer,
/// otherwise the texture's alpha content decides - partial alpha is translucent, binary holes are cutout, and only a fully
/// opaque texture is solid
/// (ref://mc-26.3-analysis/src/net/minecraft/client/resources/model/cuboid/FaceBakery.java computeMaterialTransparency,
/// ref://.../chunk/ChunkSectionLayer.java byTransparency).
/// Fluids and the glass family carry an explicit translucent material in the reference, which is what the curated name list
/// in <see cref="BlockRenderLayers"/> stands in for until model render types are parsed.
/// </remarks>
public static class BlockLayerRules
{
    /// <summary>Layer for a block state.</summary>
    /// <param name="stateId">Global block state id.</param>
    /// <param name="sprites">Sprite map, used for the alpha content.</param>
    /// <param name="layout">Layout used to name the state.</param>
    public static BlockLayer Of(int stateId, BlockSpriteMap? sprites, BlockStateLayout? layout)
    {
        if (layout?.BlockAt(stateId) is not { } block)
        {
            return BlockLayer.Solid;
        }

        if (BlockRenderLayers.IsTranslucent(block))
        {
            return BlockLayer.Translucent;
        }

        if (sprites is null)
        {
            // Without an atlas the alpha cannot be read, so fall back to the table that stands in for model render types.
            return BlockRenderLayers.IsCutout(block) ? BlockLayer.Cutout : BlockLayer.Solid;
        }

        // The reference decides from the texture, not from the block: oak leaves carry no render type at all, they are cut
        // out because their texture has zero alpha texels (84 of them, none partial).
        var transparency = sprites.TransparencyOf(block);

        if (transparency.HasTranslucent)
        {
            return BlockLayer.Translucent;
        }

        if (transparency.HasTransparent)
        {
            return BlockLayer.Cutout;
        }

        // Opaque texture but the name table says cut out: keep the declared behaviour for models whose texture shipped
        // fully opaque.
        return BlockRenderLayers.IsCutout(block) ? BlockLayer.Cutout : BlockLayer.Solid;
    }
}
