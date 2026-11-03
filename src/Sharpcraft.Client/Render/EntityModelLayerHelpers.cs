// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>Clones a model's geometry as a collar overlay: same parts, collar texture, collar-colour tint, only while tamed.</summary>
public static class EntityModelLayerHelpers
{
    public static EntityModelPart[] WithCollar(IReadOnlyList<EntityModelPart> parts, string texturePath) =>
        WithCopy(parts, texturePath, static state => state.HasCollar, 0.1f,
            static state => state.CollarTint == default ? Vector3.One : state.CollarTint);

    public static EntityModelPart[] WithCopy(IReadOnlyList<EntityModelPart> parts, string texturePath, Func<EntityRenderState, bool> visible, float inflate = 0.05f, Func<EntityRenderState, Vector3>? tint = null)
    {
        var result = new List<EntityModelPart>(parts.Count * 2);
        result.AddRange(parts);
        foreach (var part in parts)
        {
            result.Add(part with
            {
                Name = "copylayer_" + part.Name,
                TexturePath = texturePath,
                Grow = part.Grow + inflate,
                TintSelector = tint,
                VisibleWhen = visible
            });
        }

        return [.. result];
    }

    private static bool static_hasCollar(EntityRenderState state) => state.HasCollar;

    public static EntityModelPart[] WithHorseMarkings(IReadOnlyList<EntityModelPart> parts, string texturePrefix, bool baby)
    {
        (int Id, string Key)[] markings = [(1, "white"), (2, "whitefield"), (3, "whitedots"), (4, "blackdots")];
        var result = new List<EntityModelPart>(parts.Count * (markings.Length + 1));
        result.AddRange(parts);
        foreach (var (id, key) in markings)
        {
            var path = texturePrefix + key + (baby ? "_baby" : string.Empty);
            foreach (var part in parts)
            {
                result.Add(part with
                {
                    Name = "marking_" + key + "_" + part.Name,
                    TexturePath = path,
                    Grow = part.Grow + 0.05f,
                    VisibleWhen = state => state.Markings == id
                });
            }
        }

        return [.. result];
    }
}