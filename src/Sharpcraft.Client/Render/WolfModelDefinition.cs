// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultWolfModel: real head with ears/snout, body + upper body, four 2x8x2 legs, and the real tail. Parts are declared
/// with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-684); WithCollar, the tame/angry
/// selector, additional paths and variants are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.wolf.WolfModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/wolf/WolfModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WolfModelDefinition
{
    public const int WolfEntityTypeId = 152;

    /// <summary>Declarative DSL tree for the thirteen wolf parts (T-684).</summary>
    private static readonly VanillaMeshDefinition WolfMesh = BuildWolfMesh();

    private static readonly CompiledEntityModel BaseCompiled =
        VanillaMeshBakery.Build(WolfMesh, 64, 32, texturePath: "entity/wolf/wolf");

    private static readonly CompiledEntityModel CompiledModel =
        EntityModelLayerHelpers.WithCollar(BaseCompiled, "entity/wolf/wolf_collar", inflate: 0f);

    public static readonly EntityModelDefinition Definition = new(
        "wolf",
        64,
        32,
        EntityModelLayerHelpers.WithCollar(
            [.. VanillaMeshBake.Bake(WolfMesh, 0, 0)],
            "entity/wolf/wolf_collar"),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wolf/wolf",
        textureSelector: state => state.IsTame
            ? "entity/wolf/wolf_tame"
            : state.IsAngry
                ? "entity/wolf/wolf_angry"
                : "entity/wolf/wolf",
        additionalTexturePaths: ["entity/wolf/wolf_tame", "entity/wolf/wolf_angry"],
        compiled: CompiledModel);

    /// <summary>Registry entry names of the wolf variants, in vanilla bootstrap order.</summary>
    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "pale", "spotted", "snowy", "black", "ashen", "rusty", "woods", "chestnut", "striped"
    ];

    private static readonly string[] VariantFiles =
    [
        "wolf", "wolf_spotted", "wolf_snowy", "wolf_black", "wolf_ashen", "wolf_rusty", "wolf_woods", "wolf_chestnut", "wolf_striped"
    ];

    /// <summary>Variant key to definition; texture follows the tame/angry state through the selector.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["pale"] = Definition
        };

        for (var i = 0; i < VariantKeys.Count; i++)
        {
            var key = VariantKeys[i];
            if (key == "pale")
            {
                continue;
            }

            var wild = "entity/wolf/" + VariantFiles[i];
            var tame = wild + "_tame";
            var angry = wild + "_angry";
            var captured = wild;
            variants[key] = new EntityModelDefinition(
                "wolf_" + key,
                64,
                32,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                captured,
                textureSelector: state => state.IsTame ? captured + "_tame" : state.IsAngry ? captured + "_angry" : captured,
                additionalTexturePaths: [tame, angry],
                compiled: CompiledModel);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildWolfMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(-1f, 13.5f, -7f),
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        head.AddOrReplaceChild("real_head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("real_head", 0, 0, -2f, -3f, -2f, 6f, 6f, 4f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 16, 14, -2f, -5f, 0f, 2f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 16, 14, 2f, -5f, 0f, 2f, 2f, 1f));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 0, 10, -0.5f, -0.001f, -5f, 3f, 3f, 4f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 14f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 18, 14, -3f, -2f, -3f, 6f, 9f, 6f));
        root.AddOrReplaceChild("upper_body", VanillaPartPose.OffsetAndRotation(-1f, 14f, -3f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("upper_body", 21, 0, -3f, -3f, -3f, 8f, 6f, 7f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.5f, 16f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 0, 18, 0f, 0f, -1f, 2f, 8f, 2f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(0.5f, 16f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 0, 18, 0f, 0f, -1f, 2f, 8f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.5f, 16f, -4f),
            new VanillaCubeDefinition("right_front_leg", 0, 18, 0f, 0f, -1f, 2f, 8f, 2f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(0.5f, 16f, -4f),
            new VanillaCubeDefinition("left_front_leg", 0, 18, 0f, 0f, -1f, 2f, 8f, 2f));
        var tail = root.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(-1f, 12f, 8f, -MathF.PI / 5f, 0f, 0f),
            new VanillaCubeDefinition("tail", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        tail.AddOrReplaceChild("real_tail", VanillaPartPose.Zero,
            new VanillaCubeDefinition("real_tail", 9, 18, 0f, 0f, -1f, 2f, 8f, 2f));
        return new VanillaMeshDefinition(root);
    }
}







