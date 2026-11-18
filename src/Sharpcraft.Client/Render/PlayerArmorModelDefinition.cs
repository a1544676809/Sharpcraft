// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Raw source-frame adult player armor layers (helmet/chestplate/leggings/boots). Head/chest/feet use the
/// outer (1.0 px) deformation; leggings use the inner (0.5 px) deformation. Geometry is the source
/// <c>HumanoidModel.createArmorMeshSet</c> retained-part meshes; baby/piglin and trim/glint/crackiness
/// material layers remain separate.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.HumanoidModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Adult humanoid armor slot raw source-frame meshes; baby/piglin and material layers separate")]
public static class PlayerArmorModelDefinition
{
    private const float Outer = 1f;
    private const float Inner = 0.5f;

    public static readonly EntityModelDefinition Helmet = Build("helmet", 5);
    public static readonly EntityModelDefinition Chestplate = Build("chestplate", 4);
    public static readonly EntityModelDefinition Leggings = Build("leggings", 3);
    public static readonly EntityModelDefinition Boots = Build("boots", 2);

    public static EntityModelDefinition ForSlot(int slot) => slot switch
    {
        5 => Helmet,
        4 => Chestplate,
        3 => Leggings,
        2 => Boots,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static EntityModelDefinition Build(string name, int slot)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (slot == 5)
        {
            var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
                new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: Outer));
            head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
                new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: Outer + 0.5f));
        }
        else if (slot == 4)
        {
            root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
                new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f, Grow: Outer));
            root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
                new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true, Grow: Outer));
            root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
                new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: Outer));
        }
        else if (slot is 3 or 2)
        {
            var grow = slot == 3 ? Inner : Outer;
            var legGrow = grow - 0.1f;
            root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
                new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true, Grow: legGrow));
            root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
                new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Grow: legGrow));
            if (slot == 3)
            {
                root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
                    new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: Inner));
            }
        }

        return EntityModelDefinition.FromTree(
            "player_armor_" + name + "_raw",
            64,
            32,
            new VanillaMeshDefinition(root),
            faceTransforms: null,
            texturePath: null,
            baselinePx: 0f,
            rawSourceFrame: true,
            sourceFrameBaselinePx: 0f);
    }
}
