// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// FrogModel: empty "root" pivot carrying the body, head with eyes, mouth parts and both arms, plus the two hind legs.
/// </summary>
/// <remarks>
/// Bind pose only. Frog variants are registry entries that pick a texture (temperate/warm/cold); the croaking body is a
/// static part here because the croak animation is not ported. Parts are declared with the <c>VanillaMeshDefinition</c>
/// DSL and baked via <c>VanillaMeshBake.Bake</c> (T-677); the empty root pivot and zero-size pivot groups are preserved.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.frog.FrogModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/FrogModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class FrogModelDefinition
{
    public const int FrogEntityTypeId = 56;

    /// <summary>Declarative DSL tree for the nested frog parts (T-677).</summary>
    private static readonly VanillaMeshDefinition FrogMesh = BuildFrogMesh();

    private static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(FrogMesh, 0, 0)];

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "frog",
        48,
        48,
        FrogMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/frog/frog_temperate");

    /// <summary>Registry entry names of the frog variants, in vanilla bootstrap order.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["temperate", "warm", "cold"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "temperate")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "frog_" + key,
                48,
                48,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/frog/frog_" + key,
                compiled: Definition.Compiled);
        }

        return variants;
    }

    /// <summary>
    /// The empty root pivot carries body (with its own nested head/eyes/arms chain) and the two hind legs. Zero-size
    /// cubes are pure pivot groups; pre-order baking reproduces the original flat part order and parent links.
    /// </summary>
    private static VanillaMeshDefinition BuildFrogMesh()
    {
        var container = new VanillaPartDefinition("container", VanillaPartPose.Zero);
        var root = container.AddOrReplaceChild("root", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -2f, 4f),
            new VanillaCubeDefinition("body", 3, 1, -3.5f, -2f, -8f, 7f, 3f, 9f));
        body.AddOrReplaceChild("body_layer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_layer", 23, 22, -3.5f, -1f, -8f, 7f, 0f, 9f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -2f, -1f),
            new VanillaCubeDefinition("head", 0, 13, -3.5f, -2f, -7f, 7f, 3f, 9f));
        head.AddOrReplaceChild("head_layer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_layer", 23, 13, -3.5f, -1f, -7f, 7f, 0f, 9f));
        var eyes = head.AddOrReplaceChild("eyes", VanillaPartPose.Offset(-0.5f, 0f, 2f),
            new VanillaCubeDefinition("eyes", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        eyes.AddOrReplaceChild("right_eye", VanillaPartPose.Offset(-1.5f, -3f, -6.5f),
            new VanillaCubeDefinition("right_eye", 0, 0, -1.5f, -1f, -1.5f, 3f, 2f, 3f));
        eyes.AddOrReplaceChild("left_eye", VanillaPartPose.Offset(2.5f, -3f, -6.5f),
            new VanillaCubeDefinition("left_eye", 0, 5, -1.5f, -1f, -1.5f, 3f, 2f, 3f));
        body.AddOrReplaceChild("croaking_body", VanillaPartPose.Offset(0f, -1f, -5f),
            new VanillaCubeDefinition("croaking_body", 26, 5, -3.5f, -0.1f, -2.9f, 7f, 2f, 3f, Grow: -0.1f));
        body.AddOrReplaceChild("tongue", VanillaPartPose.Offset(0f, -1.01f, 1f),
            new VanillaCubeDefinition("tongue", 17, 13, -2f, 0f, -7.1f, 4f, 0f, 7f));
        var leftArm = body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(4f, -1f, -6.5f),
            new VanillaCubeDefinition("left_arm", 0, 32, -1f, 0f, -1f, 2f, 3f, 3f));
        leftArm.AddOrReplaceChild("left_hand", VanillaPartPose.Offset(0f, 3f, -1f),
            new VanillaCubeDefinition("left_hand", 18, 40, -4f, 0.01f, -4f, 8f, 0f, 8f));
        var rightArm = body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-4f, -1f, -6.5f),
            new VanillaCubeDefinition("right_arm", 0, 38, -1f, 0f, -1f, 2f, 3f, 3f));
        rightArm.AddOrReplaceChild("right_hand", VanillaPartPose.Offset(0f, 3f, 0f),
            new VanillaCubeDefinition("right_hand", 2, 40, -4f, 0.01f, -5f, 8f, 0f, 8f));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(3.5f, -3f, 4f),
            new VanillaCubeDefinition("left_leg", 14, 25, -1f, 0f, -2f, 3f, 3f, 4f));
        leftLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Offset(2f, 3f, 0f),
            new VanillaCubeDefinition("left_foot", 2, 32, -4f, 0.01f, -4f, 8f, 0f, 8f));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-3.5f, -3f, 4f),
            new VanillaCubeDefinition("right_leg", 0, 25, -2f, 0f, -2f, 3f, 3f, 4f));
        rightLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Offset(-2f, 3f, 0f),
            new VanillaCubeDefinition("right_foot", 18, 32, -4f, 0.01f, -4f, 8f, 0f, 8f));
        return new VanillaMeshDefinition(container);
    }
}
