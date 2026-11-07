// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Humanoid mob definitions. Every part is written in the reference's y-down pivot/cube form and converted by the
/// centralized bake: all definitions here declare their parts with the <c>VanillaMeshDefinition</c> DSL and bake via
/// <c>VanillaMeshBake.Bake</c> (T-672/T-673/T-675), including the clothing overlay parts in <c>WithClothing</c> that
/// carry a <c>PoseSource</c>. No per-model Bottom/CentreX arithmetic remains here.
/// </summary>
public static class HumanoidMobModelDefinition
{
    public const int SkeletonEntityTypeId = 118;
    public const int StrayEntityTypeId = 131;
    public const int BoggedEntityTypeId = 16;
    public const int WitherSkeletonEntityTypeId = 150;
    public const int HuskEntityTypeId = 68;
    public const int DrownedEntityTypeId = 39;
    public const int EndermanEntityTypeId = 42;
    public const int ZombieEntityTypeId = 154;

    /// <summary>Declarative DSL tree for the enderman's six parts (T-673).</summary>
    private static readonly VanillaMeshDefinition EndermanMesh = BuildEndermanMesh();

    private static readonly CompiledEntityModel EndermanCompiled =
        VanillaMeshBakery.Build(EndermanMesh, 64, 32, modelHeight: 25f);

