// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SheepModel (base) + SheepFurModel (wool), both expressed as independent raw cubes. The base legs are 4x12x4;
/// the wool legs are separate 4x6x4 cubes with grow 0.5 and must not be modeled as overlays of the base legs.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.sheep.SheepModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/sheep/SheepModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SheepModelDefinition
{
    public const int SheepEntityTypeId = 114;
    private const string WoolTexture = "entity/sheep/sheep_wool";

    public static readonly EntityModelDefinition Definition = new(
        "sheep",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 6f, -8f, -3f, -4f, -6f, 6f, 6f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -4f, -10f, -7f, 8f, 16f, 6f, 28, 8, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("wool_head", 0f, 6f, -8f, -3f, -4f, -4f, 6f, 6f, 6f, 0, 0,
                grow: 0.6f, texturePath: WoolTexture),
            PlayerModelDefinition.FromVanilla("wool_body", 0f, 5f, 2f, -4f, -10f, -7f, 8f, 16f, 6f, 28, 8,
                rotationX: -MathF.PI / 2f, grow: 1.75f, texturePath: WoolTexture),
            PlayerModelDefinition.FromVanilla("wool_right_hind_leg", -3f, 12f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                grow: 0.5f, texturePath: WoolTexture),
            PlayerModelDefinition.FromVanilla("wool_left_hind_leg", 3f, 12f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                mirror: true, grow: 0.5f, texturePath: WoolTexture),
            PlayerModelDefinition.FromVanilla("wool_right_front_leg", -3f, 12f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                grow: 0.5f, texturePath: WoolTexture),
            PlayerModelDefinition.FromVanilla("wool_left_front_leg", 3f, 12f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16,
                mirror: true, grow: 0.5f, texturePath: WoolTexture)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/sheep/sheep");
}
