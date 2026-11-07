// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultFelineModel used by AdultCatModel: head with nose/ears, rotated body, two tail segments and four legs.
/// AdultCatModel applies CAT_TRANSFORMER scale 0.8.
/// </summary>
/// <remarks>
/// Cat variants only change the texture (docs/analysis/render/entity-variants.md 3); the registry entry name is the
/// suffix of <c>entity/cat/cat_&lt;name&gt;</c>. Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked
/// via <c>VanillaMeshBake.Bake</c> (T-684); WithCollar, variants and scale are unchanged.
/// </remarks>
public static class CatModelDefinition
{
    public const int CatEntityTypeId = 21;
    public const int OcelotEntityTypeId = 92;

    /// <summary>Declarative DSL tree for the eleven cat parts (T-684).</summary>
    private static readonly VanillaMeshDefinition CatMesh = BuildCatMesh();

    private static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(CatMesh, 0, 0)];

    private static readonly EntityModelPart[] CollarParts = EntityModelLayerHelpers.WithCollar(Parts, "entity/cat/cat_collar");

    private static readonly CompiledEntityModel BaseCompiled =
        VanillaMeshBakery.Build(CatMesh, 64, 32, texturePath: "entity/cat/cat_tabby");

    private static readonly CompiledEntityModel CompiledModel =
        EntityModelLayerHelpers.WithCollar(BaseCompiled, "entity/cat/cat_collar", inflate: 0.01f);

    public static readonly EntityModelDefinition Definition = new(
        "cat",
        64,
        32,
        CollarParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cat/cat_tabby",
        0.8f,
        compiled: CompiledModel);

    public static readonly EntityModelDefinition Ocelot = new(
        "ocelot",
        64,
        32,
        CollarParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cat/ocelot",
        compiled: CompiledModel);

    /// <summary>Registry entry names, in the vanilla bootstrap order used when no registry data is available.</summary>
    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "tabby", "black", "red", "siamese", "british_shorthair",
        "calico", "persian", "ragdoll", "white", "jellie", "all_black"
    ];

    /// <summary>Variant key to definition, keyed by the registry entry name.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["tabby"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "tabby")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "cat_" + key,
                64,
                32,
                CollarParts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/cat/cat_" + key,
                0.8f,
                compiled: CompiledModel);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildCatMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head_main", VanillaPartPose.Offset(0f, 15f, -9f),
            new VanillaCubeDefinition("head_main", 0, 0, -2.5f, -2f, -3f, 5f, 4f, 5f));
        root.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, 15f, -9f),
            new VanillaCubeDefinition("nose", 0, 24, -1.5f, -0.001f, -4f, 3f, 2f, 2f));
        root.AddOrReplaceChild("ear1", VanillaPartPose.Offset(0f, 15f, -9f),
            new VanillaCubeDefinition("ear1", 0, 10, -2f, -3f, 0f, 1f, 1f, 2f));
        root.AddOrReplaceChild("ear2", VanillaPartPose.Offset(0f, 15f, -9f),
            new VanillaCubeDefinition("ear2", 6, 10, 1f, -3f, 0f, 1f, 1f, 2f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 12f, -10f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 20, 0, -2f, 3f, -8f, 4f, 16f, 6f));
        root.AddOrReplaceChild("tail1", VanillaPartPose.OffsetAndRotation(0f, 15f, 8f, -0.9f, 0f, 0f),
            new VanillaCubeDefinition("tail1", 0, 15, -0.5f, 0f, 0f, 1f, 8f, 1f));
        root.AddOrReplaceChild("tail2", VanillaPartPose.Offset(0f, 20f, 14f),
            new VanillaCubeDefinition("tail2", 4, 15, -0.5f, 0f, 0f, 1f, 8f, 1f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1.1f, 18f, 5f),
            new VanillaCubeDefinition("left_hind_leg", 8, 13, -1f, 0f, 1f, 2f, 6f, 2f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-1.1f, 18f, 5f),
            new VanillaCubeDefinition("right_hind_leg", 8, 13, -1f, 0f, 1f, 2f, 6f, 2f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(1.2f, 14.1f, -5f),
            new VanillaCubeDefinition("left_front_leg", 40, 0, -1f, 0f, 0f, 2f, 10f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-1.2f, 14.1f, -5f),
            new VanillaCubeDefinition("right_front_leg", 40, 0, -1f, 0f, 0f, 2f, 10f, 2f));
        return new VanillaMeshDefinition(root);
    }
}