    public static readonly EntityModelDefinition Enderman = new(
        "enderman",
        64,
        32,
        [.. VanillaMeshBake.Bake(EndermanMesh, 0, 0, modelHeight: 25f)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/enderman/enderman", 1f, "entity/enderman/enderman_eyes",
        compiled: EndermanCompiled);

    private static VanillaMeshDefinition BuildEndermanMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -13f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Overlays: [new("hat", -0.5f, 0, 16)]));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -14f, 0f),
            new VanillaCubeDefinition("body", 32, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, -12f, 0f),
            new VanillaCubeDefinition("left_arm", 56, 0, -1f, -2f, -1f, 2f, 30f, 2f, Mirror: true));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, -12f, 0f),
            new VanillaCubeDefinition("right_arm", 56, 0, -1f, -2f, -1f, 2f, 30f, 2f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, -5f, 0f),
            new VanillaCubeDefinition("left_leg", 56, 0, -1f, 0f, -1f, 2f, 30f, 2f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, -5f, 0f),
            new VanillaCubeDefinition("right_leg", 56, 0, -1f, 0f, -1f, 2f, 30f, 2f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>Declarative DSL tree for the drowned's six parts (T-673).</summary>
    private static readonly VanillaMeshDefinition DrownedMesh = BuildDrownedMesh();

    private static readonly CompiledEntityModel DrownedCompiled =
        VanillaMeshBakery.Build(DrownedMesh, 64, 64);

    public static readonly EntityModelDefinition Drowned = new(
        "drowned",
        64,
        64,
        [.. VanillaMeshBake.Bake(DrownedMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/drowned",
        compiled: DrownedCompiled);

    private static VanillaMeshDefinition BuildDrownedMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Overlays: [new("hat", 0.5f, 32, 0), DrownedOuter(0, 0)]));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f,
                Overlays: [DrownedOuter(16, 16)]));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 32, 48, -1f, -2f, -2f, 4f, 12f, 4f,
                Overlays: [DrownedOuter(32, 48)]));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f,
                Overlays: [DrownedOuter(40, 16)]));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 16, 48, -2f, 0f, -2f, 4f, 12f, 4f,
                Overlays: [DrownedOuter(16, 48)]));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f,
                Overlays: [DrownedOuter(0, 16)]));
        return new VanillaMeshDefinition(root);
    }

    private static EntityModelLayer DrownedOuter(int textureX, int textureY) =>
        new("drowned_outer", 0.25f, textureX, textureY, false, "entity/zombie/drowned_outer_layer", EntityGeometryLayerKind.Translucent);

    /// <summary>Declarative DSL tree for the husk's six parts (T-673).</summary>
    private static readonly VanillaMeshDefinition HuskMesh = BuildHuskMesh();

    private static readonly CompiledEntityModel HuskCompiled =
        VanillaMeshBakery.Build(HuskMesh, 64, 64);

    public static readonly EntityModelDefinition Husk = new(
        "husk",
        64,
        64,
        [.. VanillaMeshBake.Bake(HuskMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/husk",
        1.0625f,
        compiled: HuskCompiled);

    private static VanillaMeshDefinition BuildHuskMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Overlays: [new("hat", 0.5f, 32, 0)]));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>Declarative DSL tree for the skeleton's six parts (T-672).</summary>
    private static readonly VanillaMeshDefinition SkeletonMesh = BuildSkeletonMesh();

    private static readonly CompiledEntityModel SkeletonCompiled =
        VanillaMeshBakery.Build(SkeletonMesh, 64, 32);

    public static readonly EntityModelDefinition Skeleton = new(
        "skeleton",
        64,
        32,
        [.. VanillaMeshBake.Bake(SkeletonMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/skeleton/skeleton",
        compiled: SkeletonCompiled);

    private static VanillaMeshDefinition BuildSkeletonMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Overlays: [new("hat", 0.5f, 32, 0)]));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -1f, 2f, 12f, 2f, Mirror: true));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -1f, -2f, -1f, 2f, 12f, 2f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 16, -1f, 0f, -1f, 2f, 12f, 2f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -1f, 0f, -1f, 2f, 12f, 2f));
        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition Stray = WithClothing("stray", "entity/skeleton/stray", "entity/skeleton/stray_overlay", 0.25f);

    public static readonly EntityModelDefinition Bogged = WithClothing("bogged", "entity/skeleton/bogged", "entity/skeleton/bogged_overlay", 0.2f);

    private static EntityModelDefinition WithClothing(string name, string texturePath, string overlayPath, float grow)
    {
        var parts = new List<EntityModelPart>(Skeleton.Parts);
        var clothingMesh = BuildClothingMesh(overlayPath, grow);
        parts.AddRange(VanillaMeshBake.Bake(clothingMesh, 0, 0));
        var clothingCompiled = VanillaMeshBakery.Build(clothingMesh, 64, 32);
        var compiled = EntityModelLayerHelpers.WithModelLayer(
            SkeletonCompiled,
            clothingCompiled,
            "outer_",
            _ => overlayPath,
            fallbackTexture: overlayPath);
        return new EntityModelDefinition(
            name, 64, 32, parts, PlayerModelDefinition.DefaultFaceTransforms, texturePath,
            compiled: compiled);
    }

    /// <summary>
    /// Clothing overlay parts (T-675). Each is a top-level part that declares the base part whose pose it follows via
    /// <c>PoseSource</c>; otherwise the overlay would stay in bind pose while the body animates.
    /// </summary>
    private static VanillaMeshDefinition BuildClothingMesh(string overlayPath, float grow)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("outer_head", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("outer_head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Grow: grow, TexturePath: overlayPath,
                Overlays: [new("outer_hat", 0.5f, 32, 0, false, overlayPath)],
                PoseSource: "head"));
        root.AddOrReplaceChild("outer_body", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("outer_body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f,
                Grow: grow, TexturePath: overlayPath, PoseSource: "body"));
        root.AddOrReplaceChild("outer_left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("outer_left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f,
                Mirror: true, Grow: grow, TexturePath: overlayPath, PoseSource: "left_arm"));
        root.AddOrReplaceChild("outer_right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("outer_right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f,
                Grow: grow, TexturePath: overlayPath, PoseSource: "right_arm"));
        root.AddOrReplaceChild("outer_left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("outer_left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f,
                Mirror: true, Grow: grow, TexturePath: overlayPath, PoseSource: "left_leg"));
        root.AddOrReplaceChild("outer_right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("outer_right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f,
                Grow: grow, TexturePath: overlayPath, PoseSource: "right_leg"));
        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition WitherSkeleton = new(
        "wither_skeleton", 64, 32, Skeleton.Parts, PlayerModelDefinition.DefaultFaceTransforms,
        "entity/skeleton/wither_skeleton", compiled: SkeletonCompiled);

    /// <summary>Declarative DSL tree for the zombie's six parts (T-672).</summary>
    private static readonly VanillaMeshDefinition ZombieMesh = BuildZombieMesh();

    private static readonly CompiledEntityModel ZombieCompiled =
        VanillaMeshBakery.Build(ZombieMesh, 64, 64);

    public static readonly EntityModelDefinition Zombie = new(
        "zombie",
        64,
        64,
        [.. VanillaMeshBake.Bake(ZombieMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/zombie",
        compiled: ZombieCompiled);

    private static VanillaMeshDefinition BuildZombieMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f,
                Overlays: [new("hat", 0.5f, 32, 0)]));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f));
        return new VanillaMeshDefinition(root);
    }
}



