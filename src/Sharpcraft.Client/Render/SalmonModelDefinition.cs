// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SalmonModel: two body segments with fins and a head. Three synced size variants scale the same geometry 0.5/1.0/1.5.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-680).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fish.SalmonModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/SalmonModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SalmonModelDefinition
{
    public const int SalmonEntityTypeId = 113;

    /// <summary>Declarative DSL tree for the eight salmon parts (T-680).</summary>
    private static readonly VanillaMeshDefinition SalmonMesh = BuildSalmonMesh();

    private static readonly EntityModelPart[] Parts = BuildParts();

    private static readonly CompiledEntityModel SalmonCompiled =
        VanillaMeshBakery.Build(SalmonMesh, 32, 32);

    /// <summary>
    /// The reference lists the three root body parts before the child fins, while <c>Bake</c> emits children
    /// depth-first. Reorder the baked parts by name (values untouched) to keep the flat array order identical.
    /// </summary>
    private static EntityModelPart[] BuildParts()
    {
        var baked = VanillaMeshBake.Bake(SalmonMesh, 0, 0);
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in baked)
        {
            byName[part.Name] = part;
        }

        return
        [
            byName["body_front"], byName["body_back"], byName["head"],
            byName["back_fin"], byName["top_front_fin"], byName["top_back_fin"],
            byName["right_fin"], byName["left_fin"]
        ];
    }

    private static readonly float[] VariantScale = [0.5f, 1f, 1.5f];

    /// <summary>Synced variant ids: SMALL, MEDIUM, LARGE.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["small", "medium", "large"];

    public static readonly EntityModelDefinition Definition = new(
        "salmon",
        32,
        32,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fish/salmon",
        compiled: SalmonCompiled);

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal);
        for (var i = 0; i < VariantKeys.Count; i++)
        {
            var key = VariantKeys[i];
            variants[key] = key == "medium"
                ? Definition
                : new EntityModelDefinition(
                    "salmon_" + key,
                    32,
                    32,
                    Parts,
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/fish/salmon",
                    VariantScale[i],
                    compiled: SalmonCompiled);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildSalmonMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var bodyFront = root.AddOrReplaceChild("body_front", VanillaPartPose.Offset(0f, 20f, -7.2f),
            new VanillaCubeDefinition("body_front", 0, 0, -1.5f, -2.5f, 0f, 3f, 5f, 8f));
        bodyFront.AddOrReplaceChild("top_front_fin", VanillaPartPose.Offset(0f, -4.5f, 5f),
            new VanillaCubeDefinition("top_front_fin", 2, 1, 0f, 0f, 0f, 0f, 2f, 3f));
        var bodyBack = root.AddOrReplaceChild("body_back", VanillaPartPose.Offset(0f, 20f, 0.8f),
            new VanillaCubeDefinition("body_back", 0, 13, -1.5f, -2.5f, 0f, 3f, 5f, 8f));
        bodyBack.AddOrReplaceChild("back_fin", VanillaPartPose.Offset(0f, 0f, 8f),
            new VanillaCubeDefinition("back_fin", 20, 10, 0f, -2.5f, 0f, 0f, 5f, 6f));
        bodyBack.AddOrReplaceChild("top_back_fin", VanillaPartPose.Offset(0f, -4.5f, -1f),
            new VanillaCubeDefinition("top_back_fin", 0, 2, 0f, 0f, 0f, 0f, 2f, 4f));
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 20f, -7.2f),
            new VanillaCubeDefinition("head", 22, 0, -1f, -2f, -3f, 2f, 4f, 3f));
        root.AddOrReplaceChild("right_fin", VanillaPartPose.OffsetAndRotation(-1.5f, 21.5f, -7.2f, 0f, 0f, MathF.PI / 4f),
            new VanillaCubeDefinition("right_fin", 28, 0, -2f, 0f, 0f, 2f, 0f, 2f));
        root.AddOrReplaceChild("left_fin", VanillaPartPose.OffsetAndRotation(1.5f, 21.5f, -7.2f, 0f, 0f, -MathF.PI / 4f),
            new VanillaCubeDefinition("left_fin", 0, 0, 0f, 0f, 0f, 2f, 0f, 2f));
        return new VanillaMeshDefinition(root);
    }
}
