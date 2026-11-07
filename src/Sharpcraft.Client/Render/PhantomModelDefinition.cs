// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PhantomModel: body with head, a two-segment tail and two two-segment wings. The eyes layer uses phantom_eyes.
/// Bind pose only; wing flap and tail sway are not ported. Parts are declared with the <c>VanillaMeshDefinition</c>
/// DSL and baked via <c>VanillaMeshBake</c> (T-687).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.phantom.PhantomModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/phantom/PhantomModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PhantomModelDefinition
{
    public const int PhantomEntityTypeId = 100;

    private static readonly VanillaMeshDefinition PhantomMesh = BuildPhantomMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "phantom",
        64,
        64,
        PhantomMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/phantom/phantom",
        emissiveTexturePath: "entity/phantom/phantom_eyes");

    private static VanillaMeshDefinition BuildPhantomMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0.1f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 8, -3f, -2f, -8f, 5f, 3f, 9f));
        body.AddOrReplaceChild("head", VanillaPartPose.OffsetAndRotation(0f, 1f, -7f, 0.2f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -2f, -5f, 7f, 3f, 5f));
        var tailBase = body.AddOrReplaceChild("tail_base", VanillaPartPose.Offset(0f, -2f, 1f),
            new VanillaCubeDefinition("tail_base", 3, 20, -2f, 0f, 0f, 3f, 2f, 6f));
        tailBase.AddOrReplaceChild("tail_tip", VanillaPartPose.Offset(0f, 0.5f, 6f),
            new VanillaCubeDefinition("tail_tip", 4, 29, -1f, 0f, 0f, 1f, 1f, 6f));
        var leftWingBase = body.AddOrReplaceChild("left_wing_base", VanillaPartPose.OffsetAndRotation(2f, -2f, -8f, 0f, 0f, 0.1f),
            new VanillaCubeDefinition("left_wing_base", 23, 12, 0f, 0f, 0f, 6f, 2f, 9f));
        leftWingBase.AddOrReplaceChild("left_wing_tip", VanillaPartPose.OffsetAndRotation(6f, 0f, 0f, 0f, 0f, 0.1f),
            new VanillaCubeDefinition("left_wing_tip", 16, 24, 0f, 0f, 0f, 13f, 1f, 9f));
        var rightWingBase = body.AddOrReplaceChild("right_wing_base", VanillaPartPose.OffsetAndRotation(-3f, -2f, -8f, 0f, 0f, -0.1f),
            new VanillaCubeDefinition("right_wing_base", 23, 12, -6f, 0f, 0f, 6f, 2f, 9f, Mirror: true));
        rightWingBase.AddOrReplaceChild("right_wing_tip", VanillaPartPose.OffsetAndRotation(-6f, 0f, 0f, 0f, 0f, -0.1f),
            new VanillaCubeDefinition("right_wing_tip", 16, 24, -13f, 0f, 0f, 13f, 1f, 9f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
