// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SheepModel (QuadrupedModel legSize 12) plus the SheepWoolLayer overlay textures.
/// </summary>
public static class SheepModelDefinition
{
    public const int SheepEntityTypeId = 114;
    private const string WoolTexture = "entity/sheep/sheep_wool";

    public static readonly EntityModelDefinition Definition = new(
        "sheep",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 6f, -8f, -3f, -4f, -6f, 6f, 6f, 8f, 0, 0,
                overlays: [Wool(0.6f, 0, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -4f, -10f, -7f, 8f, 16f, 6f, 28, 8,
                overlays: [Wool(1.75f, 28, 8)], rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 12f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                overlays: [Wool(0.5f, 0, 16)]),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 12f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                mirror: true, overlays: [Wool(0.5f, 0, 16)]),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 12f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                overlays: [Wool(0.5f, 0, 16)]),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 12f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                mirror: true, overlays: [Wool(0.5f, 0, 16)])
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/sheep/sheep");

    private static EntityModelLayer Wool(float grow, int textureX, int textureY) =>
        new("wool", grow, textureX, textureY, false, WoolTexture);
}
