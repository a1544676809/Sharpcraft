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
        float scale = 1f)
    {
        Name = name;
        TextureWidth = textureWidth;
        TextureHeight = textureHeight;
        Parts = parts;
        FaceTransforms = faceTransforms ?? new Dictionary<string, FaceUvTransform>();
        TexturePath = texturePath;
        Scale = scale;
    }

    public string Name { get; }
    public int TextureWidth { get; }
    public int TextureHeight { get; }
    public IReadOnlyList<EntityModelPart> Parts { get; }
    public IReadOnlyDictionary<string, FaceUvTransform> FaceTransforms { get; }
    public string? TexturePath { get; }
    public float Scale { get; }

    public IEnumerable<string> TexturePaths
    {
        get
        {
            if (TexturePath is { Length: > 0 } main)
            {
                yield return main;
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
