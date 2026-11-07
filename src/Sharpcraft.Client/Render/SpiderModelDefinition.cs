// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SpiderModel: head + two body cubes + eight rotated 16x2x2 legs. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-690).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.spider.SpiderModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/spider/SpiderModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SpiderModelDefinition
{
    public const int SpiderEntityTypeId = 127;
    private const float Pi = MathF.PI;

    private static readonly VanillaMeshDefinition SpiderMesh = BuildSpiderMesh();

    public static readonly EntityModelDefinition Definition = new(
        "spider",
        64,
        32,
        [.. VanillaMeshBake.Bake(SpiderMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/spider/spider", 1f, "entity/spider/spider_eyes");

    private static VanillaMeshDefinition BuildSpiderMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, -3f),
            new VanillaCubeDefinition("head", 32, 4, -4f, -4f, -8f, 8f, 8f, 8f, MirrorX: false));
        root.AddOrReplaceChild("body0", VanillaPartPose.Offset(0f, 15f, 0f),
            new VanillaCubeDefinition("body0", 0, 0, -3f, -3f, -3f, 6f, 6f, 6f, MirrorX: false));
        root.AddOrReplaceChild("body1", VanillaPartPose.Offset(0f, 15f, 9f),
            new VanillaCubeDefinition("body1", 0, 12, -5f, -4f, -6f, 10f, 8f, 12f, MirrorX: false));
        AddLeg(root, "right_hind_leg", -4f, 2f, false, Pi / 4f, -Pi / 4f);
        AddLeg(root, "left_hind_leg", 4f, 2f, true, -Pi / 4f, Pi / 4f);
        AddLeg(root, "right_middle_hind_leg", -4f, 1f, false, Pi / 8f, -0.58119464f);
        AddLeg(root, "left_middle_hind_leg", 4f, 1f, true, -Pi / 8f, 0.58119464f);
        AddLeg(root, "right_middle_front_leg", -4f, 0f, false, -Pi / 8f, -0.58119464f);
        AddLeg(root, "left_middle_front_leg", 4f, 0f, true, Pi / 8f, 0.58119464f);
        AddLeg(root, "right_front_leg", -4f, -1f, false, -Pi / 4f, -Pi / 4f);
        AddLeg(root, "left_front_leg", 4f, -1f, true, Pi / 4f, Pi / 4f);
        return new VanillaMeshDefinition(root);
    }

    private static void AddLeg(VanillaPartDefinition root, string name, float pivotX, float pivotZ, bool mirror, float rotationY, float rotationZ) =>
        root.AddOrReplaceChild(name,
            VanillaPartPose.OffsetAndRotation(pivotX, 15f, pivotZ, 0f, rotationY, -rotationZ),
            new VanillaCubeDefinition(name, 18, 0, mirror ? -1f : -15f, -1f, -1f, 16f, 2f, 2f,
                Mirror: mirror, MirrorX: false));
}
