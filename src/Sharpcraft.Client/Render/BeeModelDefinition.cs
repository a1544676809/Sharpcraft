// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultBeeModel: an empty "bone" pivot carrying the body, two-segment wings and three leg planes, with the body
/// carrying the stinger and both antennae.
/// </summary>
/// <remarks>
/// Static bind pose (the wing flap animation is not ported). The renderer also swaps in angry/nectar textures by entity
/// state; those state textures are not wired yet (docs/analysis/render/entity-variants.md).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.bee.BeeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/bee/BeeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BeeModelDefinition
{
    public const int BeeEntityTypeId = 11;

    public static readonly EntityModelDefinition Definition = new(
        "bee",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("bone", 0f, 19f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "bone", 0f, 0f, 0f, -3.5f, -4f, -5f, 7f, 7f, 10f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("stinger", "body", 0f, 0f, 0f, 0f, -1f, 5f, 0f, 1f, 2f, 26, 7),
            PlayerModelDefinition.FromVanillaChild("left_antenna", "body", 0f, -2f, -5f, 1.5f, -2f, -3f, 1f, 2f, 3f, 2, 0),
            PlayerModelDefinition.FromVanillaChild("right_antenna", "body", 0f, -2f, -5f, -2.5f, -2f, -3f, 1f, 2f, 3f, 2, 3),
            PlayerModelDefinition.FromVanillaChild("right_wing", "bone", -1.5f, -4f, -3f, -9f, 0f, 0f, 9f, 0f, 6f, 0, 18,
                rotationY: -0.2618f, grow: 0.001f),
            PlayerModelDefinition.FromVanillaChild("left_wing", "bone", 1.5f, -4f, -3f, 0f, 0f, 0f, 9f, 0f, 6f, 0, 18,
                mirror: true, rotationY: 0.2618f, grow: 0.001f),
            PlayerModelDefinition.FromVanillaChild("front_legs", "bone", 1.5f, 3f, -2f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 1),
            PlayerModelDefinition.FromVanillaChild("middle_legs", "bone", 1.5f, 3f, 0f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 3),
            PlayerModelDefinition.FromVanillaChild("back_legs", "bone", 1.5f, 3f, 2f, -5f, 0f, 0f, 7f, 2f, 0f, 26, 5)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bee/bee",
        textureSelector: state => state.IsAngry
            ? (state.HasNectar ? "entity/bee/bee_angry_nectar" : "entity/bee/bee_angry")
            : (state.HasNectar ? "entity/bee/bee_nectar" : "entity/bee/bee"),
        additionalTexturePaths:
        [
            "entity/bee/bee_angry", "entity/bee/bee_nectar", "entity/bee/bee_angry_nectar"
        ]);
}
