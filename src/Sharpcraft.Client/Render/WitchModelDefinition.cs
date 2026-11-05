// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// WitchModel: the villager body (body + grown jacket, crossed arms, legs) with a witch head carrying a four-tier hat
/// and a mole on the nose.
/// </summary>
/// <remarks>
/// Bind pose only. The reference starts from <c>VillagerModel.createBodyModel()</c> and replaces only the head/nose;
/// the jacket is a 0.5-grown outer layer of the body. Held-item and nose-wiggle animations are not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.witch.WitchModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/witch/WitchModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WitchModelDefinition
{
    public const int WitchEntityTypeId = 148;

    private static readonly float Pi = MathF.PI;

    public static readonly EntityModelDefinition Definition = new(
        "witch",
        64,
        128,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0, mirrorX: false),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0),
            PlayerModelDefinition.FromVanillaChild("mole", "nose", 0f, -2f, 0f, 0f, 3f, -6.75f, 1f, 1f, 1f, 0, 0, grow: -0.25f),
            PlayerModelDefinition.FromVanillaChild("hat", "head", -5f, -10.03125f, -5f, 0f, 0f, 0f, 10f, 2f, 10f, 0, 64),
            PlayerModelDefinition.FromVanillaChild("hat2", "hat", 1.75f, -4f, 2f, 0f, 0f, 0f, 7f, 4f, 7f, 0, 76,
                rotationX: -0.05235988f, rotationZ: 0.02617994f),
            PlayerModelDefinition.FromVanillaChild("hat3", "hat2", 1.75f, -4f, 2f, 0f, 0f, 0f, 4f, 4f, 4f, 0, 87,
                rotationX: -0.10471976f, rotationZ: 0.05235988f),
            PlayerModelDefinition.FromVanillaChild("hat4", "hat3", 1.75f, -2f, 2f, 0f, 0f, 0f, 1f, 2f, 1f, 0, 95,
                grow: 0.25f, rotationX: -Pi / 15f, rotationZ: 0.10471976f),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20, mirrorX: false),
            PlayerModelDefinition.FromVanillaChild("jacket", "body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.5f),
            PlayerModelDefinition.FromVanilla("arms", 0f, 3f, -1f, -4f, -2f, -2f, 8f, 4f, 4f, 40, 38, mirrorX: false, rotationX: 0.75f),
            PlayerModelDefinition.FromVanillaChild("arms_left", "arms", 0f, 0f, 0f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22),
            PlayerModelDefinition.FromVanillaChild("arms_right", "arms", 0f, 0f, 0f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/witch/witch",
        0.9375f);
}
