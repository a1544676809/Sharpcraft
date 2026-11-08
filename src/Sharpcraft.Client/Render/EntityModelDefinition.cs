// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

/// <summary>
/// A render-only entity model definition. This mirrors the role of the reference LayerDefinition/EntityModelSet:
/// named parts, texture dimensions and per-face UV defaults. It never references entity simulation.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.EntityModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/EntityModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class EntityModelDefinition
{
    public EntityModelDefinition(
        string name,
        int textureWidth,
        int textureHeight,
        IReadOnlyList<EntityModelPart> parts,
        IReadOnlyDictionary<string, FaceUvTransform>? faceTransforms = null,
        string? texturePath = null,
        float scale = 1f,
        string? emissiveTexturePath = null,
        Func<EntityRenderState, string?>? textureSelector = null,
        IReadOnlyList<string>? additionalTexturePaths = null,
        CompiledEntityModel? compiled = null,
        IReadOnlyList<EntityRenderLayerDefinition>? renderLayers = null,
        bool applyRootPose = false)
    {
        Name = name;
        TextureWidth = textureWidth;
        TextureHeight = textureHeight;
        Parts = parts;
        FaceTransforms = faceTransforms ?? new Dictionary<string, FaceUvTransform>();
        TexturePath = texturePath;
        Scale = scale;
        EmissiveTexturePath = emissiveTexturePath;
        TextureSelector = textureSelector;
        AdditionalTexturePaths = additionalTexturePaths ?? [];
        Compiled = compiled;
        RenderLayers = renderLayers ?? DefaultRenderLayers;
        ApplyRootPose = applyRootPose;
    }

    /// <summary>Default cutout/elytra/translucent/emissive layer order shared by entity models.</summary>
    public static readonly IReadOnlyList<EntityRenderLayerDefinition> DefaultRenderLayers =
    [
        new("cutout", EntityGeometryLayerKind.Cutout, 0),
        new("elytra", EntityGeometryLayerKind.Elytra, 10),
        new("translucent", EntityGeometryLayerKind.Translucent, 20),
        new("emissive", EntityGeometryLayerKind.Emissive, 30)
    ];

    public string Name { get; }
    public int TextureWidth { get; }
    public int TextureHeight { get; }
    public IReadOnlyList<EntityModelPart> Parts { get; }
    public IReadOnlyDictionary<string, FaceUvTransform> FaceTransforms { get; }
    public string? TexturePath { get; }
    public float Scale { get; }

    /// <summary>
    /// Eyes texture drawn over the same geometry with the reference eyes render type (emissive, depth &gt;=, no writes),
    /// or null when the model has no eyes layer (docs/decisions/0021).
    /// </summary>
    public string? EmissiveTexturePath { get; }

    /// <summary>
    /// Picks the texture for the current entity state (tame/angry, sleeping, nectar), or null to fall back to
    /// <see cref="TexturePath"/>.
    /// </summary>
    public Func<EntityRenderState, string?>? TextureSelector { get; }

    /// <summary>Textures the selector can return, loaded into the skin atlas up front.</summary>
    public IReadOnlyList<string> AdditionalTexturePaths { get; }

    /// <summary>Structural tree model when the definition has been migrated to the tree path; null uses legacy flat parts.</summary>
    public CompiledEntityModel? Compiled { get; }

    /// <summary>Explicit render-layer order for this model; defaults to the shared cutout/elytra/translucent/emissive order.</summary>
    public IReadOnlyList<EntityRenderLayerDefinition> RenderLayers { get; }

    /// <summary>Whether the compiled root group itself accepts a pose (EnderDragon root y/z/xRot).</summary>
    public bool ApplyRootPose { get; }

    /// <summary>
    /// Creates a definition from a declarative mesh: the legacy flat <see cref="Parts"/> remain available for compatibility,
    /// while <see cref="Compiled"/> is produced once by the structural bakery.
    /// </summary>
    public static EntityModelDefinition FromTree(
        string name,
        int textureWidth,
        int textureHeight,
        VanillaMeshDefinition mesh,
        IReadOnlyDictionary<string, FaceUvTransform>? faceTransforms = null,
        string? texturePath = null,
        float scale = 1f,
        string? emissiveTexturePath = null,
        Func<EntityRenderState, string?>? textureSelector = null,
        IReadOnlyList<string>? additionalTexturePaths = null,
        IReadOnlyList<EntityRenderLayerDefinition>? renderLayers = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var parts = VanillaMeshBake.Bake(mesh, 0, 0);
        var compiled = VanillaMeshBakery.Build(mesh, textureWidth, textureHeight, texturePath: texturePath);
        return new EntityModelDefinition(
            name,
            textureWidth,
            textureHeight,
            parts,
            faceTransforms,
            texturePath,
            scale,
            emissiveTexturePath,
            textureSelector,
            additionalTexturePaths,
            compiled,
            renderLayers);
    }

    public IEnumerable<string> TexturePaths
    {
        get
        {
            if (TexturePath is { Length: > 0 } main)
            {
                yield return main;
            }

            if (EmissiveTexturePath is { Length: > 0 } emissive)
            {
                yield return emissive;
            }

            foreach (var additional in AdditionalTexturePaths)
            {
                if (additional.Length > 0)
                {
                    yield return additional;
                }
            }

            foreach (var part in Parts)
            {
                if (part.TexturePath is { Length: > 0 } partTexture)
                {
                    yield return partTexture;
                }

                if (part.EmissiveTexturePath is { Length: > 0 } partEmissive)
                {
                    yield return partEmissive;
                }

                if (part.OverlayLayers is null)
                {
                    continue;
                }

                foreach (var layer in part.OverlayLayers)
                {
                    if (layer.TexturePath is { Length: > 0 } overlay)
                    {
                        yield return overlay;
                    }
                }
            }

            if (Compiled is { } compiled)
            {
                foreach (var cube in compiled.Cubes)
                {
                    if (cube.TexturePath is { Length: > 0 } cubeTexture)
                    {
                        yield return cubeTexture;
                    }

                    if (cube.EmissiveTexturePath is { Length: > 0 } cubeEmissive)
                    {
                        yield return cubeEmissive;
                    }

                    if (cube.Overlays is null)
                    {
                        continue;
                    }

                    foreach (var layer in cube.Overlays)
                    {
                        if (layer.TexturePath is { Length: > 0 } overlay)
                        {
                            yield return overlay;
                        }
                    }
                }
            }
        }
    }
}
