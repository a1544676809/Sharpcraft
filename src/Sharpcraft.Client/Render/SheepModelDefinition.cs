// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SheepModel (base) + SheepFurModel (wool), both expressed as independent raw cubes. The base legs are 4x12x4;
/// the wool legs are separate 4x6x4 cubes with grow 0.5 and must not be modeled as overlays of the base legs. Parts are
/// declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-677).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.sheep.SheepModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/sheep/SheepModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SheepModelDefinition
{
    public const int SheepEntityTypeId = 114;
    private const string WoolTexture = "entity/sheep/sheep_wool";

    /// <summary>Declarative DSL tree for the twelve base + wool parts (T-677).</summary>
    private static readonly VanillaMeshDefinition SheepMesh = BuildSheepMesh();

    private static readonly CompiledEntityModel CompiledModel = VanillaMeshBakery.Build(SheepMesh, 64, 32);

    public static readonly EntityModelDefinition Definition = new(
        "sheep",
        64,
        32,
        [.. VanillaMeshBake.Bake(SheepMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/sheep/sheep",
        compiled: CompiledModel);

    private static VanillaMeshDefinition BuildSheepMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 6f, -8f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -4f, -6f, 6f, 6f, 8f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 5f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 28, 8, -4f, -10f, -7f, 8f, 16f, 6f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3f, 12f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3f, 12f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3f, 12f, -5f),
            new VanillaCubeDefinition("right_front_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3f, 12f, -5f),
            new VanillaCubeDefinition("left_front_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("wool_head", VanillaPartPose.Offset(0f, 6f, -8f),
            new VanillaCubeDefinition("wool_head", 0, 0, -3f, -4f, -4f, 6f, 6f, 6f, Grow: 0.6f, TexturePath: WoolTexture));
        root.AddOrReplaceChild("wool_body", VanillaPartPose.OffsetAndRotation(0f, 5f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("wool_body", 28, 8, -4f, -10f, -7f, 8f, 16f, 6f, Grow: 1.75f, TexturePath: WoolTexture));
        root.AddOrReplaceChild("wool_right_hind_leg", VanillaPartPose.Offset(-3f, 12f, 7f),
            new VanillaCubeDefinition("wool_right_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f, Grow: 0.5f, TexturePath: WoolTexture));
        root.AddOrReplaceChild("wool_left_hind_leg", VanillaPartPose.Offset(3f, 12f, 7f),
            new VanillaCubeDefinition("wool_left_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f, Mirror: true, Grow: 0.5f, TexturePath: WoolTexture));
        root.AddOrReplaceChild("wool_right_front_leg", VanillaPartPose.Offset(-3f, 12f, -5f),
            new VanillaCubeDefinition("wool_right_front_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f, Grow: 0.5f, TexturePath: WoolTexture));
        root.AddOrReplaceChild("wool_left_front_leg", VanillaPartPose.Offset(3f, 12f, -5f),
            new VanillaCubeDefinition("wool_left_front_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f, Mirror: true, Grow: 0.5f, TexturePath: WoolTexture));
        return new VanillaMeshDefinition(root);
    }
}
