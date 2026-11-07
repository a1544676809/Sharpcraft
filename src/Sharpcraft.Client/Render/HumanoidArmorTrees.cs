// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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

    /// <summary>Returns the armor model for an equipment slot: 2 feet, 3 legs, 4 chest, 5 head.</summary>
    public static CompiledEntityModel For(int slot, bool player) =>
        (player ? Player : Adult)[slot - 2];

    private static CompiledEntityModel BuildSlot(int slot, bool player)
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

    private static VanillaCubeDefinition ZeroCube(string name) =>
        new(name, 0, 0, 0f, 0f, 0f, 0f, 0f, 0f);
}
