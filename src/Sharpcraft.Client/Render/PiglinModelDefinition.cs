// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Piglin family geometry: the player humanoid mesh with the head replaced by the piglin head (snout, nostrils, ears).
/// </summary>
/// <remarks>
/// AdultPiglinModel starts from <c>PlayerModel.createMesh(false)</c> and only replaces the body and head (clearing the
/// hat), so arms, legs and their outer layers are the player ones. Zombified Piglin and Piglin Brute share this geometry
/// and differ only by texture. Animations and the piglin arm poses are not ported. The piglin head is declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-682), appended to the adult parts
/// (minus head) in the same order.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.piglin.PiglinModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PiglinModelDefinition
{
    public const int PiglinEntityTypeId = 102;
    public const int PiglinBruteEntityTypeId = 103;
    public const int ZombifiedPiglinEntityTypeId = 158;

    /// <summary>Declarative DSL tree for the six-part piglin head (T-682).</summary>
    private static readonly VanillaMeshDefinition PiglinHeadMesh = BuildPiglinHeadMesh();

    private static readonly EntityModelPart[] HeadParts =
        [.. VanillaMeshBake.Bake(PiglinHeadMesh, 64, 64, modelHeight: 0f)];

    private static readonly CompiledEntityModel HeadCompiled =
        VanillaMeshBakery.Build(PiglinHeadMesh, 64, 64, modelHeight: 0f, texturePath: "entity/piglin/piglin");

    /// <summary>Head-only piglin skull definition (head, nose, nostrils and ears), shared with the piglin entity model.</summary>
    public static readonly EntityModelDefinition Head = new("piglin_head", 64, 64, PlayerModelDefinition.DefaultFaceTransforms, "entity/piglin/piglin", compiled: HeadCompiled)
    {
        // VanillaMeshBake emits y-up geometry; the skull renderer's source pose includes scale(-1,-1,1).
        // ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java
        LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(0f, -0.5f, 0f)
    };

    /// <summary>
    /// Independent source-frame raw DSL for the piglin body (task-185). Mirrors source AdultPiglinModel body
    /// parts derived from PlayerModel.createMesh(NONE, false): body/arms/legs plus the full jacket/left_sleeve/
    /// right_sleeve/left_pants/right_pants overlays (Grow 0.25); hat is cleared by the piglin layer. Declared
    /// with literals independent of the player's DSL/bake. Together with the 4-cube head + 2 ears the model has
    /// 16 parts. Source: ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/AdultPiglinModel.java:17-24,
    /// ref://.../player/PlayerModel.java:43-87.
    /// </summary>
    private static VanillaMeshDefinition BuildPiglinBodyMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Source AdultPiglinModel.createBodyLayer = PlayerModel.createMesh(NONE, false) body/arms/legs +
        // jacket/sleeve/pants overlays (scale.extend(0.25) => Grow 0.25); hat cleared by Piglin.
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        body.AddOrReplaceChild("jacket", VanillaPartPose.Zero,
            new VanillaCubeDefinition("jacket", 16, 32, -4f, 0f, -2f, 8f, 12f, 4f, Grow: 0.25f));
        var leftArm = root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 32, 48, -1f, -2f, -2f, 4f, 12f, 4f));
        leftArm.AddOrReplaceChild("left_sleeve", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_sleeve", 48, 48, -1f, -2f, -2f, 4f, 12f, 4f, Grow: 0.25f));
        var rightArm = root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f));
        rightArm.AddOrReplaceChild("right_sleeve", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_sleeve", 40, 32, -3f, -2f, -2f, 4f, 12f, 4f, Grow: 0.25f));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 16, 48, -2f, 0f, -2f, 4f, 12f, 4f));
        leftLeg.AddOrReplaceChild("left_pants", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_pants", 0, 48, -2f, 0f, -2f, 4f, 12f, 4f, Grow: 0.25f));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        rightLeg.AddOrReplaceChild("right_pants", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_pants", 0, 32, -2f, 0f, -2f, 4f, 12f, 4f, Grow: 0.25f));
        return new VanillaMeshDefinition(root);
    }

    private static readonly VanillaMeshDefinition PiglinBodyMesh = BuildPiglinBodyMesh();

    private static EntityModelPart[] BuildParts()
    {
        // Independent source-frame body bake (no player DSL/parts reference), then the piglin head.
        var parts = new List<EntityModelPart>(VanillaMeshBake.Bake(PiglinBodyMesh, 0, 0, rawSourceFrame: true));
        parts.AddRange(VanillaMeshBake.Bake(PiglinHeadMesh, 0, 0, rawSourceFrame: true));
        return [.. parts];
    }

    private static readonly VanillaMeshDefinition PiglinMesh = BuildPiglinMesh();

    private static readonly CompiledEntityModel PiglinCompiled =
        VanillaMeshBakery.Build(PiglinMesh, 64, 64, rawSourceFrame: true);

    private static VanillaMeshDefinition BuildPiglinMesh()
    {
        // Independent body DSL (no Player reference), then the piglin head children.
        var children = new List<VanillaPartDefinition>(PiglinBodyMesh.Root!.Children);
        children.AddRange(PiglinHeadMesh.Root!.Children);
        return new VanillaMeshDefinition(new VanillaPartDefinition("root", VanillaPartPose.Zero, children: children));
    }

    private static VanillaMeshDefinition BuildPiglinHeadMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -5f, -8f, -4f, 10f, 8f, 8f));
        head.AddOrReplaceChild("head_nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_nose", 31, 1, -2f, -4f, -5f, 4f, 4f, 1f));
        head.AddOrReplaceChild("head_nostril_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_nostril_right", 2, 4, 2f, -2f, -5f, 1f, 2f, 1f));
        head.AddOrReplaceChild("head_nostril_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_nostril_left", 2, 0, -3f, -2f, -5f, 1f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(4.5f, -6f, 0f, 0f, 0f, -MathF.PI / 6f),
            new VanillaCubeDefinition("left_ear", 51, 6, 0f, 0f, -2f, 1f, 5f, 4f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-4.5f, -6f, 0f, 0f, 0f, MathF.PI / 6f),
            new VanillaCubeDefinition("right_ear", 39, 6, -1f, 0f, -2f, 1f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition Definition = new("piglin", 64, 64, faceTransforms: null, texturePath: "entity/piglin/piglin", compiled: PiglinCompiled, baselinePx: 0f, rawSourceFrame: true);

    public static readonly EntityModelDefinition Brute = new("piglin_brute", 64, 64, faceTransforms: null, texturePath: "entity/piglin/piglin_brute", compiled: PiglinCompiled, baselinePx: 0f, rawSourceFrame: true);

    public static readonly EntityModelDefinition Zombified = new("zombified_piglin", 64, 64, faceTransforms: null, texturePath: "entity/piglin/zombified_piglin", compiled: PiglinCompiled, baselinePx: 0f, rawSourceFrame: true);
}
