// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Creeper armor is CreeperModel.createBodyLayer(new CubeDeformation(2.0F)): the charged-creeper
/// overlay, not an equipment set. Geometry is the creeper body/head/legs inflated by 2 px.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.creeper.CreeperModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creeper/CreeperModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CreeperArmorModelDefinition
{
    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "creeper_armor",
        64,
        32,
        Reorder(CreeperModelDefinition.BuildCreeperMesh(2f)),
        faceTransforms: null,
        texturePath: "entity/creeper/creeper_armor",
        baselinePx: 0f,
        rawSourceFrame: true);

    // LayerDefinitions bakes CREEPER_ARMOR from the same mesh, but the probe's map iteration order is
    // part-keyed. Keep that source order so keyed sample comparison stays exact.
    private static VanillaMeshDefinition Reorder(VanillaMeshDefinition mesh)
    {
        var root = mesh.Root!;
        var byName = root.Children.ToDictionary(child => child.Name);
        string[] order = ["head", "right_front_leg", "right_hind_leg", "left_hind_leg", "body", "left_front_leg"];
        return new VanillaMeshDefinition(new VanillaPartDefinition(
            root.Name, root.Pose, root.Cubes, order.Select(name => byName[name]).ToArray()));
    }
}
