// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// LlamaModel: head/neck/ears, rotated body, two chest boxes and four 4x14x4 legs. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-685); the four coats are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.llama.LlamaModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/llama/LlamaModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class LlamaModelDefinition
{
    public const int LlamaEntityTypeId = 79;

    /// <summary>Declarative DSL tree for the eleven llama parts (T-685).</summary>
    private static readonly VanillaMeshDefinition LlamaMesh = BuildLlamaMesh();

    public static readonly EntityModelDefinition Definition = new(
        "llama",
        128,
        64,
        [.. VanillaMeshBake.Bake(LlamaMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/llama/llama_brown");

    /// <summary>Llama coat ids, in the <c>Llama.Variant</c> enum order used on the wire.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["creamy", "white", "brown", "gray"];

    /// <summary>Coat key to definition; the geometry is identical, only the texture changes.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["brown"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "brown")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "llama_" + key,
                128,
                64,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/llama/llama_" + key);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildLlamaMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 7f, -6f),
            new VanillaCubeDefinition("head", 0, 0, -2f, -14f, -10f, 4f, 4f, 9f));
        root.AddOrReplaceChild("neck", VanillaPartPose.Offset(0f, 7f, -6f),
            new VanillaCubeDefinition("neck", 0, 14, -4f, -16f, -6f, 8f, 18f, 6f));
        root.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(0f, 7f, -6f),
            new VanillaCubeDefinition("left_ear", 17, 0, -4f, -19f, -4f, 3f, 3f, 2f));
        root.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(0f, 7f, -6f),
            new VanillaCubeDefinition("right_ear", 17, 0, 1f, -19f, -4f, 3f, 3f, 2f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 5f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 29, 0, -6f, -10f, -7f, 12f, 18f, 10f));
        root.AddOrReplaceChild("right_chest", VanillaPartPose.OffsetAndRotation(-8.5f, 3f, 3f, 0f, -MathF.PI / 2f, 0f),
            new VanillaCubeDefinition("right_chest", 45, 28, -3f, 0f, 0f, 8f, 8f, 3f));
        root.AddOrReplaceChild("left_chest", VanillaPartPose.OffsetAndRotation(5.5f, 3f, 3f, 0f, -MathF.PI / 2f, 0f),
            new VanillaCubeDefinition("left_chest", 45, 41, -3f, 0f, 0f, 8f, 8f, 3f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3.5f, 10f, 6f),
            new VanillaCubeDefinition("right_hind_leg", 29, 29, -2f, 0f, -2f, 4f, 14f, 4f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3.5f, 10f, 6f),
            new VanillaCubeDefinition("left_hind_leg", 29, 29, -2f, 0f, -2f, 4f, 14f, 4f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3.5f, 10f, -5f),
            new VanillaCubeDefinition("right_front_leg", 29, 29, -2f, 0f, -2f, 4f, 14f, 4f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3.5f, 10f, -5f),
            new VanillaCubeDefinition("left_front_leg", 29, 29, -2f, 0f, -2f, 4f, 14f, 4f));
        return new VanillaMeshDefinition(root);
    }
}
