// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>VexModel: small floating body with arms and flat wings; charging switches to the charging texture.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-688), all
/// with <c>Kind=Translucent</c>.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.vex.VexModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/vex/VexModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class VexModelDefinition
{
    public const int VexEntityTypeId = 142;

    private static readonly VanillaMeshDefinition VexMesh = BuildVexMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "vex",
        32,
        32,
        VexMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/illager/vex",
        textureSelector: state => state.IsCharging ? "entity/illager/vex_charging" : "entity/illager/vex",
        additionalTexturePaths: ["entity/illager/vex_charging"]);

    private static VanillaMeshDefinition BuildVexMesh()
    {
        var kind = EntityGeometryLayerKind.Translucent;
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Offset(0f, -2.5f, 0f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, Kind: kind));
        rootPart.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 20f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -2.5f, -5f, -2.5f, 5f, 5f, 5f, Kind: kind));
        var body = rootPart.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 20f, 0f),
            new VanillaCubeDefinition("body", 0, 10, -1.5f, 0f, -1f, 3f, 4f, 2f, Kind: kind));
        body.AddOrReplaceChild("body_inner", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_inner", 0, 16, -1.5f, 1f, -1f, 3f, 5f, 2f, Grow: -0.2f, Kind: kind));
        body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-1.75f, 0.25f, 0f),
            new VanillaCubeDefinition("right_arm", 23, 0, -1.25f, -0.5f, -1f, 2f, 4f, 2f, Grow: -0.1f, Kind: kind));
        body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(1.75f, 0.25f, 0f),
            new VanillaCubeDefinition("left_arm", 23, 6, -0.75f, -0.5f, -1f, 2f, 4f, 2f, Grow: -0.1f, Kind: kind));
        body.AddOrReplaceChild("left_wing", VanillaPartPose.Offset(0.5f, 1f, 1f),
            new VanillaCubeDefinition("left_wing", 16, 14, 0f, 0f, 0f, 0f, 5f, 8f, Mirror: true, Kind: kind));
        body.AddOrReplaceChild("right_wing", VanillaPartPose.Offset(-0.5f, 1f, 1f),
            new VanillaCubeDefinition("right_wing", 16, 14, 0f, 0f, 0f, 0f, 5f, 8f, Kind: kind));
        return new VanillaMeshDefinition(root);
    }
}
