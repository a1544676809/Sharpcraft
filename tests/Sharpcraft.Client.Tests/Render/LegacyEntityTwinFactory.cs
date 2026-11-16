// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The EXACT pre-migration legacy baked geometry for the three migrated families, using the original
/// (conjugated) rotation literals from main 2fb0f383. These are the world coordinates the migration
/// must preserve point-by-point. This factory re-states those legacy expectations via the independent
/// LegacyModelPartFactory (y-up conversion formula); raw world geometry is compared against it in
/// LivingRawReferenceSampleTests. Nothing here reads the modified production DSL or raw parts.
/// </summary>
internal static class LegacyEntityTwinFactory
{
    public static EntityModelDefinition Creeper() => new(
        "creeper_legacy", 64, 32,
        [
            LegacyModelPartFactory.FromVanilla("head", 0f, 6f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
            LegacyModelPartFactory.FromVanilla("body", 0f, 6f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            LegacyModelPartFactory.FromVanilla("left_hind_leg", 2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            LegacyModelPartFactory.FromVanilla("right_hind_leg", -2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            LegacyModelPartFactory.FromVanilla("left_front_leg", 2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            LegacyModelPartFactory.FromVanilla("right_front_leg", -2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/creeper/creeper", baselinePx: 24f, rawSourceFrame: false);

    public static EntityModelDefinition Spider(float scale = 1f, string texture = "entity/spider/spider") => new(
        "spider_legacy", 64, 32, SpiderParts(),
        PlayerModelDefinition.DefaultFaceTransforms, texture, scale: scale, baselinePx: 24f, rawSourceFrame: false);

    public static EntityModelDefinition Wither() => new(
        "wither_legacy", 64, 64,
        [
            LegacyModelPartFactory.FromVanilla("shoulders", 0f, 0f, 0f, -10f, 3.9f, -0.5f, 20f, 3f, 3f, 0, 16, mirrorX: false),
            LegacyModelPartFactory.FromVanilla("ribcage", -2f, 6.9f, -0.5f, 0f, 0f, 0f, 3f, 10f, 3f, 0, 22, rotationX: -0.20420352f),
            LegacyModelPartFactory.FromVanillaChild("rib_1", "ribcage", 0f, 0f, 0f, -4f, 1.5f, 0.5f, 11f, 2f, 2f, 24, 22),
            LegacyModelPartFactory.FromVanillaChild("rib_2", "ribcage", 0f, 0f, 0f, -4f, 4f, 0.5f, 11f, 2f, 2f, 24, 22),
            LegacyModelPartFactory.FromVanillaChild("rib_3", "ribcage", 0f, 0f, 0f, -4f, 6.5f, 0.5f, 11f, 2f, 2f, 24, 22),
            LegacyModelPartFactory.FromVanilla("tail", -2f, 16.692228f, 1.527873f, 0f, 0f, 0f, 3f, 6f, 3f, 12, 22, rotationX: -0.83252203f),
            LegacyModelPartFactory.FromVanilla("center_head", 0f, 0f, 0f, -4f, -4f, -4f, 8f, 8f, 8f, 0, 0),
            LegacyModelPartFactory.FromVanilla("right_head", -8f, 4f, 0f, -4f, -4f, -4f, 6f, 6f, 6f, 32, 0),
            LegacyModelPartFactory.FromVanilla("left_head", 10f, 4f, 0f, -4f, -4f, -4f, 6f, 6f, 6f, 32, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/wither/wither", baselinePx: 24f, rawSourceFrame: false);

    private static EntityModelPart[] SpiderParts()
    {
        EntityModelPart Leg(string name, float px, float pz, bool mirror, float ry, float rz) =>
            LegacyModelPartFactory.FromVanilla(name, px, 15f, pz, mirror ? -1f : -15f, -1f, -1f, 16f, 2f, 2f, 18, 0,
                mirror: mirror, mirrorX: false, rotationY: ry, rotationZ: -rz);
        return
        [
            LegacyModelPartFactory.FromVanilla("head", 0f, 15f, -3f, -4f, -4f, -8f, 8f, 8f, 8f, 32, 4, mirrorX: false),
            LegacyModelPartFactory.FromVanilla("body0", 0f, 15f, 0f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 0, mirrorX: false),
            LegacyModelPartFactory.FromVanilla("body1", 0f, 15f, 9f, -5f, -4f, -6f, 10f, 8f, 12f, 0, 12, mirrorX: false),
            Leg("right_hind_leg", -4f, 2f, false, MathF.PI / 4f, -MathF.PI / 4f),
            Leg("left_hind_leg", 4f, 2f, true, -MathF.PI / 4f, MathF.PI / 4f),
            Leg("right_middle_hind_leg", -4f, 1f, false, MathF.PI / 8f, -0.58119464f),
            Leg("left_middle_hind_leg", 4f, 1f, true, -MathF.PI / 8f, 0.58119464f),
            Leg("right_middle_front_leg", -4f, 0f, false, -MathF.PI / 8f, -0.58119464f),
            Leg("left_middle_front_leg", 4f, 0f, true, MathF.PI / 8f, 0.58119464f),
            Leg("right_front_leg", -4f, -1f, false, -MathF.PI / 4f, -MathF.PI / 4f),
            Leg("left_front_leg", 4f, -1f, true, MathF.PI / 4f, MathF.PI / 4f)
        ];
    }
}
