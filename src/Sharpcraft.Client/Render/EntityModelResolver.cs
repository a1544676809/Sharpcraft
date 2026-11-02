// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Resolves a render-only entity model definition from the registered entity type id. This is the C# equivalent of
/// EntityModelSet.bakeLayer: no world or simulation dependency, and missing entries stay null for placeholder fallback.
/// </summary>
public static class EntityModelResolver
{
    private static readonly Dictionary<int, EntityModelDefinition> Models = new()
    {
        [EntityPlaceholderMesher.PlayerEntityTypeId] = PlayerModelDefinition.Definition
    };

    public static EntityModelDefinition? Resolve(int typeId) =>
        Models.TryGetValue(typeId, out var definition) ? definition : null;

    public static void Register(int typeId, EntityModelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Models[typeId] = definition;
    }
}
