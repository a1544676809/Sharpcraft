// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// NautilusArmorModel.createBodyLayer: the base nautilus body/mouth mesh plus the replacement three-cube
/// shell inflated by 0.01. The third shell cube has depth 0 and follows R-029's degenerate-face rule.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.nautilus.NautilusArmorModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/nautilus/NautilusArmorModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class NautilusArmorModelDefinition
{
    public static readonly EntityModelDefinition Definition =
        EntityModelDefinition.FromTree(
            "nautilus_armor",
            128,
            128,
            Reorder(NautilusModelDefinition.BuildBodyMesh(0.01f, flatShellGrow: 0f)),
            faceTransforms: null,
            texturePath: null,
            baselinePx: 0f,
            rawSourceFrame: true,
            sourceFrameBaselinePx: 0f);

    private static VanillaMeshDefinition Reorder(VanillaMeshDefinition mesh)
    {
        var root = mesh.Root!;
        var nautilus = root.GetChild("root")!;
        var body = nautilus.GetChild("body")!;
        var reorderedBody = new VanillaPartDefinition(
            body.Name, body.Pose, body.Cubes,
            new[] { "lower_mouth", "upper_mouth", "inner_mouth" }
                .Select(name => body.GetChild(name)!).ToArray());
        var reorderedNautilus = new VanillaPartDefinition(
            nautilus.Name, nautilus.Pose, nautilus.Cubes,
            new[] { reorderedBody, nautilus.GetChild("shell")! });
        return new VanillaMeshDefinition(new VanillaPartDefinition(
            root.Name, root.Pose, root.Cubes, [reorderedNautilus]));
    }
}
