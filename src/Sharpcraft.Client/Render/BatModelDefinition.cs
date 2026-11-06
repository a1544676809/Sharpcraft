// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// BatModel: body with feet and two-segment wings, plus a separate head with ears. All pivots are the reference's; the
/// animation system is not ported, so the static pose is the bind pose. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-678).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.ambient.BatModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/ambient/BatModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BatModelDefinition
{
    public const int BatEntityTypeId = 10;

    /// <summary>Declarative DSL tree for the nine nested bat parts (T-678).</summary>
    private static readonly VanillaMeshDefinition BatMesh = BuildBatMesh();

    public static readonly EntityModelDefinition Definition = new(
        "bat",
        32,
        32,
        [.. VanillaMeshBake.Bake(BatMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bat/bat");

    private static VanillaMeshDefinition BuildBatMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 17f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -1.5f, 0f, -1f, 3f, 5f, 2f));
        body.AddOrReplaceChild("feet", VanillaPartPose.Offset(0f, 5f, 0f),
            new VanillaCubeDefinition("feet", 16, 16, -1.5f, 0f, 0f, 3f, 2f, 0f));
        var rightWing = body.AddOrReplaceChild("right_wing", VanillaPartPose.Offset(-1.5f, 0f, 0f),
            new VanillaCubeDefinition("right_wing", 12, 0, -2f, -2f, 0f, 2f, 7f, 0f));
        rightWing.AddOrReplaceChild("right_wing_tip", VanillaPartPose.Offset(-2f, 0f, 0f),
            new VanillaCubeDefinition("right_wing_tip", 16, 0, -6f, -2f, 0f, 6f, 8f, 0f));
        var leftWing = body.AddOrReplaceChild("left_wing", VanillaPartPose.Offset(1.5f, 0f, 0f),
            new VanillaCubeDefinition("left_wing", 12, 7, 0f, -2f, 0f, 2f, 7f, 0f));
        leftWing.AddOrReplaceChild("left_wing_tip", VanillaPartPose.Offset(2f, 0f, 0f),
            new VanillaCubeDefinition("left_wing_tip", 16, 8, 0f, -2f, 0f, 6f, 8f, 0f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 17f, 0f),
            new VanillaCubeDefinition("head", 0, 7, -2f, -3f, -1f, 4f, 3f, 2f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-1.5f, -2f, 0f),
            new VanillaCubeDefinition("right_ear", 1, 15, -2.5f, -4f, 0f, 3f, 5f, 0f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(1.1f, -3f, 0f),
            new VanillaCubeDefinition("left_ear", 8, 15, -0.1f, -3f, 0f, 3f, 5f, 0f));
        return new VanillaMeshDefinition(root);
    }
}
