// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Resolves a render-only entity model definition from the registered entity type id. This is the C# equivalent of
/// EntityModelSet.bakeLayer: no world or simulation dependency, and missing entries stay null for placeholder fallback.
/// </summary>
public static class EntityModelResolver
{
    public static readonly EntityModelDefinition Placeholder = new(
        "placeholder",
        64,
        64,
        [new EntityModelPart("body", 0f, 0f, 14f, 14f, 14f, 0, 0)]);

    private static readonly Dictionary<int, EntityModelDefinition> Models = new()
    {
        [EntityPlaceholderMesher.PlayerEntityTypeId] = PlayerModelDefinition.Definition,
        [HumanoidMobModelDefinition.SkeletonEntityTypeId] = HumanoidMobModelDefinition.Skeleton,
        [HumanoidMobModelDefinition.ZombieEntityTypeId] = HumanoidMobModelDefinition.Zombie
    };

    public static IReadOnlyCollection<EntityModelDefinition> Definitions => Models.Values.Distinct().ToArray();

    public static EntityModelDefinition? TryResolve(int typeId) =>
        Models.TryGetValue(typeId, out var definition) ? definition : null;

    public static EntityModelDefinition Resolve(int typeId) =>
        TryResolve(typeId) ?? Placeholder;

    public static void Register(int typeId, EntityModelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Models[typeId] = definition;
    }
}
