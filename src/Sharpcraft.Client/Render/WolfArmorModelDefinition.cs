// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Wolf armor is AdultWolfModel.createBodyLayer(new CubeDeformation(0.2F)): the full wolf body with the
/// armor inflation, not a humanoid slot set. The crackiness texture stays a separate legacy overlay.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.wolf.AdultWolfModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/wolf/AdultWolfModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WolfArmorModelDefinition
{
    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "wolf_armor",
        64,
        32,
        Reorder(WolfModelDefinition.BuildBodyMesh(0.2f)),
        faceTransforms: null,
        texturePath: null,
        baselinePx: 0f,
        rawSourceFrame: true);

    // Keep the source map's part iteration order for keyed sample comparison.
    private static VanillaMeshDefinition Reorder(VanillaMeshDefinition mesh)
    {
        var root = mesh.Root!;
        var byName = root.Children.ToDictionary(child => child.Name);
        string[] order = ["head", "right_front_leg", "right_hind_leg", "upper_body", "tail", "left_hind_leg", "body", "left_front_leg"];
        return new VanillaMeshDefinition(new VanillaPartDefinition(
            root.Name, root.Pose, root.Cubes, order.Select(name => byName[name]).ToArray()));
    }
}
