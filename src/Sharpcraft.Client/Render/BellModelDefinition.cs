// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Static source-frame geometry for the bell: a bell_body box hanging from a pivot at (8,12,8),
/// with a bell_base child carried by the body. The shake animation is a runtime rotation of
/// bell_body (<c>BellModel.setupAnim</c>) applied by the renderer, not part of this static bind pose.
/// Source: ref://mc-26.3-analysis/src/net/minecraft/client/model/object/bell/BellModel.java:24-59.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.bell.BellModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/bell/BellModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class BellModelDefinition
{
    private static VanillaMeshDefinition BuildMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("bell_body", VanillaPartPose.Offset(8f, 12f, 8f),
            new VanillaCubeDefinition("bell_body", 0, 0, -3f, -6f, -3f, 6f, 7f, 6f));
        body.AddOrReplaceChild("bell_base", VanillaPartPose.Offset(-8f, -12f, -8f),
            new VanillaCubeDefinition("bell_base", 0, 13, 4f, 4f, 4f, 8f, 2f, 8f));
        return new VanillaMeshDefinition(root);
    }

    /// <summary>Static bell geometry in the raw source frame (y-down pixels, part pivots as in the jar).</summary>
    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "bell", 32, 32, BuildMesh(), faceTransforms: null, "entity/bell/bell_body", 1f,
        baselinePx: 0f, rawSourceFrame: true);
}
