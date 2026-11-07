// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultFoxModel: head with ears/nose, rotated body, four legs and a tail. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-684); the tail stays a child of body
/// but is reordered last to keep the legacy flat order, and the sleeping selector and variants are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fox.FoxModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fox/FoxModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class FoxModelDefinition
{
    public const int FoxEntityTypeId = 55;

    /// <summary>Declarative DSL tree for the ten fox parts (T-684).</summary>
    private static readonly VanillaMeshDefinition FoxMesh = BuildFoxMesh();

    internal static VanillaMeshDefinition Mesh => FoxMesh;

    private static readonly CompiledEntityModel CompiledModel =
        VanillaMeshBakery.Build(FoxMesh, 48, 32, texturePath: "entity/fox/fox");

    private static EntityModelPart[] BuildParts()
    {
        // The reference lists the tail (a child of body) last, after all four legs; Bake emits children depth-first.
        // Reorder the baked parts by name (values untouched) to keep the flat array order identical.
        var baked = VanillaMeshBake.Bake(FoxMesh, 0, 0);
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in baked)
        {
            byName[part.Name] = part;
        }

        return
        [
            byName["head"], byName["right_ear"], byName["left_ear"], byName["nose"], byName["body"],
            byName["right_hind_leg"], byName["left_hind_leg"], byName["right_front_leg"], byName["left_front_leg"],
            byName["tail"]
        ];
    }

    public static readonly EntityModelDefinition Definition = new(
        "fox",
        48,
        32,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fox/fox",
        textureSelector: state => state.IsSleeping ? "entity/fox/fox_sleep" : "entity/fox/fox",
        additionalTexturePaths: ["entity/fox/fox_sleep"], compiled: CompiledModel);

    /// <summary>Fox type names: RED and SNOW, matching the synced integer type id order.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["red", "snow"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["red"] = Definition,
            ["snow"] = new(
                "fox_snow",
                48,
                32,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/fox/fox_snow",
                textureSelector: state => state.IsSleeping ? "entity/fox/fox_snow_sleep" : "entity/fox/fox_snow",
                additionalTexturePaths: ["entity/fox/fox_snow_sleep"], compiled: CompiledModel)
        };

    private static VanillaMeshDefinition BuildFoxMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(-1f, 16.5f, -3f),
            new VanillaCubeDefinition("head", 1, 5, -3f, -2f, -5f, 8f, 6f, 6f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 8, 1, -3f, -4f, -4f, 2f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 15, 1, 3f, -4f, -4f, 2f, 2f, 1f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nose", 6, 18, -1f, 2.01f, -8f, 4f, 2f, 3f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 16f, -6f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 24, 15, -3f, 3.999f, -3.5f, 6f, 11f, 6f));
        body.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(-4f, 15f, -1f, -0.05235988f, 0f, 0f),
            new VanillaCubeDefinition("tail", 30, 0, 2f, 0f, -1f, 4f, 9f, 5f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-5f, 17.5f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 13, 24, 2f, 0.5f, -1f, 2f, 6f, 2f, Grow: 0.001f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(-1f, 17.5f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 4, 24, 2f, 0.5f, -1f, 2f, 6f, 2f, Grow: 0.001f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-5f, 17.5f, 0f),
            new VanillaCubeDefinition("right_front_leg", 13, 24, 2f, 0.5f, -1f, 2f, 6f, 2f, Grow: 0.001f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(-1f, 17.5f, 0f),
            new VanillaCubeDefinition("left_front_leg", 4, 24, 2f, 0.5f, -1f, 2f, 6f, 2f, Grow: 0.001f));
        return new VanillaMeshDefinition(root);
    }
}
