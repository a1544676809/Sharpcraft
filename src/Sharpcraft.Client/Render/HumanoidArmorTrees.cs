// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Baked humanoid armor layer models. The reference builds four independent meshes from <c>HumanoidModel.createMesh</c>
/// and retains only the slot's parts; every non-slot root part stays as an empty group so the armor model keeps the
/// humanoid pose chain. Player armor additionally keeps empty sleeve/pants/jacket groups for the skin overlays.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.HumanoidModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Adult humanoid armor slot meshes baked as independent trees")]
internal static class HumanoidArmorTrees
{
    private const float OuterGrow = 1.0f;
    private const float InnerGrow = 0.5f;

    private static readonly CompiledEntityModel[] Adult =
    [
        BuildSlot(2, player: false),
        BuildSlot(3, player: false),
        BuildSlot(4, player: false),
        BuildSlot(5, player: false)
    ];

    private static readonly CompiledEntityModel[] Player =
    [
        BuildSlot(2, player: true),
        BuildSlot(3, player: true),
        BuildSlot(4, player: true),
        BuildSlot(5, player: true)
    ];

    private static readonly CompiledEntityModel[] Baby =
    [
        BuildBabySlot(2),
        BuildBabySlot(3),
        BuildBabySlot(4),
        BuildBabySlot(5)
    ];

    private static readonly CompiledEntityModel[] Piglin =
    [
        BuildSlot(2, player: true, piglin: true),
        BuildSlot(3, player: true, piglin: true),
        BuildSlot(4, player: true, piglin: true),
        BuildSlot(5, player: true, piglin: true)
    ];

    /// <summary>Returns the armor model for an equipment slot: 2 feet, 3 legs, 4 chest, 5 head.</summary>
    public static CompiledEntityModel For(int slot, bool player) =>
        (player ? Player : Adult)[slot - 2];

    /// <summary>Returns the dedicated baby armor model for an equipment slot.</summary>
    public static CompiledEntityModel ForBaby(int slot) => Baby[slot - 2];

    /// <summary>Returns the piglin armor model: player armor geometry with empty ear groups under the head.</summary>
    public static CompiledEntityModel ForPiglin(int slot) => Piglin[slot - 2];

    private static CompiledEntityModel BuildSlot(int slot, bool player, bool piglin = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, 0f),
            slot == 5
                ? new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: OuterGrow)
                : ZeroCube("head"));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            slot == 5
                ? new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: OuterGrow + 0.5f)
                : ZeroCube("hat"));
        if (piglin)
        {
            head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero, ZeroCube("left_ear"));
            head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero, ZeroCube("right_ear"));
        }
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
            slot is 4 or 3
                ? new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f,
                    Grow: slot == 3 ? InnerGrow : OuterGrow)
                : ZeroCube("body"));
        var rightArm = root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            slot == 4
                ? new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f, Grow: OuterGrow)
                : ZeroCube("right_arm"));
        var leftArm = root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            slot == 4
                ? new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true, Grow: OuterGrow)
                : ZeroCube("left_arm"));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            slot is 2 or 3
                ? new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f,
                    Grow: (slot == 3 ? InnerGrow : OuterGrow) - 0.1f)
                : ZeroCube("right_leg"));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            slot is 2 or 3
                ? new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true,
                    Grow: (slot == 3 ? InnerGrow : OuterGrow) - 0.1f)
                : ZeroCube("left_leg"));

        if (player)
        {
            body.AddOrReplaceChild("jacket", VanillaPartPose.Zero, ZeroCube("jacket"));
            leftArm.AddOrReplaceChild("left_sleeve", VanillaPartPose.Zero, ZeroCube("left_sleeve"));
            rightArm.AddOrReplaceChild("right_sleeve", VanillaPartPose.Zero, ZeroCube("right_sleeve"));
            leftLeg.AddOrReplaceChild("left_pants", VanillaPartPose.Zero, ZeroCube("left_pants"));
            rightLeg.AddOrReplaceChild("right_pants", VanillaPartPose.Zero, ZeroCube("right_pants"));
        }

        return VanillaMeshBakery.Build(new VanillaMeshDefinition(root), 64, 32);
    }

    private static CompiledEntityModel BuildBabySlot(int slot)
    {
        var outer = new Vector3(-0.1f, 0.5f, 0.3f);
        var inner = new Vector3(-0.1f, 0.3f, 0.3f);
        var inset = new Vector3(-0.1f, -0.1f, -0.1f);
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, 0f),
            slot == 5
                ? new VanillaCubeDefinition("head", 0, 0, -4.5f, -7f, -4.5f, 9f, 8f, 8f, GrowXyz: outer)
                : ZeroCube("head"));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero, ZeroCube("hat"));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18f, 0f),
            slot == 4
                ? new VanillaCubeDefinition("body", 0, 17, -3f, -3f, -1.5f, 6f, 5f, 3f, GrowXyz: outer)
                : ZeroCube("body"));
        root.AddOrReplaceChild("waist", VanillaPartPose.Offset(0f, 19f, 0f),
            slot == 3
                ? new VanillaCubeDefinition("waist", 0, 36, -3f, -1.2f, -1.49f, 5.9f, 2f, 2.9f, GrowXyz: inner + inset)
                : ZeroCube("waist"));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-3.5f, 15.5f, 0f),
            slot == 4
                ? new VanillaCubeDefinition("right_arm", 30, 25, -1f, 0f, -1.53f, 2f, 5f, 3f, GrowXyz: outer)
                : ZeroCube("right_arm"));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(3.5f, 15.5f, 0f),
            slot == 4
                ? new VanillaCubeDefinition("left_arm", 30, 17, -1f, 0f, -1.53f, 2f, 5f, 3f, GrowXyz: outer)
                : ZeroCube("left_arm"));
        root.AddOrReplaceChild("inner_body", VanillaPartPose.Offset(0f, 18f, 0f), ZeroCube("inner_body"));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0.5f),
            slot is 2 or 3
                ? new VanillaCubeDefinition("left_leg", 18, 24, -2f, -0.2f, -2f, 3f, 4f, 3f,
                    GrowXyz: (slot == 3 ? inner : outer) + inset)
                : ZeroCube("left_leg"));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0.5f),
            slot is 2 or 3
                ? new VanillaCubeDefinition("right_leg", 18, 17, -1f, -0.2f, -2f, 3f, 4f, 3f,
                    GrowXyz: (slot == 3 ? inner : outer) + inset)
                : ZeroCube("right_leg"));
        if (slot == 2)
        {
            leftLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Zero,
                new VanillaCubeDefinition("right_foot", 0, 25, -2f, 2.9f, -2f, 3f, 1f, 3f, GrowXyz: outer));
            rightLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Zero,
                new VanillaCubeDefinition("left_foot", 0, 29, -1f, 2.9f, -2f, 3f, 1f, 3f, Mirror: true, GrowXyz: outer));
        }
        else if (slot != 3)
        {
            leftLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Zero, ZeroCube("right_foot"));
            rightLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Zero, ZeroCube("left_foot"));
        }

        return VanillaMeshBakery.Build(new VanillaMeshDefinition(root), 64, 64);
    }

    private static VanillaCubeDefinition ZeroCube(string name) =>
        new(name, 0, 0, 0f, 0f, 0f, 0f, 0f, 0f);
}
