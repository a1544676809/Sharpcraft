// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>Baby strider/husk/snifflet/armadillo models. Parts are declared with the <c>VanillaMeshDefinition</c> DSL
/// and baked via <c>VanillaMeshBake</c> (T-700).</summary>
public static class BabyModelDefinitionsOdd
{
    private static bool Unrolled(EntityRenderState state) => !state.IsHidingInShell;

    private static bool Rolled(EntityRenderState state) => state.IsHidingInShell;

    private static readonly VanillaMeshDefinition StriderMesh = BuildStriderMesh();
    private static readonly EntityModelPart[] StriderParts = BuildParts(StriderMesh,
        ["body", "right_leg", "left_leg", "bristle0", "bristle1", "bristle2"]);

    private static readonly VanillaMeshDefinition SniffletMesh = BuildSniffletMesh();
    private static readonly EntityModelPart[] SniffletParts = [.. VanillaMeshBake.Bake(SniffletMesh, 0, 0)];

    private static readonly VanillaMeshDefinition ArmadilloMesh = BuildArmadilloMesh();
    private static readonly EntityModelPart[] ArmadilloParts = BuildArmadilloParts();

    public static readonly EntityModelDefinition Strider = new(
        "baby_strider", 32, 32, StriderParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/strider/strider_baby");

    public static readonly EntityModelDefinition Husk = new(
        "baby_husk", 64, 64, BabyModelDefinitionsHumanoid.Zombie.Parts,
        PlayerModelDefinition.DefaultFaceTransforms, "entity/zombie/husk_baby");

    public static readonly EntityModelDefinition Snifflet = new(
        "baby_sniffer", 128, 128, SniffletParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/sniffer/snifflet");

    public static readonly EntityModelDefinition Armadillo = new(
        "baby_armadillo", 64, 64, ArmadilloParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/armadillo/armadillo_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [132] = Strider,
            [68] = Husk,
            [122] = Snifflet,
            [4] = Armadillo
        };

    private static EntityModelPart[] BuildParts(VanillaMeshDefinition mesh, string[] order)
    {
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in VanillaMeshBake.Bake(mesh, 0, 0))
        {
            byName[part.Name] = part;
        }

        return [.. order.Select(name => byName[name])];
    }

    private static EntityModelPart[] BuildArmadilloParts()
    {
        var baked = VanillaMeshBake.Bake(ArmadilloMesh, 0, 0);
        return [.. baked];
    }

    private static VanillaMeshDefinition BuildStriderMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 16.75f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -3.5f, -3.75f, -4f, 7f, 7f, 8f));
        body.AddOrReplaceChild("bristle0", VanillaPartPose.Offset(0f, -4.25f, 2f),
            new VanillaCubeDefinition("bristle0", 0, 21, -3.5f, -2.5f, 0f, 7f, 3f, 0f));
        body.AddOrReplaceChild("bristle1", VanillaPartPose.Offset(0f, -4.25f, 0f),
            new VanillaCubeDefinition("bristle1", 0, 18, -3.5f, -2.5f, 0f, 7f, 3f, 0f));
        body.AddOrReplaceChild("bristle2", VanillaPartPose.Offset(0f, -4.25f, -2f),
            new VanillaCubeDefinition("bristle2", 0, 15, -3.5f, -2.5f, 0f, 7f, 3f, 0f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 24, -1f, 0f, -1f, 2f, 4f, 2f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0f),
            new VanillaCubeDefinition("left_leg", 8, 24, -1f, 0f, -1f, 2f, 4f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildSniffletMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var bone = root.AddOrReplaceChild("bone", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("bone", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var body = bone.AddOrReplaceChild("body", VanillaPartPose.Offset(6f, -3f, -9.5f),
            new VanillaCubeDefinition("body", 0, 35, -13f, -14f, -0.5f, 14f, 14f, 20f, Grow: 0.25f));
        body.AddOrReplaceChild("body_main", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_main", 0, 0, -13f, -14f, -0.5f, 14f, 15f, 20f));
        body.AddOrReplaceChild("body_flat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_flat", 68, 0, -13f, 0f, -0.5f, 14f, 0f, 20f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(-6f, -4.75f, 0f),
            new VanillaCubeDefinition("head", 68, 20, -5f, -4.25f, -7.5f, 10f, 9f, 9f));
        head.AddOrReplaceChild("head_flat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_flat", 88, 20, -5f, 3.75f, -7.5f, 10f, 0f, 9f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(5f, -4.25f, -1.5f),
            new VanillaCubeDefinition("left_ear", 104, 38, 0f, 0f, -2f, 1f, 11f, 3f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-5f, -4.25f, -1.5f),
            new VanillaCubeDefinition("right_ear", 96, 38, -1f, 0f, -2f, 1f, 11f, 3f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -1.25f, -9.5f),
            new VanillaCubeDefinition("nose", 68, 47, -5f, -3f, -2f, 10f, 3f, 4f));
        head.AddOrReplaceChild("lower_beak", VanillaPartPose.Offset(0f, 1.25f, -9.5f),
            new VanillaCubeDefinition("lower_beak", 68, 38, -5f, -2.5f, -2f, 10f, 5f, 4f));
        bone.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-4f, -4f, -7f),
            new VanillaCubeDefinition("right_front_leg", 0, 69, -2f, -1f, -2f, 4f, 5f, 4f));
        bone.AddOrReplaceChild("right_mid_leg", VanillaPartPose.Offset(-4f, -4f, 0f),
            new VanillaCubeDefinition("right_mid_leg", 0, 78, -2f, -1f, -2f, 4f, 5f, 4f));
        bone.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-4f, -4f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 0, 87, -2f, -1f, -2f, 4f, 5f, 4f));
        bone.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(4f, -4f, -7f),
            new VanillaCubeDefinition("left_front_leg", 16, 69, -2f, -1f, -2f, 4f, 5f, 4f));
        bone.AddOrReplaceChild("left_mid_leg", VanillaPartPose.Offset(4f, -4f, 0f),
            new VanillaCubeDefinition("left_mid_leg", 16, 78, -2f, -1f, -2f, 4f, 5f, 4f));
        bone.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(4f, -4f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 16, 87, -2f, -1f, -2f, 4f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildArmadilloMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 20f, 0.5f),
            new VanillaCubeDefinition("body", 0, 0, -2.5f, -2f, -3.5f, 5f, 4f, 7f, Grow: 0.3f, VisibleWhen: Unrolled));
        body.AddOrReplaceChild("body_inner", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_inner", 0, 11, -2.5f, -2f, -3f, 5f, 4f, 6f, VisibleWhen: Unrolled));
        var tail = body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, 0f, 3.4f),
            new VanillaCubeDefinition("tail", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, VisibleWhen: Unrolled));
        tail.AddOrReplaceChild("tail_cube", VanillaPartPose.OffsetAndRotation(0f, 1.5f, 1f, -1.0472f, 0f, 0f),
            new VanillaCubeDefinition("tail_cube", 22, 11, -0.5f, -0.5f, -2f, 1f, 1f, 4f, VisibleWhen: Unrolled));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, -3.2f),
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, VisibleWhen: Unrolled));
        var headCube = head.AddOrReplaceChild("head_cube", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0.7417649f, 0f, 0f),
            new VanillaCubeDefinition("head_cube", 20, 17, -1f, -2f, -4f, 2f, 2f, 4f));
        headCube.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-1f, -2f, -0.3f, -0.4363f, -0.1134f, 0.0524f),
            new VanillaCubeDefinition("right_ear", 28, 8, -1.8f, -2f, 0f, 2f, 3f, 0f, Mirror: true));
        headCube.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(1f, -2f, -0.3f, -0.4363f, 0.1134f, -0.0524f),
            new VanillaCubeDefinition("left_ear", 28, 8, -0.2f, -2f, 0f, 2f, 3f, 0f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-1.5f, 22f, 2.5f),
            new VanillaCubeDefinition("right_hind_leg", 20, 27, -1f, 0f, -1f, 2f, 2f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1.5f, 22f, 2.5f),
            new VanillaCubeDefinition("left_hind_leg", 20, 27, -1f, 0f, -1f, 2f, 2f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(1.5f, 22f, -1.5f),
            new VanillaCubeDefinition("right_front_leg", 20, 23, -1f, 0f, -1f, 2f, 2f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(-1.5f, 22f, -1.5f),
            new VanillaCubeDefinition("left_front_leg", 24, 0, -1f, 0f, -1f, 2f, 2f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("cube", VanillaPartPose.Offset(0f, 20.7f, 0.5f),
            new VanillaCubeDefinition("cube", 0, 25, -3f, -3f, -3f, 6f, 6f, 6f, Grow: 0.3f, VisibleWhen: Rolled));
        return new VanillaMeshDefinition(root);
    }
}
