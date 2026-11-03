// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>Clones a model's geometry as a collar overlay: same parts, collar texture, collar-colour tint, only while tamed.</summary>
public static class EntityModelLayerHelpers
{
    public static EntityModelPart[] WithCollar(IReadOnlyList<EntityModelPart> parts, string texturePath)
    {
        var result = new List<EntityModelPart>(parts.Count * 2);
        result.AddRange(parts);
        foreach (var part in parts)
        {
            result.Add(part with
            {
                Name = "collar_" + part.Name,
                TexturePath = texturePath,
                Grow = part.Grow + 0.1f,
                TintSelector = static state => state.CollarTint == default ? Vector3.One : state.CollarTint,
                VisibleWhen = static state => state.HasCollar
            });
        }

        return [.. result];
    }
}
