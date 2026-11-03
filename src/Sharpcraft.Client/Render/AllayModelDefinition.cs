// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>AllayModel: small floating body with two arms and two flat wings; the whole model is translucent.</summary>
public static class AllayModelDefinition
{
    public const int AllayEntityTypeId = 2;

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("root", 0f, 23.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head", "root", 0f, -3.99f, 0f, -2.5f, -5f, -2.5f, 5f, 5f, 5f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0f, -4f, 0f, -1.5f, 0f, -1f, 3f, 4f, 2f, 0, 10),
            PlayerModelDefinition.FromVanillaChild("body_inner", "body", 0f, 0f, 0f, -1.5f, 0f, -1f, 3f, 5f, 2f, 0, 16, grow: -0.2f),
            PlayerModelDefinition.FromVanillaChild("right_arm", "body", -1.75f, 0.5f, 0f, -0.75f, -0.5f, -1f, 1f, 4f, 2f, 23, 0, grow: -0.01f),
            PlayerModelDefinition.FromVanillaChild("left_arm", "body", 1.75f, 0.5f, 0f, -0.25f, -0.5f, -1f, 1f, 4f, 2f, 23, 6, grow: -0.01f),
            PlayerModelDefinition.FromVanillaChild("right_wing", "body", -0.5f, 0f, 0.6f, 0f, 1f, 0f, 0f, 5f, 8f, 16, 14),
            PlayerModelDefinition.FromVanillaChild("left_wing", "body", 0.5f, 0f, 0.6f, 0f, 1f, 0f, 0f, 5f, 8f, 16, 14)
        };

        for (var i = 0; i < parts.Count; i++)
        {
            parts[i] = parts[i] with { Kind = EntityGeometryLayerKind.Translucent };
        }

        return [.. parts];
    }

    public static readonly EntityModelDefinition Definition = new(
        "allay",
        32,
        32,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/allay/allay");
}
