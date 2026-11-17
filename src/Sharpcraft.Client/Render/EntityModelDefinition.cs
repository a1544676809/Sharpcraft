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
        bool applyRootPose = false,
        float baselinePx = 24f,
        bool rawSourceFrame = false,
        float sourceFrameBaselinePx = 24f)
    {
        if (rawSourceFrame && baselinePx != 0f)
        {
            throw new ArgumentException("A raw source-frame model must not carry a model-height baseline.", nameof(baselinePx));
        }

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
        BaselinePx = baselinePx;
        RawSourceFrame = rawSourceFrame;
        SourceFrameBaselinePx = rawSourceFrame ? sourceFrameBaselinePx : 0f;
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
    /// Height of the model in pixels, used to convert the reference's y-down model space into the
    /// world space this renderer emits. 24 is the reference skeleton height; models that are already
    /// expressed in their own frame pass 0.
    /// </summary>
    public float BaselinePx { get; }

    /// <summary>
    /// Constant transform applied to this model's geometry inside its part frames. Used by layers whose geometry must
    /// pass through an item display transform and hand anchor (the reference applies them inside the pose stack), which
    /// the mesh DSL itself cannot express. Identity by default.
    /// </summary>
    public System.Numerics.Matrix4x4 LocalTransform { get; set; } = System.Numerics.Matrix4x4.Identity;

    /// <summary>
    /// Per-state override for <see cref="LocalTransform"/>. Used where the constant differs by render state — the shield
    /// submits a different item model (and therefore a different display transform) while its holder is using it.
    /// </summary>
    public Func<Sharpcraft.Client.World.EntityRenderState, System.Numerics.Matrix4x4>? LocalTransformSelector { get; set; }

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
    /// Whether this model's parts are in the raw reference y-down frame. The mesher then emits source
    /// face vertex order/UVs and applies source rotations without the legacy X/Y conjugation.
    /// </summary>
    public bool RawSourceFrame { get; }

    /// <summary>
    /// For raw source-frame models, the model height (in pixels) whose reflection the entity transform
    /// owns on behalf of the reference renderer. This is the y-down height the legacy bake used to fold
    /// into geometry (24 for the standard skeleton, 25 for Enderman, 0 for parts authored without a
    /// baseline). It is carried here only so the transform can place raw vertices at the same final
    /// world coordinates; the bake itself uses no baseline for raw models.
    /// </summary>
    public float SourceFrameBaselinePx { get; }

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
        IReadOnlyList<EntityRenderLayerDefinition>? renderLayers = null,
        float baselinePx = 24f,
        bool rawSourceFrame = false,
        bool applyRootPose = false,
        float sourceFrameBaselinePx = 24f)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        // Raw scaled models use the structurally-preserving compiled tree, whose NodeFrame owns the
        // PartPose scale. The legacy flattened bake has only the restricted static scaled-subtree path.
        var parts = rawSourceFrame && VanillaMeshBake.HasNonUnitScale(mesh)
            ? []
            : VanillaMeshBake.Bake(mesh, 0, 0, baselinePx, rawSourceFrame: rawSourceFrame);
        var compiled = VanillaMeshBakery.Build(mesh, textureWidth, textureHeight, baselinePx, texturePath: texturePath, rawSourceFrame: rawSourceFrame);
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
            renderLayers,
            baselinePx: baselinePx,
            rawSourceFrame: rawSourceFrame,
            sourceFrameBaselinePx: sourceFrameBaselinePx,
            applyRootPose: applyRootPose);
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
