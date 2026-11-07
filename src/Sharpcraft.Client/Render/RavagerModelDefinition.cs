// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// RavagerModel: a neck carrying the big head with snout, two horns and a mouth; a rotated two-slab body and four tall
/// legs. Bind pose only; walk/attack/roar animations are not ported. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-687).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.ravager.RavagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/ravager/RavagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class RavagerModelDefinition
{
    public const int RavagerEntityTypeId = 112;

    private static readonly VanillaMeshDefinition RavagerMesh = BuildRavagerMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "ravager",
        128,
        128,
        RavagerMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/illager/ravager");

    private static VanillaMeshDefinition BuildRavagerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var neck = root.AddOrReplaceChild("neck", VanillaPartPose.Offset(0f, -7f, 5.5f),
            new VanillaCubeDefinition("neck", 68, 73, -5f, -1f, -18f, 10f, 10f, 18f));
        var head = neck.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 16f, -17f),
            new VanillaCubeDefinition("head", 0, 0, -8f, -20f, -14f, 16f, 20f, 16f));
        head.AddOrReplaceChild("head_snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_snout", 0, 0, -2f, -6f, -18f, 4f, 8f, 4f));
        head.AddOrReplaceChild("right_horn", VanillaPartPose.OffsetAndRotation(-10f, -14f, -8f, 1.0995574f, 0f, 0f),
            new VanillaCubeDefinition("right_horn", 74, 55, 0f, -14f, -2f, 2f, 14f, 4f));
        head.AddOrReplaceChild("left_horn", VanillaPartPose.OffsetAndRotation(8f, -14f, -8f, 1.0995574f, 0f, 0f),
            new VanillaCubeDefinition("left_horn", 74, 55, 0f, -14f, -2f, 2f, 14f, 4f, Mirror: true));
        head.AddOrReplaceChild("mouth", VanillaPartPose.Offset(0f, -2f, 2f),
            new VanillaCubeDefinition("mouth", 0, 36, -8f, 0f, -16f, 16f, 3f, 16f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 1f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 55, -7f, -10f, -7f, 14f, 16f, 20f));
        body.AddOrReplaceChild("body_lower", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_lower", 0, 91, -6f, 6f, -7f, 12f, 13f, 18f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-8f, -13f, 18f),
            new VanillaCubeDefinition("right_hind_leg", 96, 0, -4f, 0f, -4f, 8f, 37f, 8f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(8f, -13f, 18f),
            new VanillaCubeDefinition("left_hind_leg", 96, 0, -4f, 0f, -4f, 8f, 37f, 8f, Mirror: true));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-8f, -13f, -5f),
            new VanillaCubeDefinition("right_front_leg", 64, 0, -4f, 0f, -4f, 8f, 37f, 8f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(8f, -13f, -5f),
            new VanillaCubeDefinition("left_front_leg", 64, 0, -4f, 0f, -4f, 8f, 37f, 8f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
