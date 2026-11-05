// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// BatModel: body with feet and two-segment wings, plus a separate head with ears. All pivots are the reference's; the
/// animation system is not ported, so the static pose is the bind pose.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.ambient.BatModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/ambient/BatModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BatModelDefinition
{
    public const int BatEntityTypeId = 10;

    public static readonly EntityModelDefinition Definition = new(
        "bat",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 17f, 0f, -1.5f, 0f, -1f, 3f, 5f, 2f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("feet", "body", 0f, 5f, 0f, -1.5f, 0f, 0f, 3f, 2f, 0f, 16, 16),
            PlayerModelDefinition.FromVanillaChild("right_wing", "body", -1.5f, 0f, 0f, -2f, -2f, 0f, 2f, 7f, 0f, 12, 0),
            PlayerModelDefinition.FromVanillaChild("right_wing_tip", "right_wing", -2f, 0f, 0f, -6f, -2f, 0f, 6f, 8f, 0f, 16, 0),
            PlayerModelDefinition.FromVanillaChild("left_wing", "body", 1.5f, 0f, 0f, 0f, -2f, 0f, 2f, 7f, 0f, 12, 7),
            PlayerModelDefinition.FromVanillaChild("left_wing_tip", "left_wing", 2f, 0f, 0f, 0f, -2f, 0f, 6f, 8f, 0f, 16, 8),
            PlayerModelDefinition.FromVanilla("head", 0f, 17f, 0f, -2f, -3f, -1f, 4f, 3f, 2f, 0, 7),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -1.5f, -2f, 0f, -2.5f, -4f, 0f, 3f, 5f, 0f, 1, 15),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 1.1f, -3f, 0f, -0.1f, -3f, 0f, 3f, 5f, 0f, 8, 15)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bat/bat");
}
