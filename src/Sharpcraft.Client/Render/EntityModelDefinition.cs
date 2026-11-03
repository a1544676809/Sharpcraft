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
        string? texturePath = null,
        float scale = 1f,
        string? emissiveTexturePath = null)
    {
        Name = name;
        TextureWidth = textureWidth;
        TextureHeight = textureHeight;
        Parts = parts;
        FaceTransforms = faceTransforms ?? new Dictionary<string, FaceUvTransform>();
        TexturePath = texturePath;
        Scale = scale;
        EmissiveTexturePath = emissiveTexturePath;
    }

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

            foreach (var part in Parts)
            {
                if (part.TexturePath is { Length: > 0 } partTexture)
                {
                    yield return partTexture;
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
        }
    }
}
