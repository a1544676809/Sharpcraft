// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>TadpoleModel: a small body and a flat tail, both root parts; static bind pose.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.frog.TadpoleModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/TadpoleModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class TadpoleModelDefinition
{
    public const int TadpoleEntityTypeId = 134;

    public static readonly EntityModelDefinition Definition = new(
        "tadpole",
        16,
        16,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 22f, -3f, -1.5f, -1f, 0f, 3f, 2f, 3f, 0, 0),
            PlayerModelDefinition.FromVanilla("tail", 0f, 22f, 0f, 0f, -1f, 0f, 0f, 2f, 7f, 0, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/tadpole/tadpole");
}
