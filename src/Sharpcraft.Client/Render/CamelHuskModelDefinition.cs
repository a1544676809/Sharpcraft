// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CamelHuskRenderer uses <c>ModelLayers.CAMEL</c> (the adult camel mesh) and only swaps the texture to
/// <c>entity/camel/camel_husk</c>; its saddle layer is <c>ModelLayers.CAMEL_HUSK_SADDLE</c>, registered from the same
/// <c>CamelSaddleModel.createSaddleLayer()</c> as the camel saddle. The compiled camel mesh is shared, not re-baked.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.camel.AdultCamelModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/camel/AdultCamelModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Camel husk reuses the adult camel mesh; texture entity/camel/camel_husk; saddle layer CAMEL_HUSK_SADDLE")]
public static class CamelHuskModelDefinition
{
    public const int CamelHuskEntityTypeId = 20;

    public static readonly EntityModelDefinition Definition = new(
        "camel_husk",
        128,
        128,
        faceTransforms: null,
        texturePath: "entity/camel/camel_husk",
        compiled: CamelModelDefinition.Definition.Compiled,
        baselinePx: 0f,
        rawSourceFrame: true);
}