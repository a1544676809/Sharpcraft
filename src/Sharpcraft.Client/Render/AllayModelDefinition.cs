// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// AllayModel: small floating body with two arms and two flat wings; the whole model is translucent. Parts are declared
/// with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-678), each with <c>Kind</c>
/// set to <c>Translucent</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.allay.AllayModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/allay/AllayModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class AllayModelDefinition
{
    public const int AllayEntityTypeId = 2;

    /// <summary>Declarative DSL tree for the eight allay parts (T-678); every part is translucent.</summary>
    private static readonly VanillaMeshDefinition AllayMesh = BuildAllayMesh();

    public static readonly EntityModelDefinition Definition = new(
        "allay",
        32,
        32,
        [.. VanillaMeshBake.Bake(AllayMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/allay/allay");

    private static VanillaMeshDefinition BuildAllayMesh()
    {
        var kind = EntityGeometryLayerKind.Translucent;
        var container = new VanillaPartDefinition("container", VanillaPartPose.Zero);
        var root = container.AddOrReplaceChild("root", VanillaPartPose.Offset(0f, 23.5f, 0f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f, Kind: kind));
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -3.99f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -2.5f, -5f, -2.5f, 5f, 5f, 5f, Kind: kind));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -4f, 0f),
            new VanillaCubeDefinition("body", 0, 10, -1.5f, 0f, -1f, 3f, 4f, 2f, Kind: kind));
        body.AddOrReplaceChild("body_inner", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_inner", 0, 16, -1.5f, 0f, -1f, 3f, 5f, 2f, Grow: -0.2f, Kind: kind));
        body.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-1.75f, 0.5f, 0f),
            new VanillaCubeDefinition("right_arm", 23, 0, -0.75f, -0.5f, -1f, 1f, 4f, 2f, Grow: -0.01f, Kind: kind));
        body.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(1.75f, 0.5f, 0f),
            new VanillaCubeDefinition("left_arm", 23, 6, -0.25f, -0.5f, -1f, 1f, 4f, 2f, Grow: -0.01f, Kind: kind));
        body.AddOrReplaceChild("right_wing", VanillaPartPose.Offset(-0.5f, 0f, 0.6f),
            new VanillaCubeDefinition("right_wing", 16, 14, 0f, 1f, 0f, 0f, 5f, 8f, Kind: kind));
        body.AddOrReplaceChild("left_wing", VanillaPartPose.Offset(0.5f, 0f, 0.6f),
            new VanillaCubeDefinition("left_wing", 16, 14, 0f, 1f, 0f, 0f, 5f, 8f, Kind: kind));
        return new VanillaMeshDefinition(container);
    }
}
