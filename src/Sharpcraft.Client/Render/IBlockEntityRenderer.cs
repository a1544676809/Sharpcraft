// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws the block entities of one type family. The reference has one of these per block entity type,
/// registered in a single table, and the dispatcher looks it up by type at render time.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BlockEntityRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BlockEntityRenderer.java")]
public interface IBlockEntityRenderer
{
    /// <summary>Whether this renderer draws a block entity of the given type.</summary>
    bool Handles(int typeId);

    /// <summary>Entities drawn by the last call to <see cref="Build"/>.</summary>
    int LastEntityCount { get; }

    /// <summary>Builds geometry for every entity this renderer handles, or null when there are none.</summary>
    DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas);
}
