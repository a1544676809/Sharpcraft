// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PigModel / QuadrupedModel with legSize 6: rotated body, head with snout, and four legs.
/// </summary>
/// <remarks>
/// Variants come from the <c>pig_variant</c> registry (docs/analysis/render/entity-variants.md 3). "warm" keeps the
/// normal geometry and only changes the texture; "cold" uses <c>ColdPigModel</c>, which adds a 0.5-pixel outer layer to
/// the body (UV 28,32). Baby models are out of scope for now. Parts are declared with the <c>VanillaMeshDefinition</c>
/// DSL and baked via <c>VanillaMeshBake.Bake</c> (T-676).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.pig.PigModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/pig/PigModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PigModelDefinition
{
    public const int PigEntityTypeId = 101;

    /// <summary>Declarative DSL trees for the temperate/warm and cold pig parts (T-676).</summary>
    private static readonly VanillaMeshDefinition NormalMesh = BuildPigMesh(null);

    private static readonly EntityModelPart[] NormalParts = [.. VanillaMeshBake.Bake(NormalMesh, 0, 0)];

    private static readonly VanillaMeshDefinition ColdMesh = BuildPigMesh([new("cold_pig_layer", 0.5f, 28, 32)]);

    private static readonly EntityModelPart[] ColdParts = [.. VanillaMeshBake.Bake(ColdMesh, 0, 0)];

    public static readonly EntityModelDefinition Definition = new(
        "pig",
        64,
        64,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/pig/pig_temperate");

    /// <summary>Variant key to definition, keyed by the registry entry name ("temperate", "warm", "cold").</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new("pig_warm", 64, 64, NormalParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_warm"),
            ["cold"] = new("pig_cold", 64, 64, ColdParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_cold")
        };

    /// <summary>
    /// Builds the seven root parts. The cold variant only differs by the 0.5-pixel outer body overlay (UV 28,32).
    /// </summary>
    private static VanillaMeshDefinition BuildPigMesh(IReadOnlyList<EntityModelLayer>? bodyOverlays)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 12f, -6f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -4f, -8f, 8f, 8f, 8f));
        root.AddOrReplaceChild("snout", VanillaPartPose.Offset(0f, 12f, -6f),
            new VanillaCubeDefinition("snout", 16, 16, -2f, 0f, -9f, 4f, 3f, 1f));
        root.AddOrReplaceChild("body", VanillaPartPose.OffsetAndRotation(0f, 11f, 2f, -MathF.PI / 2f, 0f, 0f),
            new VanillaCubeDefinition("body", 28, 8, -5f, -10f, -7f, 10f, 16f, 8f, Overlays: bodyOverlays));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3f, 18f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3f, 18f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3f, 18f, -5f),
            new VanillaCubeDefinition("right_front_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3f, 18f, -5f),
            new VanillaCubeDefinition("left_front_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f, Mirror: true));
        return new VanillaMeshDefinition(root);
    }
}
