// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// TadpoleModel: a small body and a flat tail, both root parts; static bind pose. Declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-677).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.frog.TadpoleModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/TadpoleModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class TadpoleModelDefinition
{
    public const int TadpoleEntityTypeId = 134;

    /// <summary>Declarative DSL tree for the two tadpole parts (T-677).</summary>
    private static readonly VanillaMeshDefinition TadpoleMesh = BuildTadpoleMesh();

    public static readonly EntityModelDefinition Definition = new(
        "tadpole",
        16,
        16,
        [.. VanillaMeshBake.Bake(TadpoleMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/tadpole/tadpole");

    private static VanillaMeshDefinition BuildTadpoleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22f, -3f),
            new VanillaCubeDefinition("body", 0, 0, -1.5f, -1f, 0f, 3f, 2f, 3f));
        root.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("tail", 0, 0, 0f, -1f, 0f, 0f, 2f, 7f));
        return new VanillaMeshDefinition(root);
    }
}
