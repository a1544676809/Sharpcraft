// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PolarBearModel: head with mouth/ears, two body cubes, and four legs; the layer scales by 1.2. Parts are declared with
/// the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-687).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.polarbear.PolarBearModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/polarbear/PolarBearModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PolarBearModelDefinition
{
    public const int PolarBearEntityTypeId = 105;

    private static readonly VanillaMeshDefinition PolarBearMesh = BuildPolarBearMesh();

    public static readonly EntityModelDefinition Definition = new(
        "polar_bear",
        128,
        64,
        [.. VanillaMeshBake.Bake(PolarBearMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bear/polarbear",
        1.2f);

    private static VanillaMeshDefinition BuildPolarBearMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 10f, -16f),
            new VanillaCubeDefinition("head", 0, 0, -3.5f, -3f, -3f, 7f, 7f, 7f));
        root.AddOrReplaceChild("mouth", VanillaPartPose.Offset(0f, 10f, -16f),
            new VanillaCubeDefinition("mouth", 0, 44, -2.5f, 1f, -6f, 5f, 3f, 3f));
        root.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(0f, 10f, -16f),
            new VanillaCubeDefinition("right_ear", 26, 0, -4.5f, -4f, -1f, 2f, 2f, 1f));
        root.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(0f, 10f, -16f),
            new VanillaCubeDefinition("left_ear", 26, 0, 2.5f, -4f, -1f, 2f, 2f, 1f, Mirror: true));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(-2f, 9f, 12f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 19, -5f, -13f, -7f, 14f, 14f, 11f));
        root.AddOrReplaceChild("body_upper", VanillaPartPose.OffsetAndRotation(-2f, 9f, 12f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body_upper", 39, 0, -4f, -25f, -7f, 12f, 12f, 10f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-4.5f, 14f, 6f),
            new VanillaCubeDefinition("right_hind_leg", 50, 22, -2f, 0f, -2f, 4f, 10f, 8f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(4.5f, 14f, 6f),
            new VanillaCubeDefinition("left_hind_leg", 50, 22, -2f, 0f, -2f, 4f, 10f, 8f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3.5f, 14f, -8f),
            new VanillaCubeDefinition("right_front_leg", 50, 40, -2f, 0f, -2f, 4f, 10f, 6f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3.5f, 14f, -8f),
            new VanillaCubeDefinition("left_front_leg", 50, 40, -2f, 0f, -2f, 4f, 10f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
