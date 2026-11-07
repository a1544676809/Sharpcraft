// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby squid/bee/dolphin/axolotl models. Parts are declared with the <c>VanillaMeshDefinition</c> DSL and
/// baked via <c>VanillaMeshBake</c> (T-696).</summary>
public static class BabyModelDefinitionsAquatic
{
    private static readonly VanillaMeshDefinition SquidMesh = BuildSquidMesh();
    private static readonly VanillaMeshDefinition BeeMesh = BuildBeeMesh();
    private static readonly VanillaMeshDefinition DolphinMesh = BuildDolphinMesh();
    private static readonly VanillaMeshDefinition AxolotlMesh = BuildAxolotlMesh();

    private static readonly CompiledEntityModel SquidCompiled = VanillaMeshBakery.Build(SquidMesh, 32, 32);
    private static readonly CompiledEntityModel BeeCompiled = VanillaMeshBakery.Build(BeeMesh, 32, 32);
    private static readonly CompiledEntityModel DolphinCompiled = VanillaMeshBakery.Build(DolphinMesh, 64, 64);
    private static readonly CompiledEntityModel AxolotlCompiled = VanillaMeshBakery.Build(AxolotlMesh, 32, 32);

    public static readonly EntityModelDefinition Squid = new(
        "baby_squid", 32, 32, [.. VanillaMeshBake.Bake(SquidMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/squid/squid_baby",
        compiled: SquidCompiled);

    public static readonly EntityModelDefinition Bee = new(
        "baby_bee", 32, 32, [.. VanillaMeshBake.Bake(BeeMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/bee/bee_baby",
        textureSelector: state => state.IsAngry
            ? (state.HasNectar ? "entity/bee/bee_angry_nectar_baby" : "entity/bee/bee_angry_baby")
            : (state.HasNectar ? "entity/bee/bee_nectar_baby" : "entity/bee/bee_baby"),
        additionalTexturePaths:
        [
            "entity/bee/bee_angry_baby", "entity/bee/bee_nectar_baby", "entity/bee/bee_angry_nectar_baby"
        ],
        compiled: BeeCompiled);

    public static readonly EntityModelDefinition Dolphin = new(
        "baby_dolphin", 64, 64, [.. VanillaMeshBake.Bake(DolphinMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/dolphin/dolphin_baby",
        compiled: DolphinCompiled);

    public static readonly EntityModelDefinition Axolotl = new(
        "baby_axolotl", 32, 32, [.. VanillaMeshBake.Bake(AxolotlMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/axolotl/axolotl_lucy_baby",
        compiled: AxolotlCompiled);

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [130] = Squid,
            [11] = Bee,
            [36] = Dolphin,
            [7] = Axolotl
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>
        {
            [(7, "lucy")] = Axolotl
        };
        foreach (var key in AxolotlModelDefinition.VariantKeys)
        {
            if (key != "lucy")
            {
                variants[(7, key)] = new("baby_axolotl_" + key, 32, 32, Axolotl.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/axolotl/axolotl_" + key + "_baby", compiled: AxolotlCompiled);
            }
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildSquidMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 13f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -4f, -5f, -4f, 8f, 10f, 8f));
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI * 2f / 8f;
            var x = MathF.Cos(angle) * 3f;
            var z = MathF.Sin(angle) * 3f;
            var yRot = i * MathF.PI * -2f / 8f + (MathF.PI / 2f);
            root.AddOrReplaceChild("tentacle" + i, VanillaPartPose.OffsetAndRotation(x, 18.5f, z, 0f, -yRot, 0f),
                new VanillaCubeDefinition("tentacle" + i, 0, 18, -1f, -0.5f, -1f, 2f, 6f, 2f));
        }

        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildBeeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var bone = root.AddOrReplaceChild("bone", VanillaPartPose.Offset(0f, 19.6667f, -1.8567f),
            new VanillaCubeDefinition("bone", 6, 12, 1f, -1.6667f, -2.1633f, 1f, 2f, 2f));
        bone.AddOrReplaceChild("antenna_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("antenna_left", 0, 12, -2f, -1.6667f, -2.1933f, 1f, 2f, 2f));
        var body = bone.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 1.3333f, 2.3567f),
            new VanillaCubeDefinition("body", 0, 0, -2f, -2f, -2.5f, 4f, 4f, 5f));
        body.AddOrReplaceChild("stinger", VanillaPartPose.Offset(0f, 0.5f, 2.5f),
            new VanillaCubeDefinition("stinger", 13, 2, 0f, -0.5f, 0f, 0f, 1f, 1f));
        bone.AddOrReplaceChild("right_wing", VanillaPartPose.OffsetAndRotation(-1f, -0.6667f, 0.8567f, 0.2182f, 0.3491f, 0f),
            new VanillaCubeDefinition("right_wing", 3, 9, -3f, 0f, 0f, 3f, 0f, 3f));
        bone.AddOrReplaceChild("left_wing", VanillaPartPose.OffsetAndRotation(1f, -0.6667f, 0.8567f, 0.2182f, -0.3491f, 0f),
            new VanillaCubeDefinition("left_wing", -3, 9, 0f, 0f, 0f, 3f, 0f, 3f, Mirror: true));
        bone.AddOrReplaceChild("front_legs", VanillaPartPose.Offset(0f, 3.3333f, 1.8567f),
            new VanillaCubeDefinition("front_legs", 13, 0, -1.5f, 0f, 0f, 3f, 1f, 0f));
        bone.AddOrReplaceChild("middle_legs", VanillaPartPose.Offset(0f, 3.3333f, 2.8567f),
            new VanillaCubeDefinition("middle_legs", 13, 1, -1.5f, 0f, 0f, 3f, 1f, 0f));
        bone.AddOrReplaceChild("back_legs", VanillaPartPose.Offset(0f, 3.3333f, 3.8567f),
            new VanillaCubeDefinition("back_legs", 13, 2, -1.5f, 0f, 0f, 3f, 1f, 0f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildDolphinMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 21.5f, 0f),
            new VanillaCubeDefinition("body", 20, 0, -3f, -2.5f, -4f, 6f, 5f, 8f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 1f, -4f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -3.5f, -4f, 6f, 5f, 4f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, 0.5f, -4f),
            new VanillaCubeDefinition("nose", 0, 9, -1f, -1f, -2f, 2f, 2f, 2f));
        body.AddOrReplaceChild("left_fin", VanillaPartPose.OffsetAndRotation(1.8f, 0.85f, -2.6f, 0.8727f, 0f, 1.7017f),
            new VanillaCubeDefinition("left_fin", 34, 18, -0.5f, -1.5f, -0.5f, 1f, 3f, 6f));
        body.AddOrReplaceChild("right_fin", VanillaPartPose.OffsetAndRotation(-1.8f, 0.85f, -2.6f, 0.8727f, 0f, -1.7017f),
            new VanillaCubeDefinition("right_fin", 48, 18, -0.5f, -1.5f, -0.5f, 1f, 3f, 6f, Mirror: true));
        var tail = body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, 1f, 4f),
            new VanillaCubeDefinition("tail", 0, 13, -2f, -1.5f, 0f, 4f, 3f, 7f));
        tail.AddOrReplaceChild("tail_fin", VanillaPartPose.Offset(0f, 0f, 6f),
            new VanillaCubeDefinition("tail_fin", 22, 13, -4f, -0.5f, -1f, 8f, 1f, 4f));
        body.AddOrReplaceChild("back_fin", VanillaPartPose.OffsetAndRotation(0f, -1f, -2.7f, 0.8727f, 0f, 0f),
            new VanillaCubeDefinition("back_fin", 42, 0, -0.5f, -1f, 1f, 1f, 3f, 4f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildAxolotlMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var body = rootPart.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -1.25f, 1.75f),
            new VanillaCubeDefinition("body", 0, 0, -2f, -0.75f, -2.75f, 4f, 2f, 6f));
        body.AddOrReplaceChild("body_fin", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_fin", 0, 12, 0f, -1.75f, -2.75f, 0f, 3f, 5f));
        body.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2f, 0.25f, -1.25f),
            new VanillaCubeDefinition("right_front_leg", 20, 16, -3f, 0f, -0.5f, 3f, 0f, 1f));
        var rightHind = body.AddOrReplaceChild("right_hind_leg", VanillaPartPose.OffsetAndRotation(-2f, 0.25f, 1.75f, 0f, 1.5708f, 1.5708f),
            new VanillaCubeDefinition("right_hind_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rightHind.AddOrReplaceChild("right_leg_r1", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, -1.5708f, 0f, 1.5708f),
            new VanillaCubeDefinition("right_leg_r1", 20, 14, 0f, 0f, -0.5f, 3f, 0f, 1f));
        body.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2f, 0.25f, -1.25f),
            new VanillaCubeDefinition("left_front_leg", 20, 13, 0f, 0f, -0.5f, 3f, 0f, 1f));
        body.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2f, 0.25f, 1.75f),
            new VanillaCubeDefinition("left_hind_leg", 20, 14, 0f, 0f, -0.5f, 3f, 0f, 1f));
        body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -0.25f, 3.25f),
            new VanillaCubeDefinition("tail", 10, 9, 0f, -1.5f, -1f, 0f, 3f, 8f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0.25f, -2.75f),
            new VanillaCubeDefinition("head", 0, 8, -3f, -2f, -4f, 6f, 3f, 4f));
        head.AddOrReplaceChild("left_gills", VanillaPartPose.Offset(3f, -0.5f, -2f),
            new VanillaCubeDefinition("left_gills", 20, 8, 0f, -3.5f, 0f, 3f, 5f, 0f));
        head.AddOrReplaceChild("right_gills", VanillaPartPose.Offset(-3f, -0.5f, -2f),
            new VanillaCubeDefinition("right_gills", 20, 3, -3f, -3.5f, 0f, 3f, 5f, 0f));
        head.AddOrReplaceChild("top_gills", VanillaPartPose.Offset(0f, -2f, -2f),
            new VanillaCubeDefinition("top_gills", 20, 0, -3f, -3f, 0f, 6f, 3f, 0f));
        return new VanillaMeshDefinition(root);
    }
}
