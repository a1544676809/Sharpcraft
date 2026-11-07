// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Extra boxes that the reference's equipment-layer models add on top of the entity model (saddle, harness, armor
/// shells). Base-model copies remain responsible for painting the equipment texture over the body. Parts are declared
/// with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-703); fake parent nodes preserve
/// the original <c>Parent</c> links without leaking into the returned arrays.
/// </summary>
public static class EquipmentModelParts
{
    public static IReadOnlyList<EntityModelPart> For(EquipmentAssetKind kind) => kind switch
    {
        EquipmentAssetKind.HorseSaddle => HorseSaddle,
        EquipmentAssetKind.CamelSaddle => CamelSaddle,
        EquipmentAssetKind.NautilusBody => NautilusArmor,
        EquipmentAssetKind.NautilusSaddle => NautilusSaddle,
        EquipmentAssetKind.HappyGhastBody => HappyHarness,
        _ => []
    };

    private static readonly EntityModelPart[] HorseSaddle = BuildFiltered(BuildHorseSaddleMesh(), "body", "head_parts");
    private static readonly EntityModelPart[] CamelSaddle = BuildFiltered(BuildCamelSaddleMesh(), "body", "head");
    private static readonly EntityModelPart[] NautilusArmor = BuildFiltered(BuildNautilusArmorMesh(), "root");
    private static readonly EntityModelPart[] NautilusSaddle = BuildFiltered(BuildNautilusSaddleMesh(), "root");
    private static readonly EntityModelPart[] HappyHarness = BuildFiltered(BuildHappyHarnessMesh());

    private static EntityModelPart[] BuildFiltered(VanillaMeshDefinition mesh, params string[] fakeParentNames)
    {
        var excluded = fakeParentNames.ToHashSet(StringComparer.Ordinal);
        var result = new List<EntityModelPart>();
        foreach (var part in VanillaMeshBake.Bake(mesh, 0, 0))
        {
            if (!excluded.Contains(part.Name))
            {
                result.Add(part);
            }
        }

        return [.. result];
    }

    private static VanillaMeshDefinition BuildHorseSaddleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        body.AddOrReplaceChild("saddle_model", VanillaPartPose.Zero,
            new VanillaCubeDefinition("saddle_model", 26, 0, -5f, -8f, -9f, 10f, 9f, 9f, Grow: 0.5f));
        var headParts = root.AddOrReplaceChild("head_parts", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_parts", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        headParts.AddOrReplaceChild("left_saddle_mouth", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_saddle_mouth", 29, 5, 2f, -9f, -6f, 1f, 2f, 2f));
        headParts.AddOrReplaceChild("right_saddle_mouth", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_saddle_mouth", 29, 5, -3f, -9f, -6f, 1f, 2f, 2f));
        headParts.AddOrReplaceChild("left_saddle_line", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, -MathF.PI / 6f, 0f, 0f),
            new VanillaCubeDefinition("left_saddle_line", 32, 2, 3.1f, -6f, -8f, 0f, 3f, 16f));
        headParts.AddOrReplaceChild("right_saddle_line", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, -MathF.PI / 6f, 0f, 0f),
            new VanillaCubeDefinition("right_saddle_line", 32, 2, -3.1f, -6f, -8f, 0f, 3f, 16f));
        headParts.AddOrReplaceChild("head_saddle", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_saddle", 1, 1, -3f, -11f, -1.9f, 6f, 5f, 6f, Grow: 0.22f));
        headParts.AddOrReplaceChild("mouth_saddle_wrap", VanillaPartPose.Zero,
            new VanillaCubeDefinition("mouth_saddle_wrap", 19, 0, -2f, -11f, -4f, 4f, 5f, 2f, Grow: 0.2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildCamelSaddleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        body.AddOrReplaceChild("saddle_lower", VanillaPartPose.Zero,
            new VanillaCubeDefinition("saddle_lower", 74, 64, -4.5f, -17f, -15.5f, 9f, 5f, 11f, Grow: 0.05f));
        body.AddOrReplaceChild("saddle_upper", VanillaPartPose.Zero,
            new VanillaCubeDefinition("saddle_upper", 92, 114, -3.5f, -20f, -15.5f, 7f, 3f, 11f, Grow: 0.05f));
        body.AddOrReplaceChild("saddle_base", VanillaPartPose.Zero,
            new VanillaCubeDefinition("saddle_base", 0, 89, -7.5f, -12f, -23.5f, 15f, 12f, 27f, Grow: 0.05f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        head.AddOrReplaceChild("reins_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("reins_right", 98, 42, 3.51f, -18f, -17f, 0f, 7f, 15f));
        head.AddOrReplaceChild("reins_front", VanillaPartPose.Zero,
            new VanillaCubeDefinition("reins_front", 84, 57, -3.5f, -18f, -2f, 7f, 7f, 0f));
        head.AddOrReplaceChild("reins_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("reins_left", 98, 42, -3.51f, -18f, -17f, 0f, 7f, 15f));
        head.AddOrReplaceChild("bridle_lower", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bridle_lower", 60, 87, -3.5f, -7f, -15f, 7f, 8f, 19f, Grow: 0.05f));
        head.AddOrReplaceChild("bridle_upper", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bridle_upper", 21, 64, -3.5f, -21f, -15f, 7f, 14f, 7f, Grow: 0.05f));
        head.AddOrReplaceChild("bridle_snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bridle_snout", 50, 64, -2.5f, -21f, -21f, 5f, 5f, 6f, Grow: 0.05f));
        head.AddOrReplaceChild("bridle_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bridle_right", 74, 70, 2.5f, -19f, -18f, 1f, 2f, 2f));
        head.AddOrReplaceChild("bridle_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bridle_left", 74, 70, -3.5f, -19f, -18f, 1f, 2f, 2f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildNautilusArmorMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Zero,
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rootPart.AddOrReplaceChild("armor_shell", VanillaPartPose.Offset(0f, -13f, 5f),
            new VanillaCubeDefinition("armor_shell", 0, 0, -7f, -10f, -7f, 14f, 10f, 16f, Grow: 0.01f));
        rootPart.AddOrReplaceChild("armor_shell_mid", VanillaPartPose.Offset(0f, -13f, 5f),
            new VanillaCubeDefinition("armor_shell_mid", 0, 26, -7f, 0f, -7f, 14f, 8f, 20f, Grow: 0.01f));
        rootPart.AddOrReplaceChild("armor_shell_back", VanillaPartPose.Offset(0f, -13f, 5f),
            new VanillaCubeDefinition("armor_shell_back", 48, 26, -7f, 0f, 6f, 14f, 8f, 0f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildNautilusSaddleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Zero,
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rootPart.AddOrReplaceChild("saddle_shell", VanillaPartPose.Offset(0f, -13f, 5f),
            new VanillaCubeDefinition("saddle_shell", 0, 0, -7f, -10f, -7f, 14f, 10f, 16f, Grow: 0.2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildHappyHarnessMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("harness", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("harness", 0, 0, -8f, -16f, -8f, 16f, 16f, 16f));
        root.AddOrReplaceChild("goggles", VanillaPartPose.Offset(0f, 14f, -5.5f),
            new VanillaCubeDefinition("goggles", 0, 32, -8f, -2.5f, -2.5f, 16f, 5f, 5f, Grow: 0.15f));
        return new VanillaMeshDefinition(root);
    }
}
