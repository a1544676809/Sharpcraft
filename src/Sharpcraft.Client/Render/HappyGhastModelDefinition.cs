// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// HappyGhastModel (adult): one 16x16x16 body with nine tentacles hanging from its underside, scaled 4x.
/// Bind pose only; harness/ropes layers and baby model are not ported.
/// </summary>
public static class HappyGhastModelDefinition
{
    public const int HappyGhastEntityTypeId = 59;

    private static readonly (float X, float Y, float Z, float Length)[] Tentacles =
    [
        (-3.75f, 7f, -5f, 5f),
        (1.25f, 7f, -5f, 7f),
        (6.25f, 7f, -5f, 4f),
        (-6.25f, 7f, 0f, 5f),
        (-1.25f, 7f, 0f, 5f),
        (3.75f, 7f, 0f, 7f),
        (-3.75f, 7f, 5f, 8f),
        (1.25f, 7f, 5f, 8f),
        (6.25f, 7f, 5f, 5f)
    ];

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0)
        };

        for (var i = 0; i < Tentacles.Length; i++)
        {
            var (x, y, z, length) = Tentacles[i];
            parts.Add(PlayerModelDefinition.FromVanillaChild(
                "tentacle" + i,
                "body",
                x,
                y,
                z,
                -1f,
                0f,
                -1f,
                2f,
                length,
                2f,
                0,
                0));
        }

        return [.. parts];
    }

    public static readonly EntityModelDefinition Definition = new(
        "happy_ghast",
        64,
        64,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/ghast/happy_ghast",
        4f);
}
