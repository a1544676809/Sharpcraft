// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// WitchModel: villager body/jacket, arms and legs, with a witch head carrying a four-tier hat and a mole on the nose.
/// </summary>
/// <remarks>
/// Bind pose only. The reference reuses VillagerModel.createBodyModel and replaces only the head/nose; the hat chain is
/// nested under the head. Held-item and nose-wiggle animations are not ported.
/// </remarks>
public static class WitchModelDefinition
{
    public const int WitchEntityTypeId = 148;

    private static readonly float Pi = MathF.PI;

    private static EntityModelPart[] BuildParts()
    {
        var parts = new List<EntityModelPart>();
        foreach (var part in VillagerModelDefinition.Definition.Parts)
        {
            if (part.Name is "head" or "nose")
            {
                continue;
            }

            parts.Add(part);
        }

        parts.Add(PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0, mirrorX: false));
        parts.Add(PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0));
        parts.Add(PlayerModelDefinition.FromVanillaChild("mole", "nose", 0f, -2f, 0f, 0f, 3f, -6.75f, 1f, 1f, 1f, 0, 0, grow: -0.25f));
        parts.Add(PlayerModelDefinition.FromVanillaChild("hat", "head", -5f, -10.03125f, -5f, 0f, 0f, 0f, 10f, 2f, 10f, 0, 64));
        parts.Add(PlayerModelDefinition.FromVanillaChild("hat2", "hat", 1.75f, -4f, 2f, 0f, 0f, 0f, 7f, 4f, 7f, 0, 76,
            rotationX: -0.05235988f, rotationZ: 0.02617994f));
        parts.Add(PlayerModelDefinition.FromVanillaChild("hat3", "hat2", 1.75f, -4f, 2f, 0f, 0f, 0f, 4f, 4f, 4f, 0, 87,
            rotationX: -0.10471976f, rotationZ: 0.05235988f));
        parts.Add(PlayerModelDefinition.FromVanillaChild("hat4", "hat3", 1.75f, -2f, 2f, 0f, 0f, 0f, 1f, 2f, 1f, 0, 95,
            grow: 0.25f, rotationX: -Pi / 15f, rotationZ: 0.10471976f));
        return [.. parts];
    }

    public static readonly EntityModelDefinition Definition = new(
        "witch",
        64,
        128,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/witch/witch",
        0.9375f);
}
