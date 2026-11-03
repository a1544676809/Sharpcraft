// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby nautilus/happy ghast/glow squid models.</summary>
public static class BabyModelDefinitionsFinal
{
    public static readonly EntityModelDefinition Nautilus = new(
        "baby_nautilus",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("root", -0.5f, 28f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell", "root", 3f, -8f, -2f, -6f, -4f, -1f, 7f, 4f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("shell_mid", "shell", 0f, 0f, 0f, -6f, 0f, -1f, 7f, 4f, 9f, 0, 11),
            PlayerModelDefinition.FromVanillaChild("shell_back", "shell", 0f, 0f, 0f, -6f, 0f, 5f, 7f, 4f, 0f, 23, 11),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0.5f, -5f, 3f, -2.5f, -3.01f, -1f, 5f, 4f, 7f, 0, 24),
            PlayerModelDefinition.FromVanillaChild("body_back", "body", 0f, 0f, 0f, -2.5f, -3.01f, 4.1f, 5f, 4f, 0f, 0, 35),
            PlayerModelDefinition.FromVanillaChild("upper_mouth", "body", 0f, -2.01f, 3.9f, -2.5f, -1f, 0f, 5f, 2f, 2f, 24, 24, grow: -0.001f),
            PlayerModelDefinition.FromVanillaChild("inner_mouth", "body", 0f, -1.01f, 4.9f, -1.5f, -1f, -1f, 3f, 2f, 2f, 24, 32),
            PlayerModelDefinition.FromVanillaChild("lower_mouth", "body", 0f, -0.01f, 3.9f, -2.5f, -1f, 0f, 5f, 2f, 2f, 24, 28, grow: -0.001f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/nautilus/nautilus_baby");

    public static readonly EntityModelDefinition HappyGhast = new(
        "baby_happy_ghast",
        64,
        64,
        BuildHappyGhastParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/ghast/happy_ghast_baby",
        0.95f);

    public static readonly EntityModelDefinition GlowSquid = new(
        "baby_glow_squid",
        32,
        32,
        BabyModelDefinitionsAquatic.Squid.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/squid/glow_squid_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [89] = Nautilus,
            [59] = HappyGhast,
            [62] = GlowSquid
        };

    private static EntityModelPart[] BuildHappyGhastParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("inner_body", "body", 0f, 8f, 0f, -8f, -16f, -8f, 16f, 16f, 16f, 0, 32, grow: -0.5f)
        };

        (float X, float Y, float Z, float Length)[] tentacles =
        [
            (-3.75f, 7f, -5f, 5f), (1.25f, 7f, -5f, 7f), (6.25f, 7f, -5f, 4f),
            (-6.25f, 7f, 0f, 5f), (-1.25f, 7f, 0f, 5f), (3.75f, 7f, 0f, 7f),
            (-3.75f, 7f, 5f, 8f), (1.25f, 7f, 5f, 8f), (6.25f, 7f, 5f, 5f)
        ];
        for (var i = 0; i < tentacles.Length; i++)
        {
            var (x, y, z, length) = tentacles[i];
            parts.Add(PlayerModelDefinition.FromVanillaChild("tentacle" + i, "body", x, y, z, -1f, 0f, -1f, 2f, length, 2f, 0, 0));
        }

        return [.. parts];
    }
}
