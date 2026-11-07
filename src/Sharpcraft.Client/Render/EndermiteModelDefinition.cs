// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// EndermiteModel: four root segments with fixed sizes/texture offsets, no parent transforms. Parts are declared with
/// the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-691).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.endermite.EndermiteModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/endermite/EndermiteModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class EndermiteModelDefinition
{
    public const int EndermiteEntityTypeId = 43;

    private static readonly VanillaMeshDefinition EndermiteMesh = BuildEndermiteMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "endermite",
        64,
        32,
        EndermiteMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/endermite/endermite");

    private static VanillaMeshDefinition BuildEndermiteMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("segment0", VanillaPartPose.Offset(0f, 21f, -3.5f),
            new VanillaCubeDefinition("segment0", 0, 0, -2f, 0f, -1f, 4f, 3f, 2f));
        root.AddOrReplaceChild("segment1", VanillaPartPose.Offset(0f, 20f, 0f),
            new VanillaCubeDefinition("segment1", 0, 5, -3f, 0f, -2.5f, 6f, 4f, 5f));
        root.AddOrReplaceChild("segment2", VanillaPartPose.Offset(0f, 21f, 3f),
            new VanillaCubeDefinition("segment2", 0, 14, -1.5f, 0f, -0.5f, 3f, 3f, 1f));
        root.AddOrReplaceChild("segment3", VanillaPartPose.Offset(0f, 22f, 4f),
            new VanillaCubeDefinition("segment3", 0, 18, -0.5f, 0f, -0.5f, 1f, 2f, 1f));
        return new VanillaMeshDefinition(root);
    }
}
