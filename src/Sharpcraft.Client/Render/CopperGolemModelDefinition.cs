// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CopperGolemModel: a small humanoid with body, head (face + nose + two antenna boxes), two arms and two legs.
/// Four weathering states swap both the body texture and the emissive eye texture. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-692).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.golem.CopperGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/CopperGolemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CopperGolemModelDefinition
{
    public const int CopperGolemEntityTypeId = 28;

    private static readonly VanillaMeshDefinition CopperGolemMesh = BuildCopperGolemMesh();

    /// <summary>Weathering states in registry order: UNAFFECTED, EXPOSED, WEATHERED, OXIDIZED.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["unaffected", "exposed", "weathered", "oxidized"];

    private static readonly string[] BodyFiles = ["copper_golem", "copper_golem_exposed", "copper_golem_weathered", "copper_golem_oxidized"];
    private static readonly string[] EyeFiles = ["copper_golem_eyes", "copper_golem_eyes_exposed", "copper_golem_eyes_weathered", "copper_golem_eyes_oxidized"];

    public static readonly EntityModelDefinition Definition = Create("copper_golem", 0);

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static EntityModelDefinition Create(string name, int index) => EntityModelDefinition.FromTree(
        name,
        64,
        64,
        CopperGolemMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/copper_golem/" + BodyFiles[index],
        emissiveTexturePath: "entity/copper_golem/" + EyeFiles[index],
        additionalTexturePaths: ["entity/copper_golem/" + EyeFiles[index]]);

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal);
        for (var i = 0; i < VariantKeys.Count; i++)
        {
            variants[VariantKeys[i]] = i == 0 ? Definition : Create("copper_golem_" + VariantKeys[i], i);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildCopperGolemMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("body", 0, 15, -4f, -6f, -3f, 8f, 6f, 6f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -6f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -5f, -5f, 8f, 5f, 10f, Grow: 0.015f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nose", 56, 0, -1f, -2f, -6f, 2f, 3f, 2f));
        head.AddOrReplaceChild("antenna", VanillaPartPose.Zero,
            new VanillaCubeDefinition("antenna", 37, 8, -1f, -9f, -1f, 2f, 4f, 2f, Grow: -0.015f));
        head.AddOrReplaceChild("antenna_tip", VanillaPartPose.Zero,
            new VanillaCubeDefinition("antenna_tip", 37, 0, -2f, -13f, -2f, 4f, 4f, 4f, Grow: -0.015f));
        body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-4f, -6f, 0f),
            new VanillaCubeDefinition("right_arm", 36, 16, -3f, -1f, -2f, 3f, 10f, 4f));
        body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(4f, -6f, 0f),
            new VanillaCubeDefinition("left_arm", 50, 16, 0f, -1f, -2f, 3f, 10f, 4f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 27, -4f, 0f, -2f, 4f, 5f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("left_leg", 16, 27, 0f, 0f, -2f, 4f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }
}
