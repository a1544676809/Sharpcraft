// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SnifferModel: an empty bone root, body with a 0.5-grown outer layer and a flat belly, six legs, and a head with two
/// thin ears, nose and lower beak. Bind pose only; the sniff/dig/walk animations are not ported. Parts are declared with
/// the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-686); the legacy flat order places
/// the legs before the body→head subtree, so the baked parts are reordered by name.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.sniffer.SnifferModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/sniffer/SnifferModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SnifferModelDefinition
{
    public const int SnifferEntityTypeId = 122;

    private static readonly VanillaMeshDefinition SnifferMesh = BuildSnifferMesh();

    public static readonly EntityModelDefinition Definition = new(
        "sniffer",
        192,
        192,
        BuildParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/sniffer/sniffer");

    private static EntityModelPart[] BuildParts()
    {
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in VanillaMeshBake.Bake(SnifferMesh, 0, 0))
        {
            byName[part.Name] = part;
        }

        return
        [
            byName["bone"], byName["body"], byName["body_outer"], byName["body_bottom"],
            byName["right_front_leg"], byName["right_mid_leg"], byName["right_hind_leg"],
            byName["left_front_leg"], byName["left_mid_leg"], byName["left_hind_leg"],
            byName["head"], byName["head_flat"], byName["left_ear"], byName["right_ear"],
            byName["nose"], byName["lower_beak"]
        ];
    }

    private static VanillaMeshDefinition BuildSnifferMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var bone = root.AddOrReplaceChild("bone", VanillaPartPose.Offset(0f, 5f, 0f),
            new VanillaCubeDefinition("bone", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var body = bone.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 62, 68, -12.5f, -14f, -20f, 25f, 29f, 40f));
        body.AddOrReplaceChild("body_outer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_outer", 62, 0, -12.5f, -14f, -20f, 25f, 24f, 40f, Grow: 0.5f));
        body.AddOrReplaceChild("body_bottom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_bottom", 87, 68, -12.5f, 12f, -20f, 25f, 0f, 40f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 6.5f, -19.48f),
            new VanillaCubeDefinition("head", 8, 15, -6.5f, -7.5f, -11.5f, 13f, 18f, 11f));
        head.AddOrReplaceChild("head_flat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_flat", 8, 4, -6.5f, 7.5f, -11.5f, 13f, 0f, 11f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(6.51f, -7.5f, -4.51f),
            new VanillaCubeDefinition("left_ear", 2, 0, 0f, 0f, -3f, 1f, 19f, 7f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-6.51f, -7.5f, -4.51f),
            new VanillaCubeDefinition("right_ear", 48, 0, -1f, 0f, -3f, 1f, 19f, 7f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -4.5f, -11.5f),
            new VanillaCubeDefinition("nose", 10, 45, -6.5f, -2f, -9f, 13f, 2f, 9f));
        head.AddOrReplaceChild("lower_beak", VanillaPartPose.Offset(0f, 2.5f, -12.5f),
            new VanillaCubeDefinition("lower_beak", 10, 57, -6.5f, -7f, -8f, 13f, 12f, 9f));
        bone.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-7.5f, 10f, -15f),
            new VanillaCubeDefinition("right_front_leg", 32, 87, -3.5f, -1f, -4f, 7f, 10f, 8f));
        bone.AddOrReplaceChild("right_mid_leg", VanillaPartPose.Offset(-7.5f, 10f, 0f),
            new VanillaCubeDefinition("right_mid_leg", 32, 105, -3.5f, -1f, -4f, 7f, 10f, 8f));
        bone.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-7.5f, 10f, 15f),
            new VanillaCubeDefinition("right_hind_leg", 32, 123, -3.5f, -1f, -4f, 7f, 10f, 8f));
        bone.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(7.5f, 10f, -15f),
            new VanillaCubeDefinition("left_front_leg", 0, 87, -3.5f, -1f, -4f, 7f, 10f, 8f));
        bone.AddOrReplaceChild("left_mid_leg", VanillaPartPose.Offset(7.5f, 10f, 0f),
            new VanillaCubeDefinition("left_mid_leg", 0, 105, -3.5f, -1f, -4f, 7f, 10f, 8f));
        bone.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(7.5f, 10f, 15f),
            new VanillaCubeDefinition("left_hind_leg", 0, 123, -3.5f, -1f, -4f, 7f, 10f, 8f));
        return new VanillaMeshDefinition(root);
    }
}
