// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// HappyGhastModel (adult): one 16x16x16 body with nine tentacles hanging from its underside, scaled 4x.
/// Also bakes the adult/baby rope overlays: the same tree inflated by 0.2 model pixels and drawn only while the
/// ghast is a leash holder wearing a harness (RopesLayer). Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-681); the Tentacles table and scale
/// are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.ghast.HappyGhastModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/ghast/HappyGhastModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class HappyGhastModelDefinition
{
    public const int HappyGhastEntityTypeId = 59;
    public const string RopesTexture = "entity/ghast/happy_ghast_ropes";

    private static readonly (float X, float Y, float Z, float Length)[] Tentacles =
    [
        (-3.75f, 7f, -5f, 5f),
        (1.25f, 7f, -5f, 7f),
        (6.25f, 7f, -5f, 4f),
        (-6.25f, 7f, 0f, 5f),
        (-1.25f, 7f, 0f, 5f),
        (3.75f, 7f, 0f, 7f),
        (-3.75f, 7f, 5f, 8f),
        (1.25f, 7f, 5f, 8f),
        (6.25f, 7f, 5f, 5f)
    ];

    /// <summary>Declarative DSL tree for the body and the nine Tentacles-table tentacles (T-681).</summary>
    private static readonly VanillaMeshDefinition HappyGhastMesh = BuildHappyGhastMesh();

    private static readonly CompiledEntityModel HappyGhastCompiled =
        VanillaMeshBakery.Build(HappyGhastMesh, 64, 64);

    private static VanillaMeshDefinition BuildHappyGhastMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 16f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -8f, -8f, -8f, 16f, 16f, 16f));

        for (var i = 0; i < Tentacles.Length; i++)
        {
            var (x, y, z, length) = Tentacles[i];
            body.AddOrReplaceChild("tentacle" + i, VanillaPartPose.Offset(x, y, z),
                new VanillaCubeDefinition("tentacle" + i, 0, 0, -1f, 0f, -1f, 2f, length, 2f));
        }

        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition Definition = new(
        "happy_ghast",
        64,
        64,
        [.. VanillaMeshBake.Bake(HappyGhastMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/ghast/happy_ghast",
        4f,
        additionalTexturePaths: [RopesTexture],
        compiled: HappyGhastCompiled);

    private static readonly VanillaMeshDefinition AdultRopesMesh = BuildRopesMesh(baby: false);
    private static readonly VanillaMeshDefinition BabyRopesMesh = BuildRopesMesh(baby: true);
    private static readonly CompiledEntityModel AdultRopesCompiled =
        VanillaMeshBakery.Build(AdultRopesMesh, 64, 64);
    private static readonly CompiledEntityModel BabyRopesCompiled =
        VanillaMeshBakery.Build(BabyRopesMesh, 64, 64);

    /// <summary>Adult rope pass: the HappyGhast body/tentacles inflated by 0.2 model pixels.</summary>
    public static readonly EntityModelDefinition RopesDefinition = new(
        "happy_ghast_ropes",
        64,
        64,
        [.. VanillaMeshBake.Bake(AdultRopesMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        RopesTexture,
        4f,
        additionalTexturePaths: [RopesTexture],
        compiled: AdultRopesCompiled);

    /// <summary>Baby rope pass: the baby body/tentacles inflated by 0.2 model pixels, 0.2375×4 scale.</summary>
    public static readonly EntityModelDefinition RopesBabyDefinition = new(
        "happy_ghast_baby_ropes",
        64,
        64,
        [.. VanillaMeshBake.Bake(BabyRopesMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        RopesTexture,
        0.95f,
        additionalTexturePaths: [RopesTexture],
        compiled: BabyRopesCompiled);

    private static VanillaMeshDefinition BuildRopesMesh(bool baby)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 16f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -8f, -8f, -8f, 16f, 16f, 16f, Grow: 0.2f));
        if (baby)
        {
            body.AddOrReplaceChild("inner_body", VanillaPartPose.Offset(0f, 8f, 0f),
                new VanillaCubeDefinition("inner_body", 0, 32, -8f, -16f, -8f, 16f, 16f, 16f, Grow: -0.3f));
        }

        for (var i = 0; i < Tentacles.Length; i++)
        {
            var (x, y, z, length) = Tentacles[i];
            body.AddOrReplaceChild("tentacle" + i, VanillaPartPose.Offset(x, y, z),
                new VanillaCubeDefinition("tentacle" + i, 0, 0, -1f, 0f, -1f, 2f, length, 2f, Grow: 0.2f));
        }

        return new VanillaMeshDefinition(root);
    }
}
