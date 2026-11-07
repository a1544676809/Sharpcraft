// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Dedicated baby humanoid armor geometry (reference <c>HumanoidModel.createBabyArmorMesh</c>): its own head/body/
/// waist/arm/leg/foot boxes with baby UVs. Deformation is applied per slot when the armor layer is built. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-702).
/// </summary>
internal static class BabyHumanoidArmorParts
{
    private static readonly VanillaMeshDefinition ArmorMesh = BuildArmorMesh();

    public static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(ArmorMesh, 0, 0)];

    private static VanillaMeshDefinition BuildArmorMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4.5f, -7f, -4.5f, 9f, 8f, 8f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18f, 0f),
            new VanillaCubeDefinition("body", 0, 17, -3f, -3f, -1.5f, 6f, 5f, 3f));
        root.AddOrReplaceChild("waist", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("waist", 0, 36, -3f, -1.2f, -1.49f, 5.9f, 2f, 2.9f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-3.5f, 15.5f, 0f),
            new VanillaCubeDefinition("right_arm", 30, 25, -1f, 0f, -1.53f, 2f, 5f, 3f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(3.5f, 15.5f, 0f),
            new VanillaCubeDefinition("left_arm", 30, 17, -1f, 0f, -1.53f, 2f, 5f, 3f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0.5f),
            new VanillaCubeDefinition("left_leg", 18, 24, -2f, -0.2f, -2f, 3f, 4f, 3f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0.5f),
            new VanillaCubeDefinition("right_leg", 18, 17, -1f, -0.2f, -2f, 3f, 4f, 3f));
        root.AddOrReplaceChild("right_foot", VanillaPartPose.Offset(1.5f, 20f, 0.5f),
            new VanillaCubeDefinition("right_foot", 0, 25, -2f, 2.9f, -2f, 3f, 1f, 3f));
        root.AddOrReplaceChild("left_foot", VanillaPartPose.Offset(-1.5f, 20f, 0.5f),
            new VanillaCubeDefinition("left_foot", 0, 29, -1f, 2.9f, -2f, 3f, 1f, 3f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
