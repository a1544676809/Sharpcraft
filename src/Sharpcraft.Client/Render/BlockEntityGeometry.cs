// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One concatenated block-entity batch plus the split between its two render-type groups.
/// </summary>
/// <param name="Geometry">All visible block entity geometry, solid parts first.</param>
/// <param name="SolidIndexCount">
/// Number of leading indices drawn with the solid (back-face culling) pipeline. The remaining
/// indices are drawn with the no-cull pipeline.
/// </param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.BlockEntityRenderDispatcher", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/BlockEntityRenderDispatcher.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "one batch carries a solid/no-cull index split instead of the reference's per-render-type node batches")]
public sealed record BlockEntityGeometry(DynamicGeometry Geometry, int SolidIndexCount);
