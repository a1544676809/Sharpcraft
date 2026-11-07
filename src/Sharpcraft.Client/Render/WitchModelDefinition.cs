// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// WitchModel: the villager body (body + grown jacket, crossed arms, legs) with a witch head carrying a four-tier hat
/// and a mole on the nose.
/// </summary>
/// <remarks>
/// Bind pose only. The reference starts from <c>VillagerModel.createBodyModel()</c> and replaces only the head/nose;
/// the jacket is a 0.5-grown outer layer of the body. Held-item and nose-wiggle animations are not ported. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-683) with per-cube
/// <c>MirrorX: false</c> on the head/body/arms roots; the Pi-based hat rotation and scale are unchanged.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.witch.WitchModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/witch/WitchModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WitchModelDefinition
{
    public const int WitchEntityTypeId = 148;

    private static readonly float Pi = MathF.PI;

    /// <summary>Declarative DSL tree for the fourteen witch parts (T-683).</summary>
    private static readonly VanillaMeshDefinition WitchMesh = BuildWitchMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "witch",
        64,
        128,
        WitchMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/witch/witch",
        0.9375f);

    private static VanillaMeshDefinition BuildWitchMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -10f, -4f, 8f, 10f, 8f, MirrorX: false));
        var nose = head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -2f, 0f),
            new VanillaCubeDefinition("nose", 24, 0, -1f, -1f, -6f, 2f, 4f, 2f));
        nose.AddOrReplaceChild("mole", VanillaPartPose.Offset(0f, -2f, 0f),
            new VanillaCubeDefinition("mole", 0, 0, 0f, 3f, -6.75f, 1f, 1f, 1f, Grow: -0.25f));
        var hat = head.AddOrReplaceChild("hat", VanillaPartPose.Offset(-5f, -10.03125f, -5f),
            new VanillaCubeDefinition("hat", 0, 64, 0f, 0f, 0f, 10f, 2f, 10f));
        var hat2 = hat.AddOrReplaceChild("hat2", VanillaPartPose.OffsetAndRotation(1.75f, -4f, 2f, -0.05235988f, 0f, 0.02617994f),
            new VanillaCubeDefinition("hat2", 0, 76, 0f, 0f, 0f, 7f, 4f, 7f));
        var hat3 = hat2.AddOrReplaceChild("hat3", VanillaPartPose.OffsetAndRotation(1.75f, -4f, 2f, -0.10471976f, 0f, 0.05235988f),
            new VanillaCubeDefinition("hat3", 0, 87, 0f, 0f, 0f, 4f, 4f, 4f));
        hat3.AddOrReplaceChild("hat4", VanillaPartPose.OffsetAndRotation(1.75f, -2f, 2f, -Pi / 15f, 0f, 0.10471976f),
            new VanillaCubeDefinition("hat4", 0, 95, 0f, 0f, 0f, 1f, 2f, 1f, Grow: 0.25f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 20, -4f, 0f, -3f, 8f, 12f, 6f, MirrorX: false));
        body.AddOrReplaceChild("jacket", VanillaPartPose.Zero,
            new VanillaCubeDefinition("jacket", 0, 38, -4f, 0f, -3f, 8f, 20f, 6f, Grow: 0.5f));
        var arms = root.AddOrReplaceChild("arms", VanillaPartPose.OffsetAndRotation(0f, 3f, -1f, 0.75f, 0f, 0f),
            new VanillaCubeDefinition("arms", 40, 38, -4f, -2f, -2f, 8f, 4f, 4f, MirrorX: false));
        arms.AddOrReplaceChild("arms_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("arms_left", 44, 22, -8f, -2f, -2f, 4f, 8f, 4f));
        arms.AddOrReplaceChild("arms_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("arms_right", 44, 22, 4f, -2f, -2f, 4f, 8f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
