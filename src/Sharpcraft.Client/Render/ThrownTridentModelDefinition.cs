// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Reference;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// ThrownTridentRenderer/TridentModel: the raw 32x32 trident model is submitted without the living-entity source-frame
/// flip, rotated by (yRot - 90, xRot + 90), and receives an entity-scale glint overlay when the projectile's synced
/// foil flag is set.
/// </summary>
[ReferenceClass("net.minecraft.client.renderer.entity.ThrownTridentRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/ThrownTridentRenderer.java", Kind = ReferenceKind.Adapter, Status = ReferenceStatus.Partial, Note = "base and entity-scale glint layers; no thrown-trident simulation")]
public static class ThrownTridentModelDefinition
{
    /// <summary><c>EntityTypes.TRIDENT</c> id from the 26.3 registry probe: 139.</summary>
    public const int EntityTypeId = 139;

    /// <summary>ThrownTrident ID_FOIL after Entity 0-7, AbstractArrow 8-10 and ID_LOYALTY 11.</summary>
    public const int FoilAccessorIndex = 12;

    public static readonly EntityModelDefinition Definition = Create();

    private static EntityModelDefinition Create()
    {
        var baseModel = SpecialItemModelDefinition.Trident;
        var layers = new[]
        {
            new EntityRenderLayerDefinition("cutout", EntityGeometryLayerKind.Cutout, 0, AllCubes: true),
            new EntityRenderLayerDefinition(
                "trident_glint",
                EntityGeometryLayerKind.Glint,
                5,
                Model: SpecialItemModelDefinition.Trident,
                TexturePath: ItemInHandMesher.GlintTexture,
                TextureSelector: state => state.IsFoil ? ItemInHandMesher.GlintTexture : null,
                VisibleWhen: state => state.IsFoil,
                AllCubes: true,
                GlintScale: GlintTextureTransform.EntityScale),
        };
        return new EntityModelDefinition("thrown_trident", baseModel.TextureWidth, baseModel.TextureHeight, baseModel.FaceTransforms, SpecialItemModelDefinition.TridentTexture, 1f, textureSelector: null, additionalTexturePaths: [SpecialItemModelDefinition.TridentTexture, ItemInHandMesher.GlintTexture], compiled: baseModel.Compiled, renderLayers: layers, baselinePx: 0f, rawSourceFrame: true, sourceFrameBaselinePx: 0f);
    }
}
