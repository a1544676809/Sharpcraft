// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// EnderDragonModel: head/jaw, five neck segments, twelve tail segments, body with scales, two two-segment wings and
/// four three-segment legs. Bind pose corresponds to the model definition; flight animation is not ported. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-691).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.dragon.EnderDragonModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/dragon/EnderDragonModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class EnderDragonModelDefinition
{
    public const int EnderDragonEntityTypeId = 44;

    private static readonly VanillaMeshDefinition DragonMesh = BuildDragonMesh();

    public static readonly EntityModelDefinition Definition = new(
        "ender_dragon",
        256,
        256,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/enderdragon/dragon");

    private static EntityModelPart[] BuildParts()
    {
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in VanillaMeshBake.Bake(DragonMesh, 0, 0))
        {
            byName[part.Name] = part;
        }

        var order = new List<string>
        {
            "head", "upperlip", "upperhead", "scale_left", "nostril_left", "scale_right", "nostril_right", "jaw"
        };
        for (var i = 0; i < 5; i++)
        {
            order.Add("neck" + i);
            order.Add("neck" + i + "_scale");
        }

        for (var i = 0; i < 12; i++)
        {
            order.Add("tail" + i);
            order.Add("tail" + i + "_scale");
        }

        order.AddRange(["body", "body_scale_1", "body_scale_2", "body_scale_3"]);
        order.AddRange(["left_wing", "left_wing_skin", "left_wing_tip", "left_wing_tip_skin"]);
        order.AddRange(["right_wing", "right_wing_skin", "right_wing_tip", "right_wing_tip_skin"]);
        foreach (var prefix in new[] { "left_front", "right_front", "left_hind", "right_hind" })
        {
            order.Add(prefix + "_leg");
            order.Add(prefix + "_leg_tip");
            order.Add(prefix + "_foot");
        }

        return [.. order.Select(name => byName[name])];
    }

    private static VanillaMeshDefinition BuildDragonMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 20f, -62f),
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        head.AddOrReplaceChild("upperlip", VanillaPartPose.Zero,
            new VanillaCubeDefinition("upperlip", 176, 44, -6f, -1f, -24f, 12f, 5f, 16f));
        head.AddOrReplaceChild("upperhead", VanillaPartPose.Zero,
            new VanillaCubeDefinition("upperhead", 112, 30, -8f, -8f, -10f, 16f, 16f, 16f));
        head.AddOrReplaceChild("scale_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("scale_left", 0, 0, -5f, -12f, -4f, 2f, 4f, 6f, Mirror: true));
        head.AddOrReplaceChild("nostril_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nostril_left", 112, 0, -5f, -3f, -22f, 2f, 2f, 4f, Mirror: true));
        head.AddOrReplaceChild("scale_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("scale_right", 0, 0, 3f, -12f, -4f, 2f, 4f, 6f));
        head.AddOrReplaceChild("nostril_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nostril_right", 112, 0, 3f, -3f, -22f, 2f, 2f, 4f));
        head.AddOrReplaceChild("jaw", VanillaPartPose.Offset(0f, 4f, -8f),
            new VanillaCubeDefinition("jaw", 176, 65, -6f, 0f, -16f, 12f, 4f, 16f));

        for (var i = 0; i < 5; i++)
        {
            AddSpine(root, "neck" + i, 20f, -12f - (i * 10f));
        }

        for (var i = 0; i < 12; i++)
        {
            AddSpine(root, "tail" + i, 10f, 60f + (i * 10f));
        }

        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 20f, 8f),
            new VanillaCubeDefinition("body", 0, 0, -12f, -16f, -16f, 24f, 24f, 64f));
        body.AddOrReplaceChild("body_scale_1", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_scale_1", 220, 53, -1f, -22f, -10f, 2f, 6f, 12f));
        body.AddOrReplaceChild("body_scale_2", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_scale_2", 220, 53, -1f, -22f, 10f, 2f, 6f, 12f));
        body.AddOrReplaceChild("body_scale_3", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_scale_3", 220, 53, -1f, -22f, 30f, 2f, 6f, 12f));
        var leftWing = body.AddOrReplaceChild("left_wing", VanillaPartPose.Offset(12f, -15f, -6f),
            new VanillaCubeDefinition("left_wing", 112, 88, 0f, -4f, -4f, 56f, 8f, 8f, Mirror: true));
        body.AddOrReplaceChild("left_wing_skin", VanillaPartPose.Offset(12f, -15f, -6f),
            new VanillaCubeDefinition("left_wing_skin", -56, 88, 0f, 0f, 2f, 56f, 0f, 56f, Mirror: true));
        leftWing.AddOrReplaceChild("left_wing_tip", VanillaPartPose.Offset(56f, 0f, 0f),
            new VanillaCubeDefinition("left_wing_tip", 112, 136, 0f, -2f, -2f, 56f, 4f, 4f, Mirror: true));
        leftWing.AddOrReplaceChild("left_wing_tip_skin", VanillaPartPose.Offset(56f, 0f, 0f),
            new VanillaCubeDefinition("left_wing_tip_skin", -56, 144, 0f, 0f, 2f, 56f, 0f, 56f, Mirror: true));
        var rightWing = body.AddOrReplaceChild("right_wing", VanillaPartPose.Offset(-12f, -15f, -6f),
            new VanillaCubeDefinition("right_wing", 112, 88, -56f, -4f, -4f, 56f, 8f, 8f));
        body.AddOrReplaceChild("right_wing_skin", VanillaPartPose.Offset(-12f, -15f, -6f),
            new VanillaCubeDefinition("right_wing_skin", -56, 88, -56f, 0f, 2f, 56f, 0f, 56f));
        rightWing.AddOrReplaceChild("right_wing_tip", VanillaPartPose.Offset(-56f, 0f, 0f),
            new VanillaCubeDefinition("right_wing_tip", 112, 136, -56f, -2f, -2f, 56f, 4f, 4f));
        rightWing.AddOrReplaceChild("right_wing_tip_skin", VanillaPartPose.Offset(-56f, 0f, 0f),
            new VanillaCubeDefinition("right_wing_tip_skin", -56, 144, -56f, 0f, 2f, 56f, 0f, 56f));

        AddLeg(body, "left_front", 12f, 0f, -6f, -4f, -4f, -4f, 8f, 24f, 8f, 112, 104, -3f, -1f, -3f, 6f, 24f, 6f, 226, 138, 23f, 0f, -4f, 0f, -12f, 8f, 4f, 16f, 144, 104, 1.3f, -0.5f, 0.75f);
        AddLeg(body, "right_front", -12f, 0f, -6f, -4f, -4f, -4f, 8f, 24f, 8f, 112, 104, -3f, -1f, -3f, 6f, 24f, 6f, 226, 138, 23f, 0f, -4f, 0f, -12f, 8f, 4f, 16f, 144, 104, 1.3f, -0.5f, 0.75f);
        AddLeg(body, "left_hind", 16f, -4f, 34f, -8f, -4f, -8f, 16f, 32f, 16f, 0, 0, -6f, -2f, 0f, 12f, 32f, 12f, 196, 0, 31f, 4f, -9f, 0f, -20f, 18f, 6f, 24f, 112, 0, 1.0f, 0.5f, 0.75f);
        AddLeg(body, "right_hind", -16f, -4f, 34f, -8f, -4f, -8f, 16f, 32f, 16f, 0, 0, -6f, -2f, 0f, 12f, 32f, 12f, 196, 0, 31f, 4f, -9f, 0f, -20f, 18f, 6f, 24f, 112, 0, 1.0f, 0.5f, 0.75f);
        return new VanillaMeshDefinition(root);
    }

    private static void AddSpine(VanillaPartDefinition root, string name, float pivotY, float pivotZ)
    {
        var spine = root.AddOrReplaceChild(name, VanillaPartPose.Offset(0f, pivotY, pivotZ),
            new VanillaCubeDefinition(name, 192, 104, -5f, -5f, -5f, 10f, 10f, 10f));
        spine.AddOrReplaceChild(name + "_scale", VanillaPartPose.Zero,
            new VanillaCubeDefinition(name + "_scale", 48, 0, -1f, -9f, -3f, 2f, 4f, 6f));
    }

    private static void AddLeg(
        VanillaPartDefinition body,
        string prefix,
        float pivotX,
        float pivotY,
        float pivotZ,
        float mainMinX,
        float mainMinY,
        float mainMinZ,
        float mainW,
        float mainH,
        float mainD,
        int mainU,
        int mainV,
        float tipMinX,
        float tipMinY,
        float tipMinZ,
        float tipW,
        float tipH,
        float tipD,
        int tipU,
        int tipV,
        float footPivotY,
        float footPivotZ,
        float footMinX,
        float footMinY,
        float footMinZ,
        float footW,
        float footH,
        float footD,
        int footU,
        int footV,
        float mainRotX,
        float tipRotX,
        float footRotX)
    {
        var leg = body.AddOrReplaceChild(prefix + "_leg", VanillaPartPose.OffsetAndRotation(pivotX, pivotY, pivotZ, mainRotX, 0f, 0f),
            new VanillaCubeDefinition(prefix + "_leg", mainU, mainV, mainMinX, mainMinY, mainMinZ, mainW, mainH, mainD));
        var tip = leg.AddOrReplaceChild(prefix + "_leg_tip", VanillaPartPose.OffsetAndRotation(0f, 20f, -1f, tipRotX, 0f, 0f),
            new VanillaCubeDefinition(prefix + "_leg_tip", tipU, tipV, tipMinX, tipMinY, tipMinZ, tipW, tipH, tipD));
        tip.AddOrReplaceChild(prefix + "_foot", VanillaPartPose.OffsetAndRotation(0f, footPivotY, footPivotZ, footRotX, 0f, 0f),
            new VanillaCubeDefinition(prefix + "_foot", footU, footV, footMinX, footMinY, footMinZ, footW, footH, footD));
    }
}
