// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SilverfishModel: seven root body segments plus three root layer cubes. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-687).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.silverfish.SilverfishModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/silverfish/SilverfishModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SilverfishModelDefinition
{
    public const int SilverfishEntityTypeId = 117;

    private static readonly VanillaMeshDefinition SilverfishMesh = BuildSilverfishMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "silverfish",
        64,
        32,
        SilverfishMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/silverfish/silverfish");

    private static VanillaMeshDefinition BuildSilverfishMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("segment0", VanillaPartPose.Offset(0f, 22f, -3.5f),
            new VanillaCubeDefinition("segment0", 0, 0, -1.5f, 0f, -1f, 3f, 2f, 2f));
        root.AddOrReplaceChild("segment1", VanillaPartPose.Offset(0f, 21f, -1.5f),
            new VanillaCubeDefinition("segment1", 0, 4, -2f, 0f, -1f, 4f, 3f, 2f));
        root.AddOrReplaceChild("segment2", VanillaPartPose.Offset(0f, 20f, 1f),
            new VanillaCubeDefinition("segment2", 0, 9, -3f, 0f, -1.5f, 6f, 4f, 3f));
        root.AddOrReplaceChild("segment3", VanillaPartPose.Offset(0f, 21f, 4f),
            new VanillaCubeDefinition("segment3", 0, 16, -1.5f, 0f, -1.5f, 3f, 3f, 3f));
        root.AddOrReplaceChild("segment4", VanillaPartPose.Offset(0f, 22f, 6.5f),
            new VanillaCubeDefinition("segment4", 0, 22, -1f, 0f, -1.5f, 2f, 2f, 3f));
        root.AddOrReplaceChild("segment5", VanillaPartPose.Offset(0f, 23f, 8.5f),
            new VanillaCubeDefinition("segment5", 11, 0, -1f, 0f, -1f, 2f, 1f, 2f));
        root.AddOrReplaceChild("segment6", VanillaPartPose.Offset(0f, 23f, 10.5f),
            new VanillaCubeDefinition("segment6", 13, 4, -0.5f, 0f, -1f, 1f, 1f, 2f));
        root.AddOrReplaceChild("layer0", VanillaPartPose.Offset(0f, 16f, 1f),
            new VanillaCubeDefinition("layer0", 20, 0, -5f, 0f, -1.5f, 10f, 8f, 3f));
        root.AddOrReplaceChild("layer1", VanillaPartPose.Offset(0f, 20f, 6.5f),
            new VanillaCubeDefinition("layer1", 20, 11, -3f, 0f, -1.5f, 6f, 4f, 3f));
        root.AddOrReplaceChild("layer2", VanillaPartPose.Offset(0f, 19f, -1.5f),
            new VanillaCubeDefinition("layer2", 20, 18, -3f, 0f, -1.5f, 6f, 5f, 2f));
        return new VanillaMeshDefinition(root);
    }
}
