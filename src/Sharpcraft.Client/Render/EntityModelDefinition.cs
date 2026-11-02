// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// A render-only entity model definition. This mirrors the role of the reference LayerDefinition/EntityModelSet:
/// named parts, texture dimensions and per-face UV defaults. It never references entity simulation.
/// </summary>
public sealed class EntityModelDefinition
{
    public EntityModelDefinition(
        string name,
        int textureWidth,
        int textureHeight,
        IReadOnlyList<EntityModelPart> parts,
        IReadOnlyDictionary<string, FaceUvTransform>? faceTransforms = null,
        string? texturePath = null)
    {
        Name = name;
        TextureWidth = textureWidth;
        TextureHeight = textureHeight;
        Parts = parts;
        FaceTransforms = faceTransforms ?? new Dictionary<string, FaceUvTransform>();
        TexturePath = texturePath;
    }

    public string Name { get; }
    public int TextureWidth { get; }
    public int TextureHeight { get; }
    public IReadOnlyList<EntityModelPart> Parts { get; }
    public IReadOnlyDictionary<string, FaceUvTransform> FaceTransforms { get; }
    public string? TexturePath { get; }
}
