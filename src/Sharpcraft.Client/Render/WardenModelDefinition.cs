// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// WardenModel: base body/limbs plus four emissive sub-layers (bioluminescent, pulsating spots, heart and glowing
/// tendrils). Each emissive layer re-uses a subset of the base geometry with its own texture. Base parts are declared
/// with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-686).
/// </summary>
/// <remarks>Bind pose only; attack/sonic-boom/dig/emerge/roar/sniff animations and the spots two-frame pulse are not ported.</remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.warden.WardenModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/warden/WardenModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WardenModelDefinition
{
    public const int WardenEntityTypeId = 146;

    private static readonly VanillaMeshDefinition WardenMesh = BuildWardenMesh();

    private static readonly EntityModelPart[] BaseParts = [.. VanillaMeshBake.Bake(WardenMesh, 0, 0)];

    private static readonly CompiledEntityModel BaseCompiled =
        VanillaMeshBakery.Build(WardenMesh, 128, 128, texturePath: "entity/warden/warden");

    private static readonly CompiledEntityModel CompiledModel = BuildCompiled();

    public static readonly EntityModelDefinition Definition = new(
        "warden",
        128,
        128,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/warden/warden",
        compiled: CompiledModel);

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>(BaseParts);
        AddLayer(parts, "bioluminescent", "entity/warden/warden_bioluminescent_layer", "head", "left_arm", "right_arm", "left_leg", "right_leg");
        AddLayer(parts, "spots", "entity/warden/warden_pulsating_spots_1", "body", "head", "left_arm", "right_arm", "left_leg", "right_leg");
        AddLayer(parts, "heart", "entity/warden/warden_heart", "body");
        AddLayer(parts, "tendril_glow", "entity/warden/warden", "left_tendril", "right_tendril");
        return [.. parts];
    }

    private static CompiledEntityModel BuildCompiled()
    {
        var compiled = BaseCompiled;
        compiled = EntityModelLayerHelpers.WithEmissiveLayer(
            compiled,
            "bioluminescent_",
            "entity/warden/warden_bioluminescent_layer",
            new HashSet<string>(StringComparer.Ordinal) { "head", "left_arm", "right_arm", "left_leg", "right_leg" });
        compiled = EntityModelLayerHelpers.WithEmissiveLayer(
            compiled,
            "spots_",
            "entity/warden/warden_pulsating_spots_1",
            new HashSet<string>(StringComparer.Ordinal) { "body", "head", "left_arm", "right_arm", "left_leg", "right_leg" });
        compiled = EntityModelLayerHelpers.WithEmissiveLayer(
            compiled,
            "heart_",
            "entity/warden/warden_heart",
            new HashSet<string>(StringComparer.Ordinal) { "body" });
        return EntityModelLayerHelpers.WithEmissiveLayer(
            compiled,
            "tendril_glow_",
            "entity/warden/warden",
            new HashSet<string>(StringComparer.Ordinal) { "left_tendril", "right_tendril" });
    }

    private static void AddLayer(List<EntityModelPart> parts, string prefix, string texturePath, params string[] sourceNames)
    {
        foreach (var sourceName in sourceNames)
        {
            var source = BaseParts.Single(part => part.Name == sourceName);
            parts.Add(source with
            {
                Name = prefix + "_" + sourceName,
                PoseSource = sourceName,
                EmissiveOnly = true,
                IncludeInEmissive = true,
                EmissiveTexturePath = texturePath,
                Kind = EntityGeometryLayerKind.Cutout
            });
        }
    }

    private static VanillaMeshDefinition BuildWardenMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var bone = root.AddOrReplaceChild("bone", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("bone", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var body = bone.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -21f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -9f, -13f, -4f, 18f, 21f, 11f));
        body.AddOrReplaceChild("right_ribcage", VanillaPartPose.Offset(-7f, -2f, -4f),
            new VanillaCubeDefinition("right_ribcage", 90, 11, -2f, -11f, -0.1f, 9f, 21f, 0f));
        body.AddOrReplaceChild("left_ribcage", VanillaPartPose.Offset(7f, -2f, -4f),
            new VanillaCubeDefinition("left_ribcage", 90, 11, -7f, -11f, -0.1f, 9f, 21f, 0f, Mirror: true));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -13f, 0f),
            new VanillaCubeDefinition("head", 0, 32, -8f, -16f, -5f, 16f, 16f, 10f));
        head.AddOrReplaceChild("right_tendril", VanillaPartPose.Offset(-8f, -12f, 0f),
            new VanillaCubeDefinition("right_tendril", 52, 32, -16f, -13f, 0f, 16f, 16f, 0f));
        head.AddOrReplaceChild("left_tendril", VanillaPartPose.Offset(8f, -12f, 0f),
            new VanillaCubeDefinition("left_tendril", 58, 0, 0f, -13f, 0f, 16f, 16f, 0f));
        body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-13f, -13f, 1f),
            new VanillaCubeDefinition("right_arm", 44, 50, -4f, 0f, -4f, 8f, 28f, 8f));
        body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(13f, -13f, 1f),
            new VanillaCubeDefinition("left_arm", 0, 58, -4f, 0f, -4f, 8f, 28f, 8f));
        bone.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-5.9f, -13f, 0f),
            new VanillaCubeDefinition("right_leg", 76, 48, -3.1f, 0f, -3f, 6f, 13f, 6f));
        bone.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(5.9f, -13f, 0f),
            new VanillaCubeDefinition("left_leg", 76, 76, -2.9f, 0f, -3f, 6f, 13f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
