// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultStriderModel: two long legs, a big body, and six flat bristles splayed out from the body's sides.
/// </summary>
/// <remarks>
/// Bind pose only. The reference swaps to a cold texture when the strider is suffocating (overworld), driven by the
/// boolean synced value; the saddle layer is not ported. Parts are declared with the <c>VanillaMeshDefinition</c> DSL
/// and baked via <c>VanillaMeshBake.Bake</c> (T-681); the cold texture selector and additional path are unchanged.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.strider.StriderModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/strider/StriderModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class StriderModelDefinition
{
    public const int StriderEntityTypeId = 132;

    /// <summary>Declarative DSL tree for the nine strider parts (T-681).</summary>
    private static readonly VanillaMeshDefinition StriderMesh = BuildStriderMesh();

    public static readonly EntityModelDefinition Definition = new(
        "strider",
        64,
        128,
        [.. VanillaMeshBake.Bake(StriderMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/strider/strider",
        textureSelector: state => state.IsCold ? "entity/strider/strider_cold" : "entity/strider/strider",
        additionalTexturePaths: ["entity/strider/strider_cold"]);

    private static VanillaMeshDefinition BuildStriderMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-4f, 8f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 32, -2f, 0f, -2f, 4f, 16f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(4f, 8f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 55, -2f, 0f, -2f, 4f, 16f, 4f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 1f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -8f, -6f, -8f, 16f, 14f, 16f));
        body.AddOrReplaceChild("right_bottom_bristle", VanillaPartPose.OffsetAndRotation(-8f, 4f, -8f, 0f, 0f, -1.2217305f),
            new VanillaCubeDefinition("right_bottom_bristle", 16, 65, -12f, 0f, 0f, 12f, 0f, 16f, Mirror: true));
        body.AddOrReplaceChild("right_middle_bristle", VanillaPartPose.OffsetAndRotation(-8f, -1f, -8f, 0f, 0f, -1.134464f),
            new VanillaCubeDefinition("right_middle_bristle", 16, 49, -12f, 0f, 0f, 12f, 0f, 16f, Mirror: true));
        body.AddOrReplaceChild("right_top_bristle", VanillaPartPose.OffsetAndRotation(-8f, -5f, -8f, 0f, 0f, -0.87266463f),
            new VanillaCubeDefinition("right_top_bristle", 16, 33, -12f, 0f, 0f, 12f, 0f, 16f, Mirror: true));
        body.AddOrReplaceChild("left_top_bristle", VanillaPartPose.OffsetAndRotation(8f, -6f, -8f, 0f, 0f, 0.87266463f),
            new VanillaCubeDefinition("left_top_bristle", 16, 33, 0f, 0f, 0f, 12f, 0f, 16f));
        body.AddOrReplaceChild("left_middle_bristle", VanillaPartPose.OffsetAndRotation(8f, -2f, -8f, 0f, 0f, 1.134464f),
            new VanillaCubeDefinition("left_middle_bristle", 16, 49, 0f, 0f, 0f, 12f, 0f, 16f));
        body.AddOrReplaceChild("left_bottom_bristle", VanillaPartPose.OffsetAndRotation(8f, 3f, -8f, 0f, 0f, 1.2217305f),
            new VanillaCubeDefinition("left_bottom_bristle", 16, 65, 0f, 0f, 0f, 12f, 0f, 16f));
        return new VanillaMeshDefinition(root);
    }
}
