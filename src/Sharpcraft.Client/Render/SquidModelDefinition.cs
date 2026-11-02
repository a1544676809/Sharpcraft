// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SquidModel: one 12x16x12 body and eight 2x18x2 tentacles rotated around Y.
/// </summary>
public static class SquidModelDefinition
{
    public const int SquidEntityTypeId = 130;
    public const int GlowSquidEntityTypeId = 62;

    public static readonly EntityModelDefinition Definition = new(
        "squid",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 8f, 0f, -6f, -8f, -6f, 12f, 16f, 12f, 0, 0, grow: 0.02f),
            Tentacle(0, 5f, 0f, MathF.PI / 2f),
            Tentacle(1, 3.535f, 3.535f, MathF.PI / 4f),
            Tentacle(2, 0f, 5f, 0f),
            Tentacle(3, -3.535f, 3.535f, -MathF.PI / 4f),
            Tentacle(4, -5f, 0f, -MathF.PI / 2f),
            Tentacle(5, -3.535f, -3.535f, -3f * MathF.PI / 4f),
            Tentacle(6, 0f, -5f, MathF.PI),
            Tentacle(7, 3.535f, -3.535f, 3f * MathF.PI / 4f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/squid/squid");

    public static readonly EntityModelDefinition GlowSquid = new(
        "glow_squid",
        64,
        32,
        SquidModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/squid/glow_squid");

    private static EntityModelPart Tentacle(int index, float x, float z, float yRot) =>
        PlayerModelDefinition.FromVanilla(
            $"tentacle{index}",
            x,
            15f,
            z,
            -1f,
            0f,
            -1f,
            2f,
            18f,
            2f,
            48,
            0,
            rotationY: yRot);
}
