// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultRabbitModel: body with tail, head with ears, front legs and hind haunches, all through the reference's nested
/// pivots (body → head → ears, body → frontlegs, body → backlegs → hind legs → haunch). Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-677).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.rabbit.RabbitModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/rabbit/RabbitModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class RabbitModelDefinition
{
    public const int RabbitEntityTypeId = 111;

    /// <summary>Declarative DSL tree for the nested rabbit parts (T-677).</summary>
    private static readonly VanillaMeshDefinition RabbitMesh = BuildRabbitMesh();

    public static readonly EntityModelDefinition Definition = new(
        "rabbit",
        64,
        64,
        [.. VanillaMeshBake.Bake(RabbitMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/rabbit/rabbit_brown");

    public static readonly IReadOnlyList<string> VariantKeys = ["brown", "white", "black", "white_splotched", "gold", "salt", "evil"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brown"] = "rabbit_brown",
            ["white"] = "rabbit_white",
            ["black"] = "rabbit_black",
            ["white_splotched"] = "rabbit_white_splotched",
            ["gold"] = "rabbit_gold",
            ["salt"] = "rabbit_salt",
            ["evil"] = "rabbit_caerbannog"
        };
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["brown"] = Definition
        };
        foreach (var key in VariantKeys)
        {
            if (key != "brown")
            {
                variants[key] = new EntityModelDefinition("rabbit_" + key, 64, 64, Definition.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/rabbit/" + files[key]);
            }
        }

        return variants;
    }

    /// <summary>
    /// Root-level <c>body</c> and <c>backlegs</c> groups with their nested children; pre-order baking reproduces the
    /// original flat part order (body, tail, head, ears, frontlegs chain, backlegs chain).
    /// </summary>
    private static VanillaMeshDefinition BuildRabbitMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 23f, 4f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -4f, -6f, -9f, 8f, 6f, 10f));
        body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -4.9916f, 0.0125f),
            new VanillaCubeDefinition("tail", 20, 16, -2f, -3.0084f, -1.0125f, 4f, 4f, 4f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.OffsetAndRotation(0f, -5.2929f, -8.1213f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 16, -2.5f, -3f, -4f, 5f, 5f, 5f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(1.5f, -3.7071f, -0.8787f),
            new VanillaCubeDefinition("left_ear", 32, 0, -1f, -4.2929f, -0.1213f, 2f, 5f, 1f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-1.5f, -3.7071f, -0.8787f),
            new VanillaCubeDefinition("right_ear", 26, 0, -1f, -4.2929f, -0.1213f, 2f, 5f, 1f));
        var frontlegs = body.AddOrReplaceChild("frontlegs", VanillaPartPose.Offset(0f, -1.5349f, -6.3108f),
            new VanillaCubeDefinition("frontlegs", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        frontlegs.AddOrReplaceChild("right_front_leg", VanillaPartPose.OffsetAndRotation(-2f, 1.9239f, 0.3827f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("right_front_leg", 36, 18, -0.9f, -1f, -0.9f, 2f, 4f, 2f));
        frontlegs.AddOrReplaceChild("left_front_leg", VanillaPartPose.OffsetAndRotation(2f, 1.9239f, 0.4827f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("left_front_leg", 44, 18, -1f, -1f, -1f, 2f, 4f, 2f));
        var backlegs = root.AddOrReplaceChild("backlegs", VanillaPartPose.Offset(0f, 23f, 4f),
            new VanillaCubeDefinition("backlegs", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var rightHindLeg = backlegs.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3f, 0.5f, 0f),
            new VanillaCubeDefinition("right_hind_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rightHindLeg.AddOrReplaceChild("right_haunch", VanillaPartPose.OffsetAndRotation(0f, -0.5f, 0f, 0f, 0.3927f, 0f),
            new VanillaCubeDefinition("right_haunch", 20, 24, -1f, 0f, -5f, 2f, 1f, 6f));
        var leftHindLeg = backlegs.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3f, 0.5f, 0f),
            new VanillaCubeDefinition("left_hind_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        leftHindLeg.AddOrReplaceChild("left_haunch", VanillaPartPose.OffsetAndRotation(0f, -0.5f, 0f, 0f, -0.3927f, 0f),
            new VanillaCubeDefinition("left_haunch", 36, 24, -1f, 0f, -5f, 2f, 1f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
