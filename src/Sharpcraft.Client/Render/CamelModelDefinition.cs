// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultCamelModel: long body with hump, tail, a three-cube head with ears, and four 5x21x5 legs.
/// </summary>
/// <remarks>
/// The reference puts hump/tail/head under the body, and the three head cubes plus both ears under that head part; this
/// definition mirrors that hierarchy instead of composing absolute pivots (docs/modules/entity-render.md 9). Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-685).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.camel.CamelModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/camel/CamelModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CamelModelDefinition
{
    public const int CamelEntityTypeId = 19;

    /// <summary>Declarative DSL tree for the thirteen camel parts (T-685).</summary>
    private static readonly VanillaMeshDefinition CamelMesh = BuildCamelMesh();

    internal static VanillaMeshDefinition Mesh => CamelMesh;

    private static readonly CompiledEntityModel CompiledModel =
        VanillaMeshBakery.Build(CamelMesh, 128, 128, texturePath: "entity/camel/camel");

    public static readonly EntityModelDefinition Definition = new(
        "camel",
        128,
        128,
        [.. VanillaMeshBake.Bake(CamelMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/camel/camel",
        compiled: CompiledModel);

    private static VanillaMeshDefinition BuildCamelMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 4f, 9.5f),
            new VanillaCubeDefinition("body", 0, 25, -7.5f, -12f, -23.5f, 15f, 12f, 27f));
        body.AddOrReplaceChild("hump", VanillaPartPose.Offset(0f, -12f, -10f),
            new VanillaCubeDefinition("hump", 74, 0, -4.5f, -5f, -5.5f, 9f, 5f, 11f));
        body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -9f, 3.5f),
            new VanillaCubeDefinition("tail", 122, 0, -1.5f, 0f, 0f, 3f, 14f, 0f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -3f, -19.5f),
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        head.AddOrReplaceChild("head_lower", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_lower", 60, 24, -3.5f, -7f, -15f, 7f, 8f, 19f));
        head.AddOrReplaceChild("head_upper", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_upper", 21, 0, -3.5f, -21f, -15f, 7f, 14f, 7f));
        head.AddOrReplaceChild("head_snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_snout", 50, 0, -2.5f, -21f, -21f, 5f, 5f, 6f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(2.5f, -21f, -9.5f),
            new VanillaCubeDefinition("left_ear", 45, 0, -0.5f, 0.5f, -1f, 3f, 1f, 2f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-2.5f, -21f, -9.5f),
            new VanillaCubeDefinition("right_ear", 67, 0, -2.5f, 0.5f, -1f, 3f, 1f, 2f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(4.9f, 1f, 9.5f),
            new VanillaCubeDefinition("left_hind_leg", 58, 16, -2.5f, 2f, -2.5f, 5f, 21f, 5f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-4.9f, 1f, 9.5f),
            new VanillaCubeDefinition("right_hind_leg", 94, 16, -2.5f, 2f, -2.5f, 5f, 21f, 5f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(4.9f, 1f, -10.5f),
            new VanillaCubeDefinition("left_front_leg", 0, 0, -2.5f, 2f, -2.5f, 5f, 21f, 5f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-4.9f, 1f, -10.5f),
            new VanillaCubeDefinition("right_front_leg", 0, 26, -2.5f, 2f, -2.5f, 5f, 21f, 5f));
        return new VanillaMeshDefinition(root);
    }
}
