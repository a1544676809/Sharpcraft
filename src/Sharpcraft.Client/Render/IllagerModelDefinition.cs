// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// IllagerModel: shared geometry for Vindicator/Evoker/Pillager. Head/hat/nose, body + 20px outer body,
/// crossed-arm group and four Humanoid limbs. Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked
/// via <c>VanillaMeshBake.Bake</c> (T-683) with per-cube <c>VisibleWhen</c> arm-pose predicates.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.illager.IllagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/illager/IllagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class IllagerModelDefinition
{
    public const int VindicatorEntityTypeId = 144;
    public const int EvokerEntityTypeId = 47;
    public const int PillagerEntityTypeId = 104;
    public const int IllusionerEntityTypeId = 69;

    /// <summary>Declarative DSL tree for the eleven illager parts (T-683). Must precede the variant fields.</summary>
    private static readonly VanillaMeshDefinition IllagerMesh = BuildIllagerMesh();

    public static readonly EntityModelDefinition Vindicator = Create("vindicator", "entity/illager/vindicator");
    public static readonly EntityModelDefinition Evoker = Create("evoker", "entity/illager/evoker");
    public static readonly EntityModelDefinition Pillager = Create("pillager", "entity/illager/pillager");
    public static readonly EntityModelDefinition Illusioner = Create("illusioner", "entity/illager/illusioner");

    // Source IllagerModel layers use MeshTransformer.scaling(0.9375); the baked root therefore has
    // yScale=0.9375 and root.y=24.016*(1-0.9375). scale carries the former, the mesher's BuildModel
    // root-offset path carries the latter (ref://.../builders/MeshTransformer.java:7-9).
    private const float LayerScale = 0.9375f;

    private static EntityModelDefinition Create(string name, string texturePath) => EntityModelDefinition.FromTree(
        name,
        64,
        64,
        IllagerMesh,
        faceTransforms: null,
        texturePath,
        scale: LayerScale,
        baselinePx: 0f,
        rawSourceFrame: true);

    private static VanillaMeshDefinition BuildIllagerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -10f, -4f, 8f, 10f, 8f));
        // Source IllagerModel.createBodyLayer: hat is an independent child cube of head,
        // box(-4,-10,-4, 8,12,8) texOffs(32,0) deformation 0.45, PartPose.ZERO.
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 32, 0, -4f, -10f, -4f, 8f, 12f, 8f, Grow: 0.45f));
        root.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -2f, 0f),
            new VanillaCubeDefinition("nose", 24, 0, -1f, -1f, -6f, 2f, 4f, 2f));
        root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 20, -4f, 0f, -3f, 8f, 12f, 6f));
        root.AddOrReplaceChild("body_outer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_outer", 0, 38, -4f, 0f, -3f, 8f, 20f, 6f, Grow: 0.5f));
        // Source IllagerModel: arms PartPose.offsetAndRotation(0,3,-1, -0.75,0,0) - raw literal sign.
        root.AddOrReplaceChild("arms_right", VanillaPartPose.OffsetAndRotation(0f, 3f, -1f, -0.75f, 0f, 0f),
            new VanillaCubeDefinition("arms_right", 44, 22, -8f, -2f, -2f, 4f, 8f, 4f,
                VisibleWhen: static state => state.ArmPose == (int)IllagerArmPose.Crossed));
        root.AddOrReplaceChild("arms_cross", VanillaPartPose.OffsetAndRotation(0f, 3f, -1f, -0.75f, 0f, 0f),
            new VanillaCubeDefinition("arms_cross", 40, 38, -4f, 2f, -2f, 8f, 4f, 4f,
                VisibleWhen: static state => state.ArmPose == (int)IllagerArmPose.Crossed));
        root.AddOrReplaceChild("arms_left", VanillaPartPose.OffsetAndRotation(0f, 3f, -1f, -0.75f, 0f, 0f),
            new VanillaCubeDefinition("arms_left", 44, 22, 4f, -2f, -2f, 4f, 8f, 4f, Mirror: true,
                VisibleWhen: static state => state.ArmPose == (int)IllagerArmPose.Crossed));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 46, -3f, -2f, -2f, 4f, 12f, 4f,
                VisibleWhen: static state => state.ArmPose != (int)IllagerArmPose.Crossed));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 40, 46, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true,
                VisibleWhen: static state => state.ArmPose != (int)IllagerArmPose.Crossed));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
