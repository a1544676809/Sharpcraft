// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultArmadilloModel: grown body shell with head/ears/tail, four small legs, and a separate rolled-up cube.
/// The body/legs/tail hide while the armadillo is in its shell and the cube replaces them. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-686); the unrolled/rolled predicates are
/// per-cube <c>VisibleWhen</c> values.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.armadillo.ArmadilloModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/armadillo/ArmadilloModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ArmadilloModelDefinition
{
    public const int ArmadilloEntityTypeId = 4;

    private static bool Unrolled(EntityRenderState state) => !state.IsHidingInShell;

    private static bool Rolled(EntityRenderState state) => state.IsHidingInShell;

    private static readonly VanillaMeshDefinition ArmadilloMesh = BuildArmadilloMesh();

    public static readonly EntityModelDefinition Definition = new(
        "armadillo",
        64,
        64,
        [.. VanillaMeshBake.Bake(ArmadilloMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/armadillo/armadillo");

    private static VanillaMeshDefinition BuildArmadilloMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 21f, 4f),
            new VanillaCubeDefinition("body", 0, 40, -4f, -7f, -10f, 8f, 8f, 12f, VisibleWhen: Unrolled));
        body.AddOrReplaceChild("body_outer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_outer", 0, 20, -4f, -7f, -10f, 8f, 8f, 12f,
                Grow: 0.3f, VisibleWhen: Unrolled));
        body.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(0f, -3f, 1f, 0.5061f, 0f, 0f),
            new VanillaCubeDefinition("tail", 44, 53, -0.5f, -0.0865f, 0.0933f, 1f, 6f, 1f,
                VisibleWhen: Unrolled));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -2f, -11f),
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, VisibleWhen: Unrolled));
        head.AddOrReplaceChild("head_cube", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, -0.3927f, 0f, 0f),
            new VanillaCubeDefinition("head_cube", 43, 15, -1.5f, -1f, -1f, 3f, 5f, 2f));
        var rightEar = head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-1f, -1f, 0f),
            new VanillaCubeDefinition("right_ear", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, VisibleWhen: Unrolled));
        rightEar.AddOrReplaceChild("right_ear_cube", VanillaPartPose.OffsetAndRotation(-0.5f, 0f, -0.6f, 0.1886f, -0.3864f, -0.0718f),
            new VanillaCubeDefinition("right_ear_cube", 43, 10, -2f, -3f, 0f, 2f, 5f, 0f));
        var leftEar = head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(1f, -2f, 0f),
            new VanillaCubeDefinition("left_ear", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, VisibleWhen: Unrolled));
        leftEar.AddOrReplaceChild("left_ear_cube", VanillaPartPose.OffsetAndRotation(0.5f, 1f, -0.6f, 0.1886f, 0.3864f, 0.0718f),
            new VanillaCubeDefinition("left_ear_cube", 47, 10, 0f, -3f, 0f, 2f, 5f, 0f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2f, 21f, 4f),
            new VanillaCubeDefinition("right_hind_leg", 51, 31, -1f, 0f, -1f, 2f, 3f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2f, 21f, 4f),
            new VanillaCubeDefinition("left_hind_leg", 42, 31, -1f, 0f, -1f, 2f, 3f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2f, 21f, -4f),
            new VanillaCubeDefinition("right_front_leg", 51, 43, -1f, 0f, -1f, 2f, 3f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2f, 21f, -4f),
            new VanillaCubeDefinition("left_front_leg", 42, 43, -1f, 0f, -1f, 2f, 3f, 2f, VisibleWhen: Unrolled));
        root.AddOrReplaceChild("cube", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("cube", 0, 0, -5f, -10f, -6f, 10f, 10f, 10f, VisibleWhen: Rolled));
        return new VanillaMeshDefinition(root);
    }
}
