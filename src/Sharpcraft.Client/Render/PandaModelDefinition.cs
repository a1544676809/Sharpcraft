// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PandaModel: head with nose/ears, one large rotated body cube, and four 6x9x6 legs. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-684); variants are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.panda.PandaModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/panda/PandaModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PandaModelDefinition
{
    public const int PandaEntityTypeId = 97;

    /// <summary>Declarative DSL tree for the nine panda parts (T-684).</summary>
    private static readonly VanillaMeshDefinition PandaMesh = BuildPandaMesh();

    public static readonly EntityModelDefinition Definition = new(
        "panda",
        64,
        64,
        [.. VanillaMeshBake.Bake(PandaMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/panda/panda");

    public static readonly IReadOnlyList<string> VariantKeys = ["normal", "lazy", "worried", "playful", "brown", "weak", "aggressive"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["normal"] = Definition
        };
        foreach (var key in VariantKeys)
        {
            if (key != "normal")
            {
                variants[key] = new EntityModelDefinition(
                    "panda_" + key,
                    64,
                    64,
                    Definition.Parts,
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/panda/panda_" + key);
            }
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildPandaMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 11.5f, -17f),
            new VanillaCubeDefinition("head", 0, 6, -6.5f, -5f, -4f, 13f, 10f, 9f));
        root.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, 11.5f, -17f),
            new VanillaCubeDefinition("nose", 45, 16, -3.5f, 0f, -6f, 7f, 5f, 2f));
        root.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(0f, 11.5f, -17f),
            new VanillaCubeDefinition("left_ear", 52, 25, 3.5f, -8f, -1f, 5f, 4f, 1f));
        root.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(0f, 11.5f, -17f),
            new VanillaCubeDefinition("right_ear", 52, 25, -8.5f, -8f, -1f, 5f, 4f, 1f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 10f, 0f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 25, -9.5f, -13f, -6.5f, 19f, 26f, 13f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-5.5f, 15f, 9f),
            new VanillaCubeDefinition("right_hind_leg", 40, 0, -3f, 0f, -3f, 6f, 9f, 6f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(5.5f, 15f, 9f),
            new VanillaCubeDefinition("left_hind_leg", 40, 0, -3f, 0f, -3f, 6f, 9f, 6f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-5.5f, 15f, -9f),
            new VanillaCubeDefinition("right_front_leg", 40, 0, -3f, 0f, -3f, 6f, 9f, 6f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(5.5f, 15f, -9f),
            new VanillaCubeDefinition("left_front_leg", 40, 0, -3f, 0f, -3f, 6f, 9f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
