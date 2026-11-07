// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Pufferfish has three separate models for its puff state (0 small, 1 medium, 2 large), all sharing one texture.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-680).
/// </summary>
public static class PufferfishModelDefinition
{
    public const int PufferfishEntityTypeId = 110;

    /// <summary>Declarative DSL trees for the three puff states (T-680).</summary>
    private static readonly VanillaMeshDefinition SmallMesh = BuildSmallMesh();

    private static readonly EntityModelPart[] SmallParts = [.. VanillaMeshBake.Bake(SmallMesh, 0, 0)];

    private static readonly CompiledEntityModel SmallCompiled =
        VanillaMeshBakery.Build(SmallMesh, 32, 32);

    private static readonly VanillaMeshDefinition MediumMesh = BuildMediumMesh();

    private static readonly EntityModelPart[] MediumParts = [.. VanillaMeshBake.Bake(MediumMesh, 0, 0)];

    private static readonly CompiledEntityModel MediumCompiled =
        VanillaMeshBakery.Build(MediumMesh, 32, 32);

    private static readonly VanillaMeshDefinition LargeMesh = BuildLargeMesh();

    private static readonly EntityModelPart[] LargeParts = [.. VanillaMeshBake.Bake(LargeMesh, 0, 0)];

    private static readonly CompiledEntityModel LargeCompiled =
        VanillaMeshBakery.Build(LargeMesh, 32, 32);

    /// <summary>Puff state ids: 0 small, 1 medium, 2 large.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["small", "medium", "large"];

