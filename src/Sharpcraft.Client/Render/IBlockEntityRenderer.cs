// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>Reference render type family a block entity renderer submits into.</summary>
/// <remarks>
/// The reference picks a render type per renderer: <c>entitySolid</c> keeps back-face culling while
/// <c>entityCutout</c> disables it. The dispatcher groups by this value and the frame draws the two
/// index ranges with their matching pipelines.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.rendertype.RenderType", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/rendertype/RenderType.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "collapses entitySolid / entityCutout to the cull distinction block entity renderers need")]
public enum BlockEntityRenderMode
{
    /// <summary><c>entityCutout</c> semantics: alpha test, no back-face culling.</summary>
    NoCull,

    /// <summary><c>entitySolid</c> semantics: alpha test, back-face culling.</summary>
    Solid
}

/// <summary>
/// Draws the block entities of one type family. The reference has one of these per block entity type,
/// registered in a single table, and the dispatcher looks it up by type at render time.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BlockEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BlockEntityRenderer.java")]
public interface IBlockEntityRenderer
{
    /// <summary>Whether this renderer draws a block entity of the given type.</summary>
    bool Handles(int typeId);

    /// <summary>Render type family this renderer's geometry belongs to.</summary>
    BlockEntityRenderMode RenderMode => BlockEntityRenderMode.NoCull;

    /// <summary>Entities drawn by the last call to <see cref="Build"/>.</summary>
    int LastEntityCount { get; }

    /// <summary>Builds geometry for every entity this renderer handles, or null when there are none.</summary>
    DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas);

    /// <summary>
    /// Builds geometry with both the block-entity atlas and the optional shared item atlas. Renderers
    /// that draw item stacks override this overload; the default keeps block-only renderers unchanged.
    /// </summary>
    DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas, ItemAtlas? itemAtlas) =>
        Build(entities, atlas);
}
