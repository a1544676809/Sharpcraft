// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultChickenModel: head with beak/red_thing, rotated body, two legs and two wings.
/// </summary>
/// <remarks>
/// Variants come from the <c>chicken_variant</c> registry (docs/analysis/render/entity-variants.md 3). "warm" keeps the
/// normal geometry; "cold" uses <c>ColdChickenModel</c> (a zero-width fin on the body and an extra head layer). Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-676).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.chicken.ChickenModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/chicken/ChickenModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ChickenModelDefinition
{
    public const int ChickenEntityTypeId = 26;

    /// <summary>Declarative DSL trees for the temperate/warm and cold chicken parts (T-676).</summary>
    private static readonly VanillaMeshDefinition NormalMesh = BuildNormalMesh();

    private static readonly EntityModelPart[] NormalParts = [.. VanillaMeshBake.Bake(NormalMesh, 0, 0)];

    private static readonly VanillaMeshDefinition ColdMesh = BuildColdMesh();

    private static readonly EntityModelPart[] ColdParts = [.. VanillaMeshBake.Bake(ColdMesh, 0, 0)];

    public static readonly EntityModelDefinition Definition = new(
        "chicken",
        64,
        32,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/chicken/chicken_temperate");

    /// <summary>Variant key to definition, keyed by the registry entry name.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new("chicken_warm", 64, 32, NormalParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_warm"),
            ["cold"] = new("chicken_cold", 64, 32, ColdParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_cold")
        };

    private static VanillaMeshDefinition BuildNormalMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        AddHeadParts(root);
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 16f, 0f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 9, -3f, -4f, -3f, 6f, 8f, 6f));
        AddLegsAndWings(root);
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildColdMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        AddHeadParts(root);
        root.AddOrReplaceChild("cold_head_layer", VanillaPartPose.Offset(0f, 15f, -4f),
            new VanillaCubeDefinition("cold_head_layer", 44, 0, -3f, -7f, -2.015f, 6f, 3f, 4f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 16f, 0f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 9, -3f, -4f, -3f, 6f, 8f, 6f));
        root.AddOrReplaceChild("cold_body_fin", VanillaPartPose.OffsetAndRotation(0f, 16f, 0f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("cold_body_fin", 38, 9, 0f, 3f, -1f, 0f, 3f, 5f));
        AddLegsAndWings(root);
        return new VanillaMeshDefinition(root);
    }

    /// <summary>Shared head, beak and red_thing boxes (cold adds its head layer after these).</summary>
    private static void AddHeadParts(VanillaPartDefinition root)
    {
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, -4f),
            new VanillaCubeDefinition("head", 0, 0, -2f, -6f, -2f, 4f, 6f, 3f));
        root.AddOrReplaceChild("beak", VanillaPartPose.Offset(0f, 15f, -4f),
            new VanillaCubeDefinition("beak", 14, 0, -2f, -4f, -4f, 4f, 2f, 2f));
        root.AddOrReplaceChild("red_thing", VanillaPartPose.Offset(0f, 15f, -4f),
            new VanillaCubeDefinition("red_thing", 14, 4, -1f, -2f, -3f, 2f, 2f, 2f));
    }

    /// <summary>Shared two legs and two wings (the cold variant keeps them identical).</summary>
    private static void AddLegsAndWings(VanillaPartDefinition root)
    {
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 19f, 1f),
            new VanillaCubeDefinition("right_leg", 26, 0, -1f, 0f, -3f, 3f, 5f, 3f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1f, 19f, 1f),
            new VanillaCubeDefinition("left_leg", 26, 0, -1f, 0f, -3f, 3f, 5f, 3f));
        root.AddOrReplaceChild("right_wing", VanillaPartPose.Offset(-4f, 13f, 0f),
            new VanillaCubeDefinition("right_wing", 24, 13, 0f, 0f, -3f, 1f, 4f, 6f));
        root.AddOrReplaceChild("left_wing", VanillaPartPose.Offset(4f, 13f, 0f),
            new VanillaCubeDefinition("left_wing", 24, 13, -1f, 0f, -3f, 1f, 4f, 6f, Mirror: true));
    }
}
