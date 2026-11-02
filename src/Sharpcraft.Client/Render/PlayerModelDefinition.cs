// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// One static model cuboid in model-pixel space. This is render data only: it never references an entity or world.
/// </summary>
/// <param name="Name">Part name for diagnostics and future overlays.</param>
/// <param name="CentreX">Horizontal centre offset in model pixels.</param>
/// <param name="Bottom">Bottom Y in model pixels, measured from the feet.</param>
/// <param name="Width">X size in pixels.</param>
/// <param name="Height">Y size in pixels.</param>
/// <param name="Depth">Z size in pixels.</param>
public readonly record struct EntityModelPart(
    string Name,
    float CentreX,
    float Bottom,
    float Width,
    float Height,
    float Depth,
    int TextureX = 0,
    int TextureY = 0,
    bool Mirror = false,
    IReadOnlyList<EntityModelLayer>? OverlayLayers = null,
    float CentreZ = 0f,
    float PivotX = 0f,
    float PivotY = 0f,
    float PivotZ = 0f,
    float RotationX = 0f,
    float RotationY = 0f,
    float RotationZ = 0f,
    string? TexturePath = null);

/// <summary>
/// An outer skin layer around one base cuboid, expressed as a uniform growth and its own texture offset.
/// </summary>
public readonly record struct EntityModelLayer(
    string Name,
    float Grow,
    int TextureX,
    int TextureY,
    bool Mirror = false,
    string? TexturePath = null);

/// <summary>
/// Static model definitions consumed by the entity mesher. The values follow the reference
/// `HumanoidModel.createMesh` player parts (docs/modules/entity-render.md).
/// </summary>
public static class PlayerModelDefinition
{
    /// <summary>Model height in pixels: head top is 32 pixels above the feet.</summary>
    public const float ModelPixels = 32f;

    /// <summary>Final per-face UV transforms calibrated in the sandbox (identical for every adult part).</summary>
    public static readonly IReadOnlyDictionary<string, FaceUvTransform> DefaultFaceTransforms = new Dictionary<string, FaceUvTransform>(StringComparer.Ordinal)
    {
        ["DOWN"] = new(180, false, false),
        ["UP"] = new(180, false, false),
        ["WEST"] = new(0, true, false),
        ["NORTH"] = new(0, true, false),
        ["EAST"] = new(0, true, false),
        ["SOUTH"] = new(0, true, false)
    };

    public static readonly EntityModelPart[] AdultParts =
    [
        FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
        FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16, overlays: [new("jacket", 0.25f, 16, 32)]),
        FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48, overlays: [new("left_sleeve", 0.25f, 48, 48)]),
        FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16, overlays: [new("right_sleeve", 0.25f, 40, 32)]),
        FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48, overlays: [new("left_pants", 0.25f, 0, 48)]),
        FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, overlays: [new("right_pants", 0.25f, 0, 32)])
    ];

    /// <summary>
    /// Builds an EntityModelPart from the reference's y-down pivot/cube values. The conversion is centralized here so
    /// individual model definitions do not need to hand-flip Bottom/CentreX.
    /// </summary>
    public static EntityModelPart FromVanilla(
        string name,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY,
        bool mirror = false,
        bool mirrorX = true,
        IReadOnlyList<EntityModelLayer>? overlays = null,
        float modelHeight = 24f,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        string? texturePath = null)
    {
        var sourceCentreX = pivotX + minX + (width * 0.5f);
        var centreX = MathF.Round(mirrorX ? -sourceCentreX : sourceCentreX, 4);
        var bottom = MathF.Round(modelHeight - (pivotY + minY + height), 4);
        var centreZ = MathF.Round(pivotZ + minZ + (depth * 0.5f), 4);
        return new EntityModelPart(
            name,
            centreX,
            bottom,
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            overlays,
            CentreZ: centreZ,
            PivotX: mirrorX ? -pivotX : pivotX,
            PivotY: modelHeight - pivotY,
            PivotZ: pivotZ,
            RotationX: rotationX,
            RotationY: rotationY,
            RotationZ: rotationZ,
            TexturePath: texturePath);
    }

    public static readonly EntityModelDefinition Definition =
        new("player", 64, 64, AdultParts, DefaultFaceTransforms, "entity/player/wide/steve");
}
