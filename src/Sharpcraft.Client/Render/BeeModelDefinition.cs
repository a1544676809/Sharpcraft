// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultBeeModel: an empty "bone" pivot carrying the body, two-segment wings and three leg planes, with the body
/// carrying the stinger and both antennae.
/// </summary>
/// <remarks>
/// Static bind pose (the wing flap animation is not ported). The renderer also swaps in angry/nectar textures by entity
/// state; those state textures are not wired yet (docs/analysis/render/entity-variants.md). Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-678); the texture selector and its
/// additional texture paths are unchanged.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.bee.BeeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/bee/BeeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BeeModelDefinition
{
    public const int BeeEntityTypeId = 11;

    /// <summary>Declarative DSL tree for the ten nested bee parts (T-678).</summary>
    private static readonly VanillaMeshDefinition BeeMesh = BuildBeeMesh();

    public static readonly EntityModelDefinition Definition = new(
        "bee",
        64,
        64,
        [.. VanillaMeshBake.Bake(BeeMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bee/bee",
        textureSelector: state => state.IsAngry
            ? (state.HasNectar ? "entity/bee/bee_angry_nectar" : "entity/bee/bee_angry")
            : (state.HasNectar ? "entity/bee/bee_nectar" : "entity/bee/bee"),
        additionalTexturePaths:
        [
            "entity/bee/bee_angry", "entity/bee/bee_nectar", "entity/bee/bee_angry_nectar"
        ]);

    /// <summary>
    /// Empty bone pivot carrying the body (with stinger/antennae), the two wings (grow 0.001, ±0.2618 rotationY) and the
    /// three leg planes. Pre-order baking reproduces the original flat part order and parent links.
    /// </summary>
    private static VanillaMeshDefinition BuildBeeMesh()
    {
        var container = new VanillaPartDefinition("container", VanillaPartPose.Zero);
        var bone = container.AddOrReplaceChild("bone", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("bone", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var body = bone.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 0, 0, -3.5f, -4f, -5f, 7f, 7f, 10f));
        body.AddOrReplaceChild("stinger", VanillaPartPose.Zero,
            new VanillaCubeDefinition("stinger", 26, 7, 0f, -1f, 5f, 0f, 1f, 2f));
        body.AddOrReplaceChild("left_antenna", VanillaPartPose.Offset(0f, -2f, -5f),
            new VanillaCubeDefinition("left_antenna", 2, 0, 1.5f, -2f, -3f, 1f, 2f, 3f));
        body.AddOrReplaceChild("right_antenna", VanillaPartPose.Offset(0f, -2f, -5f),
            new VanillaCubeDefinition("right_antenna", 2, 3, -2.5f, -2f, -3f, 1f, 2f, 3f));
        bone.AddOrReplaceChild("right_wing", VanillaPartPose.OffsetAndRotation(-1.5f, -4f, -3f, 0f, -0.2618f, 0f),
            new VanillaCubeDefinition("right_wing", 0, 18, -9f, 0f, 0f, 9f, 0f, 6f, Grow: 0.001f));
        bone.AddOrReplaceChild("left_wing", VanillaPartPose.OffsetAndRotation(1.5f, -4f, -3f, 0f, 0.2618f, 0f),
            new VanillaCubeDefinition("left_wing", 0, 18, 0f, 0f, 0f, 9f, 0f, 6f, Mirror: true, Grow: 0.001f));
        bone.AddOrReplaceChild("front_legs", VanillaPartPose.Offset(1.5f, 3f, -2f),
            new VanillaCubeDefinition("front_legs", 26, 1, -5f, 0f, 0f, 7f, 2f, 0f));
        bone.AddOrReplaceChild("middle_legs", VanillaPartPose.Offset(1.5f, 3f, 0f),
            new VanillaCubeDefinition("middle_legs", 26, 3, -5f, 0f, 0f, 7f, 2f, 0f));
        bone.AddOrReplaceChild("back_legs", VanillaPartPose.Offset(1.5f, 3f, 2f),
            new VanillaCubeDefinition("back_legs", 26, 5, -5f, 0f, 0f, 7f, 2f, 0f));
        return new VanillaMeshDefinition(container);
    }
}
