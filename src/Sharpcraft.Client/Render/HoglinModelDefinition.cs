// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// HoglinModel: a long body with a mane, a tilted head carrying ears and two horns, and four legs.
/// </summary>
/// <remarks>
/// Bind pose only. Zoglin reuses the geometry with its own texture. Headbutt/walk animations are not ported. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-681).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.hoglin.HoglinModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/hoglin/HoglinModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class HoglinModelDefinition
{
    public const int HoglinEntityTypeId = 65;
    public const int ZoglinEntityTypeId = 153;

    /// <summary>Declarative DSL tree for the eleven hoglin parts (T-681).</summary>
    private static readonly VanillaMeshDefinition HoglinMesh = BuildHoglinMesh();

    private static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(HoglinMesh, 0, 0)];

    public static readonly EntityModelDefinition Definition = new(
        "hoglin",
        128,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/hoglin/hoglin");

    public static readonly EntityModelDefinition Zoglin = new(
        "zoglin",
        128,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/hoglin/zoglin");

    private static VanillaMeshDefinition BuildHoglinMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 7f, 0f),
            new VanillaCubeDefinition("body", 1, 1, -8f, -7f, -13f, 16f, 14f, 26f));
        body.AddOrReplaceChild("mane", VanillaPartPose.Offset(0f, -14f, -7f),
            new VanillaCubeDefinition("mane", 90, 33, 0f, 0f, -9f, 0f, 10f, 19f, Grow: 0.001f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.OffsetAndRotation(0f, 2f, -12f, -0.87266463f, 0f, 0f),
            new VanillaCubeDefinition("head", 61, 1, -7f, -3f, -19f, 14f, 6f, 19f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-6f, -2f, -3f, 0f, 0f, -MathF.PI * 2f / 9f),
            new VanillaCubeDefinition("right_ear", 1, 1, -6f, -1f, -2f, 6f, 1f, 4f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(6f, -2f, -3f, 0f, 0f, MathF.PI * 2f / 9f),
            new VanillaCubeDefinition("left_ear", 1, 6, 0f, -1f, -2f, 6f, 1f, 4f));
        head.AddOrReplaceChild("right_horn", VanillaPartPose.Offset(-7f, 2f, -12f),
            new VanillaCubeDefinition("right_horn", 10, 13, -1f, -11f, -1f, 2f, 11f, 2f));
        head.AddOrReplaceChild("left_horn", VanillaPartPose.Offset(7f, 2f, -12f),
            new VanillaCubeDefinition("left_horn", 1, 13, -1f, -11f, -1f, 2f, 11f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-4f, 10f, -8.5f),
            new VanillaCubeDefinition("right_front_leg", 66, 42, -3f, 0f, -3f, 6f, 14f, 6f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(4f, 10f, -8.5f),
            new VanillaCubeDefinition("left_front_leg", 41, 42, -3f, 0f, -3f, 6f, 14f, 6f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-5f, 13f, 10f),
            new VanillaCubeDefinition("right_hind_leg", 21, 45, -2.5f, 0f, -2.5f, 5f, 11f, 5f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(5f, 13f, 10f),
            new VanillaCubeDefinition("left_hind_leg", 0, 45, -2.5f, 0f, -2.5f, 5f, 11f, 5f));
        return new VanillaMeshDefinition(root);
    }
}
