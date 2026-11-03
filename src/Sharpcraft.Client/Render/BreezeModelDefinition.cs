// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// BreezeModel: the body with head/eyes/rods and a separate translucent wind body made of nested shells.
/// Base body texture is 32x32; wind uses a 128x128 texture and eyes use an emissive 32x32 texture.
/// </summary>
/// <remarks>Bind pose only; idle/shoot/slide animations and the wind UV scroll are not ported.</remarks>
public static class BreezeModelDefinition
{
    public const int BreezeEntityTypeId = 17;

    public static readonly EntityModelDefinition Definition = new(
        "breeze",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("rods", "body", 0f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("rod_1", "rods", 2.5981f, -3f, 1.5f, -1f, 0f, -3f, 2f, 8f, 2f, 0, 17,
                rotationX: -2.7489f, rotationY: -1.0472f, rotationZ: 3.1416f),
            PlayerModelDefinition.FromVanillaChild("rod_2", "rods", -2.5981f, -3f, 1.5f, -1f, 0f, -3f, 2f, 8f, 2f, 0, 17,
                rotationX: -2.7489f, rotationY: 1.0472f, rotationZ: 3.1416f),
            PlayerModelDefinition.FromVanillaChild("rod_3", "rods", 0f, -3f, -3f, -1f, 0f, -3f, 2f, 8f, 2f, 0, 17,
                rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 4f, 0f, -5f, -5f, -4.2f, 10f, 3f, 4f, 4, 24),
            PlayerModelDefinition.FromVanillaChild("head_cube", "head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("eyes", "head", 0f, 0f, 0f, -5f, -5f, -4.2f, 10f, 3f, 4f, 4, 24) with { EmissiveOnly = true },
            PlayerModelDefinition.FromVanillaChild("eyes_cube", "eyes", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0) with { EmissiveOnly = true },
            PlayerModelDefinition.FromVanilla("wind_body", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            WindPart("wind_bottom", "wind_body", 0f, 24f, 0f, -2.5f, -7f, -2.5f, 5f, 7f, 5f, 1, 83),
            WindPart("wind_mid", "wind_bottom", 0f, -7f, 0f, -6f, -6f, -6f, 12f, 6f, 12f, 74, 28),
            WindPart("wind_mid_mid", "wind_mid", 0f, 0f, 0f, -4f, -6f, -4f, 8f, 6f, 8f, 78, 32),
            WindPart("wind_mid_inner", "wind_mid", 0f, 0f, 0f, -2.5f, -6f, -2.5f, 5f, 6f, 5f, 49, 71),
            WindPart("wind_top", "wind_mid", 0f, -6f, 0f, -9f, -8f, -9f, 18f, 8f, 18f, 0, 0),
            WindPart("wind_top_mid", "wind_top", 0f, 0f, 0f, -6f, -8f, -6f, 12f, 8f, 12f, 6, 6),
            WindPart("wind_top_inner", "wind_top", 0f, 0f, 0f, -2.5f, -8f, -2.5f, 5f, 8f, 5f, 105, 57)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/breeze/breeze",
        1f,
        "entity/breeze/breeze_eyes",
        additionalTexturePaths: ["entity/breeze/breeze_wind", "entity/breeze/breeze_eyes"]);

    private static EntityModelPart WindPart(
        string name,
        string parent,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY) =>
        PlayerModelDefinition.FromVanillaChild(
            name,
            parent,
            pivotX,
            pivotY,
            pivotZ,
            minX,
            minY,
            minZ,
            width,
            height,
            depth,
            textureX,
            textureY) with
        {
            Kind = EntityGeometryLayerKind.Translucent,
            TexturePath = "entity/breeze/breeze_wind",
            TextureWidth = 128,
            TextureHeight = 128,
            IncludeInEmissive = false
        };
}
