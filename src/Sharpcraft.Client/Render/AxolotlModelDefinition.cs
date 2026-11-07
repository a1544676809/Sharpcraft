// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultAxolotlModel: body with a dorsal fin plane, head with top/left/right gills, four flat legs and a flat tail.
/// </summary>
/// <remarks>
/// Bind pose only. Variants (lucy/wild/gold/cyan/blue) are a synced integer, not a registry, and only swap the texture.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-686).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.axolotl.AxolotlModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/axolotl/AxolotlModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class AxolotlModelDefinition
{
    public const int AxolotlEntityTypeId = 7;

    private static readonly VanillaMeshDefinition AxolotlMesh = BuildAxolotlMesh();

    private static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(AxolotlMesh, 0, 0)];

    /// <summary>Synced variant ids: LUCY, WILD, GOLD, CYAN, BLUE.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["lucy", "wild", "gold", "cyan", "blue"];

    public static readonly EntityModelDefinition Definition = new(
        "axolotl",
        64,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/axolotl/axolotl_lucy");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["lucy"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "lucy")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "axolotl_" + key,
                64,
                64,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/axolotl/axolotl_" + key);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildAxolotlMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 19.5f, 5f),
            new VanillaCubeDefinition("body", 0, 11, -4f, -2f, -9f, 8f, 4f, 10f));
        body.AddOrReplaceChild("body_layer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_layer", 2, 17, 0f, -3f, -8f, 0f, 5f, 9f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, -9f),
            new VanillaCubeDefinition("head", 0, 1, -4f, -3f, -5f, 8f, 5f, 5f, Grow: 0.001f));
        head.AddOrReplaceChild("top_gills", VanillaPartPose.Offset(0f, -3f, -1f),
            new VanillaCubeDefinition("top_gills", 3, 37, -4f, -3f, 0f, 8f, 3f, 0f, Grow: 0.001f));
        head.AddOrReplaceChild("left_gills", VanillaPartPose.Offset(-4f, 0f, -1f),
            new VanillaCubeDefinition("left_gills", 0, 40, -3f, -5f, 0f, 3f, 7f, 0f, Grow: 0.001f));
        head.AddOrReplaceChild("right_gills", VanillaPartPose.Offset(4f, 0f, -1f),
            new VanillaCubeDefinition("right_gills", 11, 40, 0f, -5f, 0f, 3f, 7f, 0f, Grow: 0.001f));
        body.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3.5f, 1f, -1f),
            new VanillaCubeDefinition("right_hind_leg", 2, 13, -2f, 0f, 0f, 3f, 5f, 0f, Grow: 0.001f));
        body.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3.5f, 1f, -1f),
            new VanillaCubeDefinition("left_hind_leg", 2, 13, -1f, 0f, 0f, 3f, 5f, 0f, Grow: 0.001f));
        body.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3.5f, 1f, -8f),
            new VanillaCubeDefinition("right_front_leg", 2, 13, -2f, 0f, 0f, 3f, 5f, 0f, Grow: 0.001f));
        body.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3.5f, 1f, -8f),
            new VanillaCubeDefinition("left_front_leg", 2, 13, -1f, 0f, 0f, 3f, 5f, 0f, Grow: 0.001f));
        body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, 0f, 1f),
            new VanillaCubeDefinition("tail", 2, 19, 0f, -3f, 0f, 0f, 5f, 12f));
        return new VanillaMeshDefinition(root);
    }
}
