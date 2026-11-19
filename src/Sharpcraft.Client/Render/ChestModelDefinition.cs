// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Static chest bind-pose models from <c>ChestModel</c> (single and the two double-chest halves).
/// The double halves use source <c>visibleFaces</c>: ALL except EAST for the right half and ALL except WEST
/// for the left half. Openness is a runtime lid/lock x-rotation and is applied by the renderer, not baked here.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.chest.ChestModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/chest/ChestModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ChestModelDefinition
{
    public const int TextureWidth = 64;
    public const int TextureHeight = 64;

    private const EntityModelVisibleSides RightHalfSides = EntityModelVisibleSides.All & ~EntityModelVisibleSides.East;
    private const EntityModelVisibleSides LeftHalfSides = EntityModelVisibleSides.All & ~EntityModelVisibleSides.West;

    private static readonly VanillaMeshDefinition SingleMesh = BuildSingleMesh();
    private static readonly VanillaMeshDefinition DoubleRightMesh = BuildDoubleRightMesh();
    private static readonly VanillaMeshDefinition DoubleLeftMesh = BuildDoubleLeftMesh();

    // Both the flat parts and the compiled tree are baked from the same source mesh so their geometry is
    // byte-identical; the production mesher now consumes only the compiled tree.
    public static readonly EntityModelDefinition Single = new("chest_single", TextureWidth, TextureHeight, faceTransforms: null, texturePath: null, compiled: VanillaMeshBakery.Build(SingleMesh, TextureWidth, TextureHeight, modelHeight: 0f, rawSourceFrame: true), baselinePx: 0f, rawSourceFrame: true);

    public static readonly EntityModelDefinition DoubleRight = new("chest_double_right", TextureWidth, TextureHeight, faceTransforms: null, texturePath: null, compiled: VanillaMeshBakery.Build(DoubleRightMesh, TextureWidth, TextureHeight, modelHeight: 0f, rawSourceFrame: true), baselinePx: 0f, rawSourceFrame: true);

    public static readonly EntityModelDefinition DoubleLeft = new("chest_double_left", TextureWidth, TextureHeight, faceTransforms: null, texturePath: null, compiled: VanillaMeshBakery.Build(DoubleLeftMesh, TextureWidth, TextureHeight, modelHeight: 0f, rawSourceFrame: true), baselinePx: 0f, rawSourceFrame: true);

    private static VanillaMeshDefinition BuildSingleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("bottom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bottom", 0, 19, 1f, 0f, 1f, 14f, 10f, 14f));
        root.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lid", 0, 0, 1f, 0f, 0f, 14f, 5f, 14f));
        root.AddOrReplaceChild("lock", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lock", 0, 0, 7f, -2f, 14f, 2f, 4f, 1f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildDoubleRightMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("bottom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bottom", 0, 19, 1f, 0f, 1f, 15f, 10f, 14f, VisibleSides: RightHalfSides));
        root.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lid", 0, 0, 1f, 0f, 0f, 15f, 5f, 14f, VisibleSides: RightHalfSides));
        root.AddOrReplaceChild("lock", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lock", 0, 0, 15f, -2f, 14f, 1f, 4f, 1f, VisibleSides: RightHalfSides));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildDoubleLeftMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("bottom", VanillaPartPose.Zero,
            new VanillaCubeDefinition("bottom", 0, 19, 0f, 0f, 1f, 15f, 10f, 14f, VisibleSides: LeftHalfSides));
        root.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lid", 0, 0, 0f, 0f, 0f, 15f, 5f, 14f, VisibleSides: LeftHalfSides));
        root.AddOrReplaceChild("lock", VanillaPartPose.Offset(0f, 9f, 1f),
            new VanillaCubeDefinition("lock", 0, 0, 0f, -2f, 14f, 1f, 4f, 1f, VisibleSides: LeftHalfSides));
        return new VanillaMeshDefinition(root);
    }
}
