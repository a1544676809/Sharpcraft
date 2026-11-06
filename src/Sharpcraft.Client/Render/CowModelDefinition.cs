// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CowModel / QuadrupedModel with legSize 12: head with snout and horns, rotated body with udder, four legs.
/// </summary>
/// <remarks>
/// Variants come from the <c>cow_variant</c> registry (docs/analysis/render/entity-variants.md 3). "warm" replaces the
/// head with <c>WarmCowModel</c> (four extra ear/horn boxes, two mirrored); "cold" uses <c>ColdCowModel</c> (0.5-pixel
/// outer body layer, snout UV 9,33 and two rotated horn children). The head has no rotation, so its children collapse
/// to plain pivot offsets in this flat model. Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked
/// via <c>VanillaMeshBake.Bake</c> (T-676); the four legs are shared by all three trees through <c>AddLegs</c>.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.cow.CowModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/cow/CowModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CowModelDefinition
{
    public const int CowEntityTypeId = 30;
    public const int MooshroomEntityTypeId = 87;

    /// <summary>Declarative DSL trees for the temperate/warm/cold cow parts (T-676).</summary>
    private static readonly VanillaMeshDefinition NormalMesh = BuildNormalMesh();

    private static readonly EntityModelPart[] NormalParts = [.. VanillaMeshBake.Bake(NormalMesh, 0, 0)];

    private static readonly VanillaMeshDefinition WarmMesh = BuildWarmMesh();

    private static readonly EntityModelPart[] WarmParts = [.. VanillaMeshBake.Bake(WarmMesh, 0, 0)];

    private static readonly VanillaMeshDefinition ColdMesh = BuildColdMesh();

    private static readonly EntityModelPart[] ColdParts = [.. VanillaMeshBake.Bake(ColdMesh, 0, 0)];

    public static readonly EntityModelDefinition Definition = new(
        "cow",
        64,
        64,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cow/cow_temperate");

    public static readonly EntityModelDefinition Mooshroom = new(
        "mooshroom",
        64,
        64,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cow/mooshroom_red");

    /// <summary>Variant key to definition, keyed by the registry entry name.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new("cow_warm", 64, 64, WarmParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_warm"),
            ["cold"] = new("cow_cold", 64, 64, ColdParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_cold")
        };

    private static VanillaMeshDefinition BuildNormalMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -4f, -6f, 8f, 8f, 6f));
        root.AddOrReplaceChild("snout", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("snout", 1, 33, -3f, 1f, -7f, 6f, 3f, 1f));
        root.AddOrReplaceChild("right_horn", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("right_horn", 22, 0, -5f, -5f, -5f, 1f, 3f, 1f));
        root.AddOrReplaceChild("left_horn", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("left_horn", 22, 0, 4f, -5f, -5f, 1f, 3f, 1f));
        AddBodyAndUdder(root, null);
        AddLegs(root);
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildWarmMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -4f, -6f, 8f, 8f, 6f));
        root.AddOrReplaceChild("snout", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("snout", 1, 33, -3f, 1f, -7f, 6f, 3f, 1f));
        root.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("right_ear", 27, 0, -8f, -3f, -5f, 4f, 2f, 2f));
        root.AddOrReplaceChild("right_ear_tip", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("right_ear_tip", 39, 0, -8f, -5f, -5f, 2f, 2f, 2f));
        root.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("left_ear", 27, 0, 4f, -3f, -5f, 4f, 2f, 2f, Mirror: true));
        root.AddOrReplaceChild("left_ear_tip", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("left_ear_tip", 39, 0, 6f, -5f, -5f, 2f, 2f, 2f, Mirror: true));
        AddBodyAndUdder(root, null);
        AddLegs(root);
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildColdMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -4f, -6f, 8f, 8f, 6f));
        root.AddOrReplaceChild("snout", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("snout", 9, 33, -3f, 1f, -7f, 6f, 3f, 1f));
        root.AddOrReplaceChild("right_horn", VanillaPartPose.OffsetAndRotation(-4.5f, 1.5f, -11.5f, -1.5708f, 0f, 0f),
            new VanillaCubeDefinition("right_horn", 0, 40, -1.5f, -4.5f, -0.5f, 2f, 6f, 2f));
        root.AddOrReplaceChild("left_horn", VanillaPartPose.OffsetAndRotation(5.5f, 1.5f, -13f, -1.5708f, 0f, 0f),
            new VanillaCubeDefinition("left_horn", 0, 32, -1.5f, -3f, -0.5f, 2f, 6f, 2f));
        AddBodyAndUdder(root, [new("cold_cow_layer", 0.5f, 20, 32)]);
        AddLegs(root);
        return new VanillaMeshDefinition(root);
    }

    /// <summary>Shared rotated body + udder; the cold variant supplies the 0.5-pixel outer body overlay (UV 20,32).</summary>
    private static void AddBodyAndUdder(VanillaPartDefinition root, IReadOnlyList<EntityModelLayer>? bodyOverlays)
    {
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 5f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 18, 4, -6f, -10f, -7f, 12f, 18f, 10f, Overlays: bodyOverlays));
        root.AddOrReplaceChild("udder", VanillaPartPose.OffsetAndRotation(0f, 5f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("udder", 52, 0, -2f, 2f, -8f, 4f, 6f, 1f));
    }

    /// <summary>The four legs shared by every cow variant (the former <c>Legs</c> array).</summary>
    private static void AddLegs(VanillaPartDefinition root)
    {
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-4f, 12f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(4f, 12f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-4f, 12f, -5f),
            new VanillaCubeDefinition("right_front_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(4f, 12f, -5f),
            new VanillaCubeDefinition("left_front_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
    }
}
