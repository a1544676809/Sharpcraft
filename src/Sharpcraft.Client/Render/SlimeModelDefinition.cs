// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// SlimeModel's inner cube/eyes/mouth plus the separate outer cube, expressed directly in vanilla cube coordinates.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-688).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.slime.SlimeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/SlimeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SlimeModelDefinition
{
    public const int SlimeEntityTypeId = 120;

    private static readonly VanillaMeshDefinition SlimeMesh = BuildSlimeMesh();

    public static readonly EntityModelDefinition Definition = new(
        "slime",
        64,
        32,
        [.. VanillaMeshBake.Bake(SlimeMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/slime/slime");

    private static VanillaMeshDefinition BuildSlimeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("cube", VanillaPartPose.Zero,
            new VanillaCubeDefinition("cube", 0, 16, -3f, 17f, -3f, 6f, 6f, 6f));
        root.AddOrReplaceChild("right_eye", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_eye", 32, 0, -3.25f, 18f, -3.5f, 2f, 2f, 2f));
        root.AddOrReplaceChild("left_eye", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_eye", 32, 4, 1.25f, 18f, -3.5f, 2f, 2f, 2f));
        root.AddOrReplaceChild("mouth", VanillaPartPose.Zero,
            new VanillaCubeDefinition("mouth", 32, 8, 0f, 21f, -3.5f, 1f, 1f, 1f));
        root.AddOrReplaceChild("outer_cube", VanillaPartPose.Zero,
            new VanillaCubeDefinition("outer_cube", 0, 0, -4f, 16f, -4f, 8f, 8f, 8f, Kind: EntityGeometryLayerKind.Translucent));
        return new VanillaMeshDefinition(root);
    }
}
