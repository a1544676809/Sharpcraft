// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One explicitly ordered entity render layer. A definition describes the layer's stable name, geometry kind and
/// submit order; the mesher turns the union of the batch's definitions into draw layers sorted by <see cref="Order"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.RenderLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/RenderLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Explicit per-layer name/kind/order before independent layer models are wired")]
public sealed record EntityRenderLayerDefinition(
    string Name,
    EntityGeometryLayerKind Kind,
    int Order,
    EntityModelDefinition? Model = null,
    string? TexturePath = null,
    Func<Sharpcraft.Client.World.EntityRenderState, string?>? TextureSelector = null,
    Func<Sharpcraft.Client.World.EntityRenderState, bool>? VisibleWhen = null,
    bool AllCubes = false);
