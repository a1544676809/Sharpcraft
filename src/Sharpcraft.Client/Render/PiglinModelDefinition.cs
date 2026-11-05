// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Piglin family geometry: the player humanoid mesh with the head replaced by the piglin head (snout, nostrils, ears).
/// </summary>
/// <remarks>
/// AdultPiglinModel starts from <c>PlayerModel.createMesh(false)</c> and only replaces the body and head (clearing the
/// hat), so arms, legs and their outer layers are the player ones. Zombified Piglin and Piglin Brute share this geometry
/// and differ only by texture. Animations and the piglin arm poses are not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.piglin.PiglinModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PiglinModelDefinition
{
    public const int PiglinEntityTypeId = 102;
    public const int PiglinBruteEntityTypeId = 103;
    public const int ZombifiedPiglinEntityTypeId = 158;

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

        parts.Add(PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -5f, -8f, -4f, 10f, 8f, 8f, 0, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("head_nose", "head", 0f, 0f, 0f, -2f, -4f, -5f, 4f, 4f, 1f, 31, 1));
        parts.Add(PlayerModelDefinition.FromVanillaChild("head_nostril_right", "head", 0f, 0f, 0f, 2f, -2f, -5f, 1f, 2f, 1f, 2, 4));
        parts.Add(PlayerModelDefinition.FromVanillaChild("head_nostril_left", "head", 0f, 0f, 0f, -3f, -2f, -5f, 1f, 2f, 1f, 2, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("left_ear", "head", 4.5f, -6f, 0f, 0f, 0f, -2f, 1f, 5f, 4f, 51, 6,
            rotationZ: -MathF.PI / 6f));
        parts.Add(PlayerModelDefinition.FromVanillaChild("right_ear", "head", -4.5f, -6f, 0f, -1f, 0f, -2f, 1f, 5f, 4f, 39, 6,
            rotationZ: MathF.PI / 6f));
        return [.. parts];
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
