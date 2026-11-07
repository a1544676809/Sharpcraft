// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// IronGolemModel: root head/body cubes, two long arms and two legs. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-692).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.golem.IronGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/IronGolemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class IronGolemModelDefinition
{
    public const int IronGolemEntityTypeId = 71;

    private static readonly VanillaMeshDefinition IronGolemMesh = BuildIronGolemMesh();

    public static readonly EntityModelDefinition Definition = new(
        "iron_golem",
        128,
        128,
        [.. VanillaMeshBake.Bake(IronGolemMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/iron_golem/iron_golem");

    private static VanillaMeshDefinition BuildIronGolemMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -7f, -2f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -12f, -5.5f, 8f, 10f, 8f));
        root.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -7f, -2f),
            new VanillaCubeDefinition("nose", 24, 0, -1f, -5f, -7.5f, 2f, 4f, 2f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -7f, 0f),
            new VanillaCubeDefinition("body", 0, 40, -9f, -2f, -6f, 18f, 12f, 11f));
        root.AddOrReplaceChild("lower_body", VanillaPartPose.Offset(0f, -7f, 0f),
            new VanillaCubeDefinition("lower_body", 0, 70, -4.5f, 10f, -3f, 9f, 5f, 6f, Grow: 0.5f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(0f, -7f, 0f),
            new VanillaCubeDefinition("right_arm", 60, 21, -13f, -2.5f, -3f, 4f, 30f, 6f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(0f, -7f, 0f),
            new VanillaCubeDefinition("left_arm", 60, 58, 9f, -2.5f, -3f, 4f, 30f, 6f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-4f, 11f, 0f),
            new VanillaCubeDefinition("right_leg", 37, 0, -3.5f, -3f, -3f, 6f, 16f, 5f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(5f, 11f, 0f),
            new VanillaCubeDefinition("left_leg", 60, 0, -3.5f, -3f, -3f, 6f, 16f, 5f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
