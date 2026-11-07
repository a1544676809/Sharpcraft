// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// MagmaCubeModel: eight stacked segment cubes plus the inside cube, all in raw model coordinates. Parts are declared
/// with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-688).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.slime.MagmaCubeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/MagmaCubeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class MagmaCubeModelDefinition
{
    public const int MagmaCubeEntityTypeId = 81;

    private static readonly VanillaMeshDefinition MagmaCubeMesh = BuildMagmaCubeMesh();

    private static readonly CompiledEntityModel MagmaCubeCompiled =
        VanillaMeshBakery.Build(MagmaCubeMesh, 64, 64);

    public static readonly EntityModelDefinition Definition = new(
        "magma_cube",
        64,
        64,
        [.. VanillaMeshBake.Bake(MagmaCubeMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/slime/magmacube",
        compiled: MagmaCubeCompiled);

    private static VanillaMeshDefinition BuildMagmaCubeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("cube0", VanillaPartPose.Zero, new VanillaCubeDefinition("cube0", 0, 0, -4f, 16f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube1", VanillaPartPose.Zero, new VanillaCubeDefinition("cube1", 0, 9, -4f, 17f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube2", VanillaPartPose.Zero, new VanillaCubeDefinition("cube2", 0, 18, -4f, 18f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube3", VanillaPartPose.Zero, new VanillaCubeDefinition("cube3", 0, 27, -4f, 19f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube4", VanillaPartPose.Zero, new VanillaCubeDefinition("cube4", 32, 0, -4f, 20f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube5", VanillaPartPose.Zero, new VanillaCubeDefinition("cube5", 32, 9, -4f, 21f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube6", VanillaPartPose.Zero, new VanillaCubeDefinition("cube6", 32, 18, -4f, 22f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("cube7", VanillaPartPose.Zero, new VanillaCubeDefinition("cube7", 32, 27, -4f, 23f, -4f, 8f, 1f, 8f));
        root.AddOrReplaceChild("inside_cube", VanillaPartPose.Zero, new VanillaCubeDefinition("inside_cube", 24, 40, -2f, 18f, -2f, 4f, 4f, 4f));
        return new VanillaMeshDefinition(root);
    }
}
