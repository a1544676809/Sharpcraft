// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SnowGolemModel: head, upper/lower body and two laterally rotated arms. The reference uses -0.5 cube deformation.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-692).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.golem.SnowGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/SnowGolemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SnowGolemModelDefinition
{
    public const int SnowGolemEntityTypeId = 124;

    private static readonly VanillaMeshDefinition SnowGolemMesh = BuildSnowGolemMesh();

    public static readonly EntityModelDefinition Definition = new(
        "snow_golem",
        64,
        64,
        [.. VanillaMeshBake.Bake(SnowGolemMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/snow_golem/snow_golem");

    private static VanillaMeshDefinition BuildSnowGolemMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 4f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: -0.5f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.OffsetAndRotation(5f, 6f, 1f, 0f, 0f, 1f),
            new VanillaCubeDefinition("left_arm", 32, 0, -1f, 0f, -1f, 12f, 2f, 2f, Grow: -0.5f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.OffsetAndRotation(-5f, 6f, -1f, 0f, MathF.PI, 1f),
            new VanillaCubeDefinition("right_arm", 32, 0, -1f, 0f, -1f, 12f, 2f, 2f, Grow: -0.5f));
        root.AddOrReplaceChild("upper_body", VanillaPartPose.Offset(0f, 13f, 0f),
            new VanillaCubeDefinition("upper_body", 0, 16, -5f, -10f, -5f, 10f, 10f, 10f, Grow: -0.5f));
        root.AddOrReplaceChild("lower_body", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("lower_body", 0, 36, -6f, -12f, -6f, 12f, 12f, 12f, Grow: -0.5f));
        return new VanillaMeshDefinition(root);
    }
}