    public static readonly EntityModelDefinition Definition = new(
        "pufferfish",
        32,
        32,
        SmallParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fish/pufferfish",
        compiled: SmallCompiled);

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["small"] = Definition,
            ["medium"] = new("pufferfish_medium", 32, 32, MediumParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/fish/pufferfish", compiled: MediumCompiled),
            ["large"] = new("pufferfish_large", 32, 32, LargeParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/fish/pufferfish", compiled: LargeCompiled)
        };

    private static VanillaMeshDefinition BuildSmallMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 23f, 0f),
            new VanillaCubeDefinition("body", 0, 27, -1.5f, -2f, -1.5f, 3f, 2f, 3f));
        root.AddOrReplaceChild("right_eye", VanillaPartPose.Offset(0f, 20f, 0f),
            new VanillaCubeDefinition("right_eye", 24, 6, -1.5f, 0f, -1.5f, 1f, 1f, 1f));
        root.AddOrReplaceChild("left_eye", VanillaPartPose.Offset(0f, 20f, 0f),
            new VanillaCubeDefinition("left_eye", 28, 6, 0.5f, 0f, -1.5f, 1f, 1f, 1f));
        root.AddOrReplaceChild("back_fin", VanillaPartPose.Offset(0f, 22f, 1.5f),
            new VanillaCubeDefinition("back_fin", -3, 0, -1.5f, 0f, 0f, 3f, 0f, 3f));
        root.AddOrReplaceChild("right_fin", VanillaPartPose.Offset(-1.5f, 22f, -1.5f),
            new VanillaCubeDefinition("right_fin", 25, 0, -1f, 0f, 0f, 1f, 0f, 2f));
        root.AddOrReplaceChild("left_fin", VanillaPartPose.Offset(1.5f, 22f, -1.5f),
            new VanillaCubeDefinition("left_fin", 25, 0, 0f, 0f, 0f, 1f, 0f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildMediumMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("body", 12, 22, -2.5f, -5f, -2.5f, 5f, 5f, 5f));
        root.AddOrReplaceChild("right_blue_fin", VanillaPartPose.Offset(-2.5f, 18f, -1.5f),
            new VanillaCubeDefinition("right_blue_fin", 24, 0, -2f, 0f, 0f, 2f, 0f, 2f));
        root.AddOrReplaceChild("left_blue_fin", VanillaPartPose.Offset(2.5f, 18f, -1.5f),
            new VanillaCubeDefinition("left_blue_fin", 24, 3, 0f, 0f, 0f, 2f, 0f, 2f));
        root.AddOrReplaceChild("top_front_fin", VanillaPartPose.OffsetAndRotation(0f, 17f, -2.5f, -MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("top_front_fin", 19, 17, -2.5f, -1f, 0f, 5f, 1f, 0f));
        root.AddOrReplaceChild("top_back_fin", VanillaPartPose.OffsetAndRotation(0f, 17f, 2.5f, MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("top_back_fin", 11, 17, -2.5f, -1f, 0f, 5f, 1f, 0f));
        root.AddOrReplaceChild("right_front_fin", VanillaPartPose.OffsetAndRotation(-2.5f, 22f, -2.5f, 0f, MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("right_front_fin", 5, 17, -1f, -5f, 0f, 1f, 5f, 0f));
        root.AddOrReplaceChild("right_back_fin", VanillaPartPose.OffsetAndRotation(-2.5f, 22f, 2.5f, 0f, -MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("right_back_fin", 9, 17, -1f, -5f, 0f, 1f, 5f, 0f));
        root.AddOrReplaceChild("left_back_fin", VanillaPartPose.OffsetAndRotation(2.5f, 22f, 2.5f, 0f, MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("left_back_fin", 1, 17, 0f, -5f, 0f, 1f, 5f, 0f));
        root.AddOrReplaceChild("left_front_fin", VanillaPartPose.OffsetAndRotation(2.5f, 22f, -2.5f, 0f, -MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("left_front_fin", 1, 17, 0f, -5f, 0f, 1f, 5f, 0f));
        root.AddOrReplaceChild("bottom_back_fin", VanillaPartPose.OffsetAndRotation(-2.5f, 22f, 2.5f, -MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("bottom_back_fin", 18, 20, 0f, 0f, 0f, 5f, 1f, 0f));
        root.AddOrReplaceChild("bottom_front_fin", VanillaPartPose.OffsetAndRotation(0f, 22f, -2.5f, MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("bottom_front_fin", 17, 19, -2.5f, 0f, 0f, 5f, 1f, 1f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildLargeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f));
        root.AddOrReplaceChild("right_blue_fin", VanillaPartPose.Offset(-4f, 15f, -2f),
            new VanillaCubeDefinition("right_blue_fin", 24, 0, -2f, 0f, -1f, 2f, 1f, 2f));
        root.AddOrReplaceChild("left_blue_fin", VanillaPartPose.Offset(4f, 15f, -2f),
            new VanillaCubeDefinition("left_blue_fin", 24, 3, 0f, 0f, -1f, 2f, 1f, 2f));
        root.AddOrReplaceChild("top_front_fin", VanillaPartPose.OffsetAndRotation(0f, 14f, -4f, -MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("top_front_fin", 15, 17, -4f, -1f, 0f, 8f, 1f, 0f));
        root.AddOrReplaceChild("top_middle_fin", VanillaPartPose.Offset(0f, 14f, 0f),
            new VanillaCubeDefinition("top_middle_fin", 14, 16, -4f, -1f, 0f, 8f, 1f, 1f));
        root.AddOrReplaceChild("top_back_fin", VanillaPartPose.OffsetAndRotation(0f, 14f, 4f, MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("top_back_fin", 23, 18, -4f, -1f, 0f, 8f, 1f, 0f));
        root.AddOrReplaceChild("right_front_fin", VanillaPartPose.OffsetAndRotation(-4f, 22f, -4f, 0f, MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("right_front_fin", 5, 17, -1f, -8f, 0f, 1f, 8f, 0f));
        root.AddOrReplaceChild("left_front_fin", VanillaPartPose.OffsetAndRotation(4f, 22f, -4f, 0f, -MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("left_front_fin", 1, 17, 0f, -8f, 0f, 1f, 8f, 0f));
        root.AddOrReplaceChild("bottom_front_fin", VanillaPartPose.OffsetAndRotation(0f, 22f, -4f, MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("bottom_front_fin", 15, 20, -4f, 0f, 0f, 8f, 1f, 0f));
        root.AddOrReplaceChild("bottom_middle_fin", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("bottom_middle_fin", 15, 20, -4f, 0f, 0f, 8f, 1f, 0f));
        root.AddOrReplaceChild("bottom_back_fin", VanillaPartPose.OffsetAndRotation(0f, 22f, 4f, -MathF.PI / 4f, 0f, 0f),
            new VanillaCubeDefinition("bottom_back_fin", 15, 20, -4f, 0f, 0f, 8f, 1f, 0f));
        root.AddOrReplaceChild("right_back_fin", VanillaPartPose.OffsetAndRotation(-4f, 22f, 4f, 0f, -MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("right_back_fin", 9, 17, -1f, -8f, 0f, 1f, 8f, 0f));
        root.AddOrReplaceChild("left_back_fin", VanillaPartPose.OffsetAndRotation(4f, 22f, 4f, 0f, MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("left_back_fin", 9, 17, 0f, -8f, 0f, 1f, 8f, 0f));
        return new VanillaMeshDefinition(root);
    }
}
