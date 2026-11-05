// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>CodModel: body, head, nose, two side fins, tail fin and a flat top fin; static bind pose.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fish.CodModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/CodModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CodModelDefinition
{
    public const int CodEntityTypeId = 27;

    public static readonly EntityModelDefinition Definition = new(
        "cod",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 22f, 0f, -1f, -2f, 0f, 2f, 4f, 7f, 0, 0),
            PlayerModelDefinition.FromVanilla("head", 0f, 22f, 0f, -1f, -2f, -3f, 2f, 4f, 3f, 11, 0),
            PlayerModelDefinition.FromVanilla("nose", 0f, 22f, -3f, -1f, -2f, -1f, 2f, 3f, 1f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_fin", -1f, 23f, 0f, -2f, 0f, -1f, 2f, 0f, 2f, 22, 1,
                rotationZ: MathF.PI / 4f),
            PlayerModelDefinition.FromVanilla("left_fin", 1f, 23f, 0f, 0f, 0f, -1f, 2f, 0f, 2f, 22, 4,
                rotationZ: -MathF.PI / 4f),
            PlayerModelDefinition.FromVanilla("tail_fin", 0f, 22f, 7f, 0f, -2f, 0f, 0f, 4f, 4f, 22, 3),
            PlayerModelDefinition.FromVanilla("top_fin", 0f, 20f, 0f, 0f, -1f, -1f, 0f, 1f, 6f, 20, -6)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fish/cod");
}
