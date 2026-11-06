// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Piglin family geometry: the player humanoid mesh with the head replaced by the piglin head (snout, nostrils, ears).
/// </summary>
/// <remarks>
/// AdultPiglinModel starts from <c>PlayerModel.createMesh(false)</c> and only replaces the body and head (clearing the
/// hat), so arms, legs and their outer layers are the player ones. Zombified Piglin and Piglin Brute share this geometry
/// and differ only by texture. Animations and the piglin arm poses are not ported. The piglin head is declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-682), appended to the adult parts
/// (minus head) in the same order.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.piglin.PiglinModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PiglinModelDefinition
{
    public const int PiglinEntityTypeId = 102;
    public const int PiglinBruteEntityTypeId = 103;
    public const int ZombifiedPiglinEntityTypeId = 158;

    /// <summary>Declarative DSL tree for the six-part piglin head (T-682).</summary>
    private static readonly VanillaMeshDefinition PiglinHeadMesh = BuildPiglinHeadMesh();

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>();
        foreach (var part in PlayerModelDefinition.AdultParts)
        {
            if (part.Name != "head")
            {
                parts.Add(part);
            }
        }

        parts.AddRange(VanillaMeshBake.Bake(PiglinHeadMesh, 0, 0));
        return [.. parts];
    }

    private static VanillaMeshDefinition BuildPiglinHeadMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -5f, -8f, -4f, 10f, 8f, 8f));
        head.AddOrReplaceChild("head_nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_nose", 31, 1, -2f, -4f, -5f, 4f, 4f, 1f));
        head.AddOrReplaceChild("head_nostril_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_nostril_right", 2, 4, 2f, -2f, -5f, 1f, 2f, 1f));
        head.AddOrReplaceChild("head_nostril_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_nostril_left", 2, 0, -3f, -2f, -5f, 1f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(4.5f, -6f, 0f, 0f, 0f, -MathF.PI / 6f),
            new VanillaCubeDefinition("left_ear", 51, 6, 0f, 0f, -2f, 1f, 5f, 4f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-4.5f, -6f, 0f, 0f, 0f, MathF.PI / 6f),
            new VanillaCubeDefinition("right_ear", 39, 6, -1f, 0f, -2f, 1f, 5f, 4f));
        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition Definition = new(
        "piglin",
        64,
        64,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/piglin/piglin");

    public static readonly EntityModelDefinition Brute = new(
        "piglin_brute",
        64,
        64,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/piglin/piglin_brute");

    public static readonly EntityModelDefinition Zombified = new(
        "zombified_piglin",
        64,
        64,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/piglin/zombified_piglin");
}
