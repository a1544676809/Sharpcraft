// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ParrotModel: body, tail, two wings, a head with crest/beak parts, and two small legs. Poses/animations are not ported.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-693); the
/// legacy flat order is restored after baking.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.parrot.ParrotModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/parrot/ParrotModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ParrotModelDefinition
{
    public const int ParrotEntityTypeId = 99;

    private static readonly VanillaMeshDefinition ParrotMesh = BuildParrotMesh();

    private static readonly EntityModelPart[] Parts = BuildParts();

    private static readonly CompiledEntityModel ParrotCompiled =
        VanillaMeshBakery.Build(ParrotMesh, 32, 32);

    /// <summary>Synced variant ids in enum order: RED_BLUE, BLUE, GREEN, YELLOW_BLUE, GRAY.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["red_blue", "blue", "green", "yellow_blue", "gray"];

    private static readonly string[] VariantFiles = ["parrot_red_blue", "parrot_blue", "parrot_green", "parrot_yellow_blue", "parrot_grey"];

    public static readonly EntityModelDefinition Definition = new(
        "parrot",
        32,
        32,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/parrot/parrot_red_blue",
        compiled: ParrotCompiled);

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["red_blue"] = Definition
        };

        for (var i = 0; i < VariantKeys.Count; i++)
        {
            if (VariantKeys[i] == "red_blue")
            {
                continue;
            }

            variants[VariantKeys[i]] = new EntityModelDefinition(
                "parrot_" + VariantKeys[i],
                32,
                32,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/parrot/" + VariantFiles[i],
                compiled: ParrotCompiled);
        }

        return variants;
    }

    private static EntityModelPart[] BuildParts()
    {
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in VanillaMeshBake.Bake(ParrotMesh, 0, 0))
        {
            byName[part.Name] = part;
        }

        return
        [
            byName["body"], byName["tail"], byName["left_wing"], byName["right_wing"],
            byName["head"], byName["head2"], byName["beak1"], byName["beak2"], byName["feather"],
            byName["left_leg"], byName["right_leg"]
        ];
    }

    private static VanillaMeshDefinition BuildParrotMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 16.5f, -3f, -0.4937f, 0f, 0f),
            new VanillaCubeDefinition("body", 2, 8, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(0f, 21.07f, 1.16f, -1.015f, 0f, 0f),
            new VanillaCubeDefinition("tail", 22, 1, -1.5f, -1f, -1f, 3f, 4f, 1f));
        root.AddOrReplaceChild("left_wing", VanillaPartPose.OffsetAndRotation(1.5f, 16.94f, -2.76f, 0.6981f, MathF.PI, 0f),
            new VanillaCubeDefinition("left_wing", 19, 8, -0.5f, 0f, -1.5f, 1f, 5f, 3f));
        root.AddOrReplaceChild("right_wing", VanillaPartPose.OffsetAndRotation(-1.5f, 16.94f, -2.76f, 0.6981f, MathF.PI, 0f),
            new VanillaCubeDefinition("right_wing", 19, 8, -0.5f, 0f, -1.5f, 1f, 5f, 3f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15.69f, -2.76f),
            new VanillaCubeDefinition("head", 2, 2, -1f, -1.5f, -1f, 2f, 3f, 2f));
        head.AddOrReplaceChild("head2", VanillaPartPose.Offset(0f, -2f, -1f),
            new VanillaCubeDefinition("head2", 10, 0, -1f, -0.5f, -2f, 2f, 1f, 4f));
        head.AddOrReplaceChild("beak1", VanillaPartPose.Offset(0f, -0.5f, -1.5f),
            new VanillaCubeDefinition("beak1", 11, 7, -0.5f, -1f, -0.5f, 1f, 2f, 1f));
        head.AddOrReplaceChild("beak2", VanillaPartPose.Offset(0f, -1.75f, -2.45f),
            new VanillaCubeDefinition("beak2", 16, 7, -0.5f, 0f, -0.5f, 1f, 2f, 1f));
        head.AddOrReplaceChild("feather", VanillaPartPose.OffsetAndRotation(0f, -2.15f, 0.15f, -0.2214f, 0f, 0f),
            new VanillaCubeDefinition("feather", 2, 18, 0f, -4f, -2f, 0f, 5f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.OffsetAndRotation(1f, 22f, -1.05f, 0.0299f, 0f, 0f),
            new VanillaCubeDefinition("left_leg", 14, 18, -0.5f, 0f, -0.5f, 1f, 2f, 1f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.OffsetAndRotation(-1f, 22f, -1.05f, 0.0299f, 0f, 0f),
            new VanillaCubeDefinition("right_leg", 14, 18, -0.5f, 0f, -0.5f, 1f, 2f, 1f));
        return new VanillaMeshDefinition(root);
    }
}
