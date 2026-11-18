// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Raw source-frame geometry for the decorated pot: the base layer (neck ring/rim + top/bottom planes)
/// and the four side planes, each side cube emitting only its source NORTH face via VisibleSides.
/// Pattern/sprite selection, facing and wobble stay in the renderer state shell.
/// Source: ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/DecoratedPotRenderer.java:89-118.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.blockentity.DecoratedPotRenderer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/blockentity/DecoratedPotRenderer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Raw static geometry for base layer and four NORTH-only side planes (VisibleSides)")]
public static class DecoratedPotModelDefinition
{
    private static VanillaMeshDefinition BuildBase()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var neck = root.AddOrReplaceChild("neck",
            VanillaPartPose.OffsetAndRotation(0f, 37f, 16f, MathF.PI, 0f, 0f));
        // Multi-cube part must be named after its first cube ("neck"); source sample /neck c0/c1.
        neck.AddCube(new VanillaCubeDefinition("neck", 0, 0, 4f, 17f, 4f, 8f, 3f, 8f, Grow: -0.1f));
        neck.AddCube(new VanillaCubeDefinition("neck_rim", 0, 5, 5f, 20f, 5f, 6f, 1f, 6f, Grow: 0.2f));
        root.AddOrReplaceChild("top", VanillaPartPose.Offset(1f, 16f, 1f),
            new VanillaCubeDefinition("top", -14, 13, 0f, 0f, 0f, 14f, 0f, 14f));
        root.AddOrReplaceChild("bottom", VanillaPartPose.Offset(1f, 0f, 1f),
            new VanillaCubeDefinition("bottom", -14, 13, 0f, 0f, 0f, 14f, 0f, 14f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildSides()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        // Reference sidePlane: texOffs(1,0), addBox(0,0,0, 14,16,0, EnumSet.of(NORTH)).
        // Only the NORTH face exists; VisibleSides=North maps to the source face mask.
        root.AddOrReplaceChild("back", VanillaPartPose.OffsetAndRotation(15f, 16f, 1f, 0f, 0f, MathF.PI),
            new VanillaCubeDefinition("back", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f, VisibleSides: EntityModelVisibleSides.North));
        root.AddOrReplaceChild("left", VanillaPartPose.OffsetAndRotation(1f, 16f, 1f, 0f, -MathF.PI / 2f, MathF.PI),
            new VanillaCubeDefinition("left", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f, VisibleSides: EntityModelVisibleSides.North));
        root.AddOrReplaceChild("right", VanillaPartPose.OffsetAndRotation(15f, 16f, 15f, 0f, MathF.PI / 2f, MathF.PI),
            new VanillaCubeDefinition("right", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f, VisibleSides: EntityModelVisibleSides.North));
        root.AddOrReplaceChild("front", VanillaPartPose.OffsetAndRotation(1f, 16f, 15f, MathF.PI, 0f, 0f),
            new VanillaCubeDefinition("front", 1, 0, 0f, 0f, 0f, 14f, 16f, 0f, VisibleSides: EntityModelVisibleSides.North));
        return new VanillaMeshDefinition(root);
    }

    public static readonly EntityModelDefinition Base = EntityModelDefinition.FromTree(
        "decorated_pot_base_raw", 32, 32, BuildBase(), faceTransforms: null,
        "entity/decorated_pot/decorated_pot_base", 1f, baselinePx: 0f, rawSourceFrame: true);

    public static readonly EntityModelDefinition Sides = EntityModelDefinition.FromTree(
        "decorated_pot_sides_raw", 16, 16, BuildSides(), faceTransforms: null,
        "entity/decorated_pot/decorated_pot_side", 1f, baselinePx: 0f, rawSourceFrame: true);
}
