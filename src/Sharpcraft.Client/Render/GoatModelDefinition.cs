// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// GoatModel: head parts (ears/goatee/horns/nose), two body cubes and four 3x6x3/3x10x3 legs. Parts are declared with
/// the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-685).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.goat.GoatModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/goat/GoatModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class GoatModelDefinition
{
    public const int GoatEntityTypeId = 63;

    /// <summary>Declarative DSL tree for the twelve goat parts (T-685).</summary>
    private static readonly VanillaMeshDefinition GoatMesh = BuildGoatMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "goat",
        64,
        64,
        GoatMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/goat/goat");

    private static VanillaMeshDefinition BuildGoatMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(1f, 14f, 0f),
            new VanillaCubeDefinition("right_ear", 2, 61, -6f, -11f, -10f, 3f, 2f, 1f));
        root.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(1f, 14f, 0f),
            new VanillaCubeDefinition("left_ear", 2, 61, 2f, -11f, -10f, 3f, 2f, 1f, Mirror: true));
        root.AddOrReplaceChild("goatee", VanillaPartPose.Offset(1f, 14f, 0f),
            new VanillaCubeDefinition("goatee", 23, 52, -0.5f, -3f, -14f, 0f, 7f, 5f));
        root.AddOrReplaceChild("nose", VanillaPartPose.OffsetAndRotation(1f, 6f, -8f, -0.9599f, 0f, 0f),
            new VanillaCubeDefinition("nose", 34, 46, -3f, -4f, -8f, 5f, 7f, 10f));
        root.AddOrReplaceChild("left_horn", VanillaPartPose.Offset(1f, 14f, 0f),
            new VanillaCubeDefinition("left_horn", 12, 55, -0.01f, -16f, -10f, 2f, 7f, 2f));
        root.AddOrReplaceChild("right_horn", VanillaPartPose.Offset(1f, 14f, 0f),
            new VanillaCubeDefinition("right_horn", 12, 55, -2.99f, -16f, -10f, 2f, 7f, 2f));
        root.AddOrReplaceChild("body_lower", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("body_lower", 1, 1, -4f, -17f, -7f, 9f, 11f, 16f));
        root.AddOrReplaceChild("body_upper", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("body_upper", 0, 28, -5f, -18f, -8f, 11f, 14f, 11f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1f, 14f, 4f),
            new VanillaCubeDefinition("left_hind_leg", 36, 29, 0f, 4f, 0f, 3f, 6f, 3f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3f, 14f, 4f),
            new VanillaCubeDefinition("right_hind_leg", 49, 29, 0f, 4f, 0f, 3f, 6f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(1f, 14f, -6f),
            new VanillaCubeDefinition("left_front_leg", 49, 2, 0f, 0f, 0f, 3f, 10f, 3f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3f, 14f, -6f),
            new VanillaCubeDefinition("right_front_leg", 35, 2, 0f, 0f, 0f, 3f, 10f, 3f));
        return new VanillaMeshDefinition(root);
    }
}
