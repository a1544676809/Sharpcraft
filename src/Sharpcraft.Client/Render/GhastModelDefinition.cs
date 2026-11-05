// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// GhastModel: one 16x16x16 body and nine tentacles whose lengths come from the reference's seeded Random(1660).
/// The whole model is scaled 4.5x. Charging swaps to the shooting texture. Tentacle sway is not ported.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.ghast.GhastModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ghast/GhastModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class GhastModelDefinition
{
    public const int GhastEntityTypeId = 58;

    private static readonly int[] TentacleLengths = [8, 13, 9, 11, 11, 10, 12, 9, 12];

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 17.6f, 0f, -8f, -8f, -8f, 16f, 16f, 16f, 0, 0)
        };

        for (var i = 0; i < 9; i++)
        {
            var xo = ((i % 3 - i / 3 % 2 * 0.5f + 0.25f) - 1f) * 5f;
            var yo = (i / 3 / 2 * 2 - 1f) * 5f;
            parts.Add(PlayerModelDefinition.FromVanilla(
                "tentacle" + i,
                xo,
                24.6f,
                yo,
                -1f,
                0f,
                -1f,
                2f,
                TentacleLengths[i],
                2f,
                0,
                0));
        }

        return [.. parts];
    }

    public static readonly EntityModelDefinition Definition = new(
        "ghast",
        64,
        32,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/ghast/ghast",
        4.5f,
        textureSelector: state => state.IsCharging ? "entity/ghast/ghast_shooting" : "entity/ghast/ghast",
        additionalTexturePaths: ["entity/ghast/ghast_shooting"]);
}
