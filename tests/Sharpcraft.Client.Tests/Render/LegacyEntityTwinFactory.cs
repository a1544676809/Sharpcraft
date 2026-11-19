// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Independent reconstruction of the EXACT pre-migration legacy baked geometry for the three migrated
/// families (main 2fb0f383). This does NOT call any production bake (VanillaMeshBake/BakeRoot/BakeChild)
/// nor LegacyModelPartFactory: the legacy y-up values are written out here as direct formulas, so a bug
/// in the production legacy conversion cannot mask a world-coordinate regression. The expected final
/// world positions the migration must preserve are produced by meshing these parts through the plain
/// entity transform in LivingRawReferenceSampleTests.
///
/// Legacy root formula: centreX = -(pivotX+minX+w/2), bottom = modelHeight-(pivotY+minY+h),
/// centreZ = pivotZ+minZ+d/2, pivot = (-pivotX, modelHeight-pivotY, pivotZ), rotations literal.
/// Legacy child formula: centreX = -(minX+w/2), bottom = -(minY+h), pivot = (-pivotX,-pivotY,pivotZ),
/// rotations X/Y negated, Z literal.
///
/// Source literals below are the pre-migration DSL values (conjugated rotations), recovered from
/// main 2fb0f383's model definitions / reference model facts.
/// </summary>
internal static class LegacyEntityTwinFactory
{
    private const float H = 24f;

    private static EntityModelPart Root(string name, float px, float py, float pz, float minX, float minY, float minZ,
        float w, float h, float d, int tx, int ty, bool mirror = false, bool mirrorX = true,
        float rx = 0f, float ry = 0f, float rz = 0f) => new(
        name,
        MathF.Round(mirrorX ? -(px + minX + (w * 0.5f)) : (px + minX + (w * 0.5f)), 4),
        MathF.Round(H - (py + minY + h), 4),
        w, h, d, tx, ty, mirror,
        CentreZ: MathF.Round(pz + minZ + (d * 0.5f), 4),
        PivotX: mirrorX ? -px : px,
        PivotY: H - py,
        PivotZ: pz,
        RotationX: rx, RotationY: ry, RotationZ: rz);

    private static EntityModelPart Child(string name, string parent, float px, float py, float pz, float minX, float minY, float minZ,
        float w, float h, float d, int tx, int ty, bool mirror = false,
        float rx = 0f, float ry = 0f, float rz = 0f) => new(
        name,
        MathF.Round(-(minX + (w * 0.5f)), 4),
        MathF.Round(-(minY + h), 4),
        w, h, d, tx, ty, mirror,
        CentreZ: MathF.Round(minZ + (d * 0.5f), 4),
        PivotX: MathF.Round(-px, 4),
        PivotY: MathF.Round(-py, 4),
        PivotZ: pz,
        RotationX: -rx, RotationY: -ry, RotationZ: rz,
        Parent: parent);

    private static EntityModelDefinition Build(string name, int w, int h, EntityModelPart[] parts,
        string texture, float scale = 1f, float baseline = 24f) =>
        CompiledPartHelper.Legacy(parts, new EntityModelDefinition(name, w, h,
            PlayerModelDefinition.DefaultFaceTransforms, texture, scale: scale, baselinePx: baseline, rawSourceFrame: false));

    public static EntityModelDefinition Creeper() => Build("creeper_legacy", 64, 32,
        [
            Root("head", 0f, 6f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
            Root("body", 0f, 6f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            Root("left_hind_leg", 2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            Root("right_hind_leg", -2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            Root("left_front_leg", 2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            Root("right_front_leg", -2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16)
        ],
        "entity/creeper/creeper", baseline: H);

    public static EntityModelDefinition Spider(float scale = 1f, string texture = "entity/spider/spider") => Build("spider_legacy", 64, 32, SpiderParts(), texture, scale, H);

    public static EntityModelDefinition Wither() => Build("wither_legacy", 64, 64,
        [
            Root("shoulders", 0f, 0f, 0f, -10f, 3.9f, -0.5f, 20f, 3f, 3f, 0, 16, mirrorX: false),
            // Pre-migration conjugated rotations: -0.20420352 / -0.83252203 (see main 2fb0f383).
            Root("ribcage", -2f, 6.9f, -0.5f, 0f, 0f, 0f, 3f, 10f, 3f, 0, 22, rx: -0.20420352f),
            Child("rib_1", "ribcage", 0f, 0f, 0f, -4f, 1.5f, 0.5f, 11f, 2f, 2f, 24, 22),
            Child("rib_2", "ribcage", 0f, 0f, 0f, -4f, 4f, 0.5f, 11f, 2f, 2f, 24, 22),
            Child("rib_3", "ribcage", 0f, 0f, 0f, -4f, 6.5f, 0.5f, 11f, 2f, 2f, 24, 22),
            Root("tail", -2f, 16.692407608f, 1.527009249f, 0f, 0f, 0f, 3f, 6f, 3f, 12, 22, rx: -0.83252203f),
            Root("center_head", 0f, 0f, 0f, -4f, -4f, -4f, 8f, 8f, 8f, 0, 0),
            Root("right_head", -8f, 4f, 0f, -4f, -4f, -4f, 6f, 6f, 6f, 32, 0),
            Root("left_head", 10f, 4f, 0f, -4f, -4f, -4f, 6f, 6f, 6f, 32, 0)
        ],
        "entity/wither/wither", baseline: H);

    private static EntityModelPart[] SpiderParts()
    {
        // Pre-migration spider legs: mirrorX:false roots, conjugated Z rotation (-rz) in the legacy DSL.
        EntityModelPart Leg(string name, float px, float pz, bool mirror, float ry, float rz) =>
            Root(name, px, 15f, pz, mirror ? -1f : -15f, -1f, -1f, 16f, 2f, 2f, 18, 0,
                mirror: mirror, mirrorX: false, ry: ry, rz: -rz);
        return
        [
            Root("head", 0f, 15f, -3f, -4f, -4f, -8f, 8f, 8f, 8f, 32, 4, mirrorX: false),
            Root("body0", 0f, 15f, 0f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 0, mirrorX: false),
            Root("body1", 0f, 15f, 9f, -5f, -4f, -6f, 10f, 8f, 12f, 0, 12, mirrorX: false),
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
