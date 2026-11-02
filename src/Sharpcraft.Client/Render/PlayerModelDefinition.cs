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
    bool Mirror = false);

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
        new("head", 0f, 24f, 8f, 8f, 8f, 0, 0),
        new("body", 0f, 12f, 8f, 12f, 4f, 16, 16),
        new("left_arm", -6f, 12f, 4f, 12f, 4f, 32, 48),
        new("right_arm", 6f, 12f, 4f, 12f, 4f, 40, 16),
        new("left_leg", -1.9f, 0f, 4f, 12f, 4f, 16, 48),
        new("right_leg", 1.9f, 0f, 4f, 12f, 4f, 0, 16)
    ];
}
